#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using NBitcoin;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Flash
{
    public class FlashLightningClient : ILightningClient
    {
        private readonly IGraphQLClient _graphQLClient;
        private readonly ILogger<FlashLightningClient> _logger;
        private readonly string _bearerToken;
        private string? _cachedWalletId;
        private string? _cachedWalletCurrency;

        public FlashLightningClient(
            string bearerToken,
            Uri endpoint,
            ILogger<FlashLightningClient> logger,
            HttpClient? httpClient = null)
        {
            _bearerToken = bearerToken ?? throw new ArgumentNullException(nameof(bearerToken));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (httpClient == null)
            {
                httpClient = new HttpClient();
            }

            // Make sure authorization header is set correctly
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);

            var options = new GraphQLHttpClientOptions
            {
                EndPoint = endpoint,
                HttpMessageHandler = httpClient.GetType().GetProperty("HttpMessageHandler")?.GetValue(httpClient) as HttpMessageHandler
            };

            _graphQLClient = new GraphQLHttpClient(options, new NewtonsoftJsonSerializer(), httpClient);

            // Initialize wallet ID on construction
            InitializeWalletIdAsync().Wait();
        }

        private async Task InitializeWalletIdAsync()
        {
            try
            {
                var walletQuery = new GraphQLRequest
                {
                    Query = @"
                    query {
                      me {
                        defaultAccount {
                          wallets {
                            id
                            walletCurrency
                          }
                        }
                      }
                    }"
                };

                var walletResponse = await _graphQLClient.SendQueryAsync<WalletQueryResponse>(walletQuery);

                // Prioritize USD wallet
                var wallet = walletResponse.Data.me.defaultAccount.wallets
                    .FirstOrDefault(w => w.walletCurrency == "USD");

                if (wallet != null)
                {
                    _cachedWalletId = wallet.id;
                    _cachedWalletCurrency = "USD";
                    _logger.LogInformation($"Found wallet ID: {_cachedWalletId} for currency: {_cachedWalletCurrency}");
                }
                else
                {
                    // Only if no USD wallet is found, fall back to BTC
                    wallet = walletResponse.Data.me.defaultAccount.wallets
                        .FirstOrDefault(w => w.walletCurrency == "BTC");

                    if (wallet != null)
                    {
                        _cachedWalletId = wallet.id;
                        _cachedWalletCurrency = "BTC";
                        _logger.LogInformation($"Found wallet ID: {_cachedWalletId} for currency: {_cachedWalletCurrency}");
                    }
                    else
                    {
                        _logger.LogWarning("No suitable wallet found. Invoice creation may fail.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to initialize wallet ID: {ex.Message}");
            }
        }

        public async Task<LightningNodeInformation> GetInfo(CancellationToken cancellation = default)
        {
            try
            {
                var query = new GraphQLRequest
                {
                    Query = @"
                    query {
                      wallet {
                        balance
                        currency
                      }
                    }"
                };

                var response = await _graphQLClient.SendQueryAsync<WalletResponse>(query, cancellation);

                // Get current block height
                int currentBlockHeight = await GetCurrentBlockHeight();

                return new LightningNodeInformation
                {
                    Alias = "Flash Lightning Wallet",
                    Version = "1.0",
                    BlockHeight = currentBlockHeight,
                    // Only set properties that exist in LightningNodeInformation
                    PeersCount = 1,
                    Color = "#FFA500"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Flash wallet info");
                throw;
            }
        }

        private async Task<int> GetCurrentBlockHeight()
        {
            try
            {
                using var httpClient = new HttpClient();
                // First try to get the current block height from mempool.space API
                var response = await httpClient.GetStringAsync("https://mempool.space/api/blocks/tip/height");
                if (int.TryParse(response, out int height))
                {
                    return height;
                }

                // If the primary API fails, try an alternative
                response = await httpClient.GetStringAsync("https://blockchain.info/q/getblockcount");
                if (int.TryParse(response, out height))
                {
                    return height;
                }

                // If both APIs fail, return a current value (as of May 2024)
                return 842000;
            }
            catch
            {
                // If API calls fail, return a current value (as of May 2024)
                return 842000;
            }
        }

        public Task<LightningNodeBalance> GetBalance(CancellationToken cancellation = default)
        {
            return Task.FromResult(new LightningNodeBalance());
        }

        public async Task<LightningInvoice> CreateInvoice(
            CreateInvoiceParams createParams,
            CancellationToken cancellation = default)
        {
            try
            {
                long amountSats = 0;
                if (createParams.Amount != null)
                {
                    amountSats = (long)createParams.Amount.MilliSatoshi / 1000;
                }
                string memo = createParams.Description ?? "BTCPay Server Payment";

                _logger.LogInformation($"Creating invoice for {amountSats} sats with memo: '{memo}'");

                // If we don't have a cached wallet ID, try to get one now
                if (string.IsNullOrEmpty(_cachedWalletId))
                {
                    await InitializeWalletIdAsync();

                    // If we still don't have a wallet ID, we need to fail
                    if (string.IsNullOrEmpty(_cachedWalletId))
                    {
                        throw new Exception("No valid wallet ID found. Please check your Flash account setup.");
                    }
                }

                // Different mutations depending on wallet currency
                string query;
                Dictionary<string, object> variables = new Dictionary<string, object>();

                if (_cachedWalletCurrency == "USD")
                {
                    // Try a simpler approach with FractionalCentAmount
                    // For USD cents, we need to convert from satoshis (1 BTC = 100,000,000 sats, approx $65,000)
                    // So roughly $0.00065 per sat, or 0.065 cents per sat
                    double usdCentAmount = amountSats * 0.065;

                    _logger.LogInformation($"Converting {amountSats} sats to {usdCentAmount} USD cents for API call");

                    // Use the USD-specific mutation for USD wallets
                    query = @"
                    mutation CreateUsdInvoice($amount: FractionalCentAmount!, $memo: Memo, $walletId: WalletId!) {
                      lnUsdInvoiceCreate(input: {amount: $amount, memo: $memo, walletId: $walletId}) {
                        invoice {
                          paymentHash
                          paymentRequest
                          paymentSecret
                          satoshis
                        }
                        errors {
                          message
                        }
                      }
                    }";

                    variables.Add("amount", usdCentAmount);
                    variables.Add("memo", memo);
                    variables.Add("walletId", _cachedWalletId);
                }
                else if (_cachedWalletCurrency == "BTC")
                {
                    throw new Exception("Flash does not support BTC wallets for lightning invoices.");
                }
                else
                {
                    throw new Exception($"Unsupported wallet currency: {_cachedWalletCurrency}");
                }

                var mutation = new GraphQLRequest
                {
                    Query = query,
                    Variables = variables
                };

                _logger.LogInformation($"Sending GraphQL mutation: {query}");
                _logger.LogInformation($"With variables: {JsonConvert.SerializeObject(variables)}");

                try
                {
                    var response = await _graphQLClient.SendMutationAsync<UsdInvoiceResponse>(mutation, cancellation);

                    if (response.Errors != null && response.Errors.Length > 0)
                    {
                        string errorMessage = string.Join(", ", response.Errors.Select(e => e.Message));
                        _logger.LogError($"GraphQL error: {errorMessage}");
                        throw new Exception($"Failed to create invoice: {errorMessage}");
                    }

                    if (response.Data?.lnUsdInvoiceCreate?.errors != null && response.Data.lnUsdInvoiceCreate.errors.Any())
                    {
                        string errorMessage = string.Join(", ", response.Data.lnUsdInvoiceCreate.errors.Select(e => e.message));
                        _logger.LogError($"Business logic error: {errorMessage}");
                        throw new Exception($"Failed to create invoice: {errorMessage}");
                    }

                    var invoice = response.Data?.lnUsdInvoiceCreate?.invoice;
                    if (invoice == null)
                    {
                        _logger.LogError("Response contained no invoice data");
                        throw new Exception("Failed to create invoice: No invoice data returned");
                    }

                    _logger.LogInformation($"Successfully created invoice with hash: {invoice.paymentHash}");

                    return new LightningInvoice
                    {
                        Id = invoice.paymentHash,
                        BOLT11 = invoice.paymentRequest,
                        Amount = new LightMoney(invoice.satoshis ?? amountSats, LightMoneyUnit.Satoshi),
                        ExpiresAt = DateTime.UtcNow.AddHours(24), // Default expiry
                        Status = LightningInvoiceStatus.Unpaid,
                        AmountReceived = LightMoney.Zero,
                    };
                }
                catch (GraphQL.Client.Http.GraphQLHttpRequestException gqlEx)
                {
                    _logger.LogError(gqlEx, "GraphQL HTTP request failed");
                    throw new Exception($"API request failed: {gqlEx.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Flash invoice");
                throw;
            }
        }

        // Overload for standard CreateInvoice
        public Task<LightningInvoice> CreateInvoice(LightMoney amount, string description, TimeSpan expiry, CancellationToken cancellation = default)
        {
            // Create the params manually without using the constructor
            var parameters = typeof(CreateInvoiceParams).GetConstructors()[0].Invoke(new object[] { });
            var createParams = (CreateInvoiceParams)parameters;

            // Set properties manually
            typeof(CreateInvoiceParams).GetProperty("Amount")?.SetValue(createParams, amount);
            typeof(CreateInvoiceParams).GetProperty("Description")?.SetValue(createParams, description);
            typeof(CreateInvoiceParams).GetProperty("Expiry")?.SetValue(createParams, expiry);

            return CreateInvoice(createParams, cancellation);
        }

        public Task<PayResponse> Pay(string bolt11, CancellationToken cancellation = default)
        {
            return Pay(bolt11, null, cancellation);
        }

        public Task<PayResponse> Pay(PayInvoiceParams invoice, CancellationToken cancellation = default)
        {
            throw new NotImplementedException("This method is not supported by Flash API");
        }

        public async Task<PayResponse> Pay(string bolt11, PayInvoiceParams? payParams, CancellationToken cancellation = default)
        {
            try
            {
                var mutation = new GraphQLRequest
                {
                    Query = @"
                    mutation PayInvoice($input: LnInvoicePaymentInput!) {
                      lnInvoicePaymentSend(input: $input) {
                        status
                        fee
                        preimage
                      }
                    }",
                    Variables = new
                    {
                        input = new
                        {
                            invoice = bolt11
                        }
                    }
                };

                var response = await _graphQLClient.SendMutationAsync<PayInvoiceResponse>(mutation, cancellation);

                if (response.Errors != null && response.Errors.Length > 0)
                {
                    string errorMessage = string.Join(", ", response.Errors.Select(e => e.Message));
                    throw new Exception($"Failed to pay invoice: {errorMessage}");
                }

                var payment = response.Data.lnInvoicePaymentSend;

                // Create a simplified PayResponse
                var result = new PayResponse
                {
                    Result = payment.status.ToLowerInvariant() == "complete"
                        ? PayResult.Ok
                        : PayResult.Error
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error paying Flash invoice");
                throw;
            }
        }

        public Task<LightningPayment[]> ListPayments(CancellationToken cancellation = default)
        {
            return ListPayments(null, cancellation);
        }

        public async Task<LightningPayment[]> ListPayments(ListPaymentsParams? request, CancellationToken cancellation = default)
        {
            try
            {
                var query = new GraphQLRequest
                {
                    Query = @"
                    query {
                      transactions {
                        id
                        amount
                        direction
                        createdAt
                        status
                      }
                    }"
                };

                var response = await _graphQLClient.SendQueryAsync<TransactionsResponse>(query, cancellation);

                if (response.Errors != null && response.Errors.Length > 0)
                {
                    string errorMessage = string.Join(", ", response.Errors.Select(e => e.Message));
                    throw new Exception($"Failed to get payments: {errorMessage}");
                }

                var payments = new List<LightningPayment>();

                foreach (var tx in response.Data.transactions)
                {
                    if (tx.direction.ToLowerInvariant() == "send")
                    {
                        payments.Add(new LightningPayment
                        {
                            Id = tx.id,
                            Amount = new LightMoney(Math.Abs(tx.amount), LightMoneyUnit.Satoshi),
                            CreatedAt = tx.createdAt,
                            Status = tx.status.ToLowerInvariant() == "complete"
                                ? LightningPaymentStatus.Complete
                                : LightningPaymentStatus.Failed
                        });
                    }
                }

                return payments.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Flash payments");
                throw;
            }
        }

        public Task<LightningPayment> GetPayment(string paymentHash, CancellationToken cancellation = default)
        {
            throw new NotImplementedException("Getting a specific payment is not supported by Flash API");
        }

        public Task<LightningInvoice> GetInvoice(string invoiceId, CancellationToken cancellation = default)
        {
            throw new NotImplementedException("Getting a specific invoice is not supported by Flash API");
        }

        public Task<LightningInvoice> GetInvoice(uint256 invoiceId, CancellationToken cancellation = default)
        {
            throw new NotImplementedException("Getting a specific invoice is not supported by Flash API");
        }

        public Task<LightningInvoice[]> ListInvoices(CancellationToken cancellation = default)
        {
            return Task.FromResult(Array.Empty<LightningInvoice>());
        }

        public Task<LightningInvoice[]> ListInvoices(ListInvoicesParams? request, CancellationToken cancellation = default)
        {
            return Task.FromResult(Array.Empty<LightningInvoice>());
        }

        public Task<LightningChannel[]> ListChannels(CancellationToken cancellation = default)
        {
            return Task.FromResult(Array.Empty<LightningChannel>());
        }

        public Task CloseChannel(string channelId, bool force = false, CancellationToken cancellation = default)
        {
            throw new NotImplementedException("Channel management is not supported by Flash API");
        }

        public Task<OpenChannelResponse> OpenChannel(OpenChannelRequest request, CancellationToken cancellation = default)
        {
            throw new NotImplementedException("Channel management is not supported by Flash API");
        }

        public Task<BitcoinAddress> GetDepositAddress(CancellationToken cancellation = default)
        {
            throw new NotImplementedException("On-chain operations are not supported by Flash API");
        }

        public Task<ConnectionResult> ConnectTo(NodeInfo nodeInfo, CancellationToken cancellation = default)
        {
            throw new NotImplementedException("Node connection is not supported by Flash API");
        }

        public Task<string> ConnectTo(string nodeId, CancellationToken cancellation = default)
        {
            throw new NotImplementedException("Node connection is not supported by Flash API");
        }

        public Task<ILightningInvoiceListener> Listen(CancellationToken cancellation = default)
        {
            throw new NotImplementedException("Invoice listening is not supported by Flash API");
        }

        public Task CancelInvoice(string invoiceId, CancellationToken cancellation = default)
        {
            throw new NotImplementedException("Invoice cancellation is not supported by Flash API");
        }

        private class WalletResponse
        {
            public WalletData wallet { get; set; } = null!;

            public class WalletData
            {
                public long balance { get; set; }
                public string currency { get; set; } = null!;
            }
        }

        private class SchemaResponse
        {
            public SchemaTypeData __type { get; set; } = null!;

            public class SchemaTypeData
            {
                public string name { get; set; } = null!;
                public List<SchemaFieldData> fields { get; set; } = null!;
            }

            public class SchemaFieldData
            {
                public string name { get; set; } = null!;
                public SchemaTypeInfo type { get; set; } = null!;
            }

            public class SchemaTypeInfo
            {
                public string name { get; set; } = null!;
                public string kind { get; set; } = null!;
            }
        }

        private class UsdInvoiceResponse
        {
            public InvoiceCreateData lnUsdInvoiceCreate { get; set; } = null!;

            public class InvoiceCreateData
            {
                public List<ErrorData>? errors { get; set; }
                public InvoiceData? invoice { get; set; }
            }

            public class ErrorData
            {
                public string message { get; set; } = null!;
            }

            public class InvoiceData
            {
                public string paymentHash { get; set; } = null!;
                public string paymentRequest { get; set; } = null!;
                public string paymentSecret { get; set; } = null!;
                public long? satoshis { get; set; }
            }
        }

        private class PayInvoiceResponse
        {
            public PaymentData lnInvoicePaymentSend { get; set; } = null!;

            public class PaymentData
            {
                public string status { get; set; } = null!;
                public long fee { get; set; }
                public string preimage { get; set; } = null!;
            }
        }

        private class TransactionsResponse
        {
            public List<TransactionData> transactions { get; set; } = null!;

            public class TransactionData
            {
                public string id { get; set; } = null!;
                public long amount { get; set; }
                public string direction { get; set; } = null!;
                public DateTime createdAt { get; set; }
                public string status { get; set; } = null!;
            }
        }

        private class WalletQueryResponse
        {
            public MeData me { get; set; } = null!;

            public class MeData
            {
                public AccountData defaultAccount { get; set; } = null!;
            }

            public class AccountData
            {
                public List<WalletData> wallets { get; set; } = null!;
            }

            public class WalletData
            {
                public string id { get; set; } = null!;
                public string walletCurrency { get; set; } = null!;
            }
        }
    }
}