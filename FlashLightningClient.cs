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

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);

            var options = new GraphQLHttpClientOptions
            {
                EndPoint = endpoint,
                HttpMessageHandler = httpClient.GetType().GetProperty("HttpMessageHandler")?.GetValue(httpClient) as HttpMessageHandler
            };

            _graphQLClient = new GraphQLHttpClient(options, new NewtonsoftJsonSerializer(), httpClient);
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

                return new LightningNodeInformation
                {
                    Alias = "Flash Lightning Wallet",
                    Version = "1.0",
                    BlockHeight = 0,
                    // Other properties with default values or values from the response
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Flash wallet info");
                throw;
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

                var mutation = new GraphQLRequest
                {
                    Query = @"
                    mutation CreateInvoice($input: LnInvoiceCreateInput!) {
                      lnInvoiceCreate(input: $input) {
                        invoice
                        paymentRequest
                        paymentHash
                        paymentSecret
                        satAmount
                        expiresAt
                      }
                    }",
                    Variables = new
                    {
                        input = new
                        {
                            amount = amountSats,
                            memo = memo
                        }
                    }
                };

                var response = await _graphQLClient.SendMutationAsync<CreateInvoiceResponse>(mutation, cancellation);

                if (response.Errors != null && response.Errors.Length > 0)
                {
                    string errorMessage = string.Join(", ", response.Errors.Select(e => e.Message));
                    throw new Exception($"Failed to create invoice: {errorMessage}");
                }

                var invoice = response.Data.lnInvoiceCreate;

                return new LightningInvoice
                {
                    Id = invoice.paymentHash,
                    BOLT11 = invoice.paymentRequest,
                    Amount = new LightMoney(invoice.satAmount, LightMoneyUnit.Satoshi),
                    ExpiresAt = invoice.expiresAt,
                    Status = LightningInvoiceStatus.Unpaid,
                    AmountReceived = LightMoney.Zero,
                };
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

        private class CreateInvoiceResponse
        {
            public InvoiceData lnInvoiceCreate { get; set; } = null!;

            public class InvoiceData
            {
                public string invoice { get; set; } = null!;
                public string paymentRequest { get; set; } = null!;
                public string paymentHash { get; set; } = null!;
                public string paymentSecret { get; set; } = null!;
                public long satAmount { get; set; }
                public DateTime expiresAt { get; set; }
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
    }
}