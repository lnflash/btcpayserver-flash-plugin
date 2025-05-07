#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Http.Websocket;
using GraphQL.Client.Serializer.Newtonsoft;
// Import LightMoneyUnit if available
#pragma warning disable CS0168
// ReSharper disable once UnusedVariable
using LightMoneyUnit = BTCPayServer.Lightning.LightMoneyUnit;
#pragma warning restore CS0168
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Network = NBitcoin.Network;

namespace BTCPayServer.Plugins.Flash
{
    public class FlashLightningClient : ILightningClient
    {
        private readonly string _authToken;
        private readonly Uri _apiEndpoint;
        public string? WalletId { get; set; }
        public string? WalletCurrency { get; set; }
        private readonly Network _network;
        public ILogger? Logger;
        private readonly GraphQLHttpClient _client;

        public class FlashConnectionInit
        {
            [JsonProperty("Authorization")] public string AuthToken { get; set; } = string.Empty;
        }

        public FlashLightningClient(string authToken, Uri apiEndpoint, string? walletId, Network network, HttpClient httpClient, ILogger? logger)
        {
            _authToken = authToken;
            _apiEndpoint = apiEndpoint;
            WalletId = walletId;
            _network = network;
            Logger = logger;
            
            // Make sure the authorization token is in the correct format
            if (!_authToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                _authToken = $"Bearer {_authToken}";
            }
            
            // Configure GraphQL client
            _client = new GraphQLHttpClient(new GraphQLHttpClientOptions() {
                EndPoint = _apiEndpoint,
                WebSocketEndPoint = new Uri("wss://" + _apiEndpoint.Host.Replace("api.", "ws.") + _apiEndpoint.PathAndQuery),
                WebSocketProtocol = WebSocketProtocols.GRAPHQL_TRANSPORT_WS,
                ConfigureWebSocketConnectionInitPayload = options => new FlashConnectionInit() {AuthToken = _authToken},
                ConfigureWebsocketOptions = _ => { }
            }, new NewtonsoftJsonSerializer(settings =>
            {
                if (settings.ContractResolver is CamelCasePropertyNamesContractResolver camelCasePropertyNamesContractResolver)
                {
                    camelCasePropertyNamesContractResolver.NamingStrategy.OverrideSpecifiedNames = false;
                    camelCasePropertyNamesContractResolver.NamingStrategy.ProcessDictionaryKeys = false;
                }
            }), httpClient);
        }

        public override string ToString()
        {
            return $"type=flash;server={_apiEndpoint};auth-token={_authToken}{(WalletId is null? "":$";wallet-id={WalletId}")}";
        }

        public async Task<(Network Network, string DefaultWalletId, string DefaultWalletCurrency)> GetNetworkAndDefaultWallet(CancellationToken cancellation = default)
        {
            var request = new GraphQLRequest
            {
                Query = @"
query GetNetworkAndDefaultWallet {
  globals {
    network
  }
  me {
    defaultAccount {
      defaultWallet {
        id
        currency
      }
    }
  }
}",
                OperationName = "GetNetworkAndDefaultWallet"
            };
            
            var response = await _client.SendQueryAsync<dynamic>(request, cancellation);

            var defaultWalletId = (string)response.Data.me.defaultAccount.defaultWallet.id;
            var defaultWalletCurrency = (string)response.Data.me.defaultAccount.defaultWallet.currency;
            var network = response.Data.globals.network.ToString() switch
            {
                "mainnet" => Network.Main,
                "testnet" => Network.TestNet,
                "regtest" => Network.RegTest,
                _ => throw new ArgumentOutOfRangeException()
            };
            return (network, defaultWalletId, defaultWalletCurrency);
        }

        #region ILightningClient Implementation

        // The following methods need to be implemented according to Flash API
        // For now, these are placeholder implementations
        
        public async Task<LightningInvoice?> GetInvoice(string invoiceId, CancellationToken cancellation = default)
        {
            if (WalletId == null)
                throw new InvalidOperationException("WalletId is required for getting invoice");
            
            var request = new GraphQLRequest
            {
                Query = @"
                query InvoiceByPaymentHash($paymentHash: PaymentHash!, $walletId: WalletId!) {
                  me {
                    defaultAccount {
                      walletById(walletId: $walletId) {
                        invoiceByPaymentHash(paymentHash: $paymentHash) {
                          createdAt
                          paymentHash
                          paymentRequest
                          paymentSecret
                          paymentStatus
                          satoshis
                        }
                      }
                    }
                  }
                }",
                OperationName = "InvoiceByPaymentHash",
                Variables = new
                {
                    walletId = WalletId,
                    paymentHash = invoiceId
                }
            };
            
            try
            {
                var response = await _client.SendQueryAsync<JObject>(request, cancellation);
                
                // Extract the invoice data
                var invoiceData = response.Data?["me"]?["defaultAccount"]?["walletById"]?["invoiceByPaymentHash"];
                
                if (invoiceData == null)
                    return null;
                
                return ToInvoice(invoiceData as JObject);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting invoice {InvoiceId}", invoiceId);
                throw;
            }
        }

        public async Task<LightningInvoice?> GetInvoice(uint256 paymentHash, CancellationToken cancellation = default)
        {
            return await GetInvoice(paymentHash.ToString(), cancellation);
        }
        
        private LightningInvoice? ToInvoice(JObject? invoiceData)
        {
            if (invoiceData == null)
                return null;
                
            var bolt11 = invoiceData["paymentRequest"]?.Value<string>();
            if (bolt11 == null)
                return null;
                
            var bolt11Parsed = BOLT11PaymentRequest.Parse(bolt11, _network);
            
            return new LightningInvoice
            {
                Id = invoiceData["paymentHash"]?.Value<string>(),
                Amount = invoiceData["satoshis"] != null 
                    ? LightMoney.Satoshis(invoiceData["satoshis"].Value<long>()) 
                    : bolt11Parsed.MinimumAmount,
                Preimage = invoiceData["paymentSecret"]?.Value<string>(),
                Status = invoiceData["paymentStatus"]?.Value<string>() switch
                {
                    "EXPIRED" => LightningInvoiceStatus.Expired,
                    "PAID" => LightningInvoiceStatus.Paid,
                    "PENDING" => LightningInvoiceStatus.Unpaid,
                    _ => LightningInvoiceStatus.Unpaid
                },
                BOLT11 = bolt11,
                PaymentHash = invoiceData["paymentHash"]?.Value<string>(),
                ExpiresAt = bolt11Parsed.ExpiryDate,
                PaidAt = invoiceData["paymentStatus"]?.Value<string>() == "PAID" ? DateTimeOffset.UtcNow : null
            };
        }

        public async Task<LightningInvoice[]> ListInvoices(CancellationToken cancellation = default)
        {
            return await ListInvoices(new ListInvoicesParams(), cancellation);
        }

        public async Task<LightningInvoice[]> ListInvoices(ListInvoicesParams request, CancellationToken cancellation = default)
        {
            if (WalletId == null)
                throw new InvalidOperationException("WalletId is required for listing invoices");
                
            var graphQLRequest = new GraphQLRequest
            {
                Query = @"
                query Invoices($walletId: WalletId!) {
                  me {
                    defaultAccount {
                      walletById(walletId: $walletId) {
                        invoices {
                          edges {
                            node {
                              createdAt
                              paymentHash
                              paymentRequest
                              paymentSecret
                              paymentStatus
                              ... on LnInvoice {
                                satoshis
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }",
                OperationName = "Invoices",
                Variables = new
                {
                    walletId = WalletId
                }
            };
            
            try
            {
                var response = await _client.SendQueryAsync<JObject>(graphQLRequest, cancellation);
                
                // Extract the invoice edges from the response
                var edges = response.Data?["me"]?["defaultAccount"]?["walletById"]?["invoices"]?["edges"] as JArray;
                
                if (edges == null || edges.Count == 0)
                    return Array.Empty<LightningInvoice>();
                
                // Convert the edges to LightningInvoice objects
                var invoices = new List<LightningInvoice>();
                foreach (var edge in edges)
                {
                    var node = edge["node"] as JObject;
                    var invoice = ToInvoice(node);
                    
                    // Apply filtering based on request parameters
                    if (invoice != null && 
                        (!request.PendingOnly.HasValue || !request.PendingOnly.Value || invoice.Status == LightningInvoiceStatus.Unpaid))
                    {
                        invoices.Add(invoice);
                    }
                }
                
                return invoices.ToArray();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error listing invoices");
                throw;
            }
        }

        public async Task<LightningPayment?> GetPayment(string paymentHash, CancellationToken cancellation = default)
        {
            if (WalletId == null)
                throw new InvalidOperationException("WalletId is required for getting payment");
            
            var request = new GraphQLRequest
            {
                Query = @"
                query TransactionsByPaymentHash($paymentHash: PaymentHash!, $walletId: WalletId!) {
                  me {
                    defaultAccount {
                      walletById(walletId: $walletId) {
                        transactionsByPaymentHash(paymentHash: $paymentHash) {
                          createdAt
                          direction
                          id
                          initiationVia {
                            ... on InitiationViaLn {
                              paymentHash
                              paymentRequest
                            }
                          }
                          memo
                          settlementAmount
                          settlementCurrency
                          settlementVia {
                            ... on SettlementViaLn {
                              preImage
                            }
                            ... on SettlementViaIntraLedger {
                              preImage
                            }
                          }
                          status
                        }
                      }
                    }
                  }
                }",
                OperationName = "TransactionsByPaymentHash",
                Variables = new
                {
                    walletId = WalletId,
                    paymentHash = paymentHash
                }
            };
            
            try
            {
                var response = await _client.SendQueryAsync<JObject>(request, cancellation);
                
                // Extract the transactions array
                var transactions = response.Data?["me"]?["defaultAccount"]?["walletById"]?["transactionsByPaymentHash"] as JArray;
                
                if (transactions == null || transactions.Count == 0)
                    return null;
                    
                // Get the first transaction (there should only be one for a given payment hash)
                var transaction = transactions.First() as JObject;
                return ToLightningPayment(transaction);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting payment {PaymentHash}", paymentHash);
                throw;
            }
        }

        public async Task<LightningPayment[]> ListPayments(CancellationToken cancellation = default)
        {
            return await ListPayments(new ListPaymentsParams(), cancellation);
        }

        public async Task<LightningPayment[]> ListPayments(ListPaymentsParams request, CancellationToken cancellation = default)
        {
            if (WalletId == null)
                throw new InvalidOperationException("WalletId is required for listing payments");
            
            var graphQLRequest = new GraphQLRequest
            {
                Query = @"
                query Transactions($walletId: WalletId!) {
                  me {
                    defaultAccount {
                      walletById(walletId: $walletId) {
                        transactions {
                          edges {
                            node {
                              createdAt
                              direction
                              id
                              initiationVia {
                                ... on InitiationViaLn {
                                  paymentHash
                                  paymentRequest
                                }
                              }
                              memo
                              settlementAmount
                              settlementCurrency
                              settlementVia {
                                ... on SettlementViaLn {
                                  preImage
                                }
                                ... on SettlementViaIntraLedger {
                                  preImage
                                }
                              }
                              status
                            }
                          }
                        }
                      }
                    }
                  }
                }",
                OperationName = "Transactions",
                Variables = new
                {
                    walletId = WalletId
                }
            };
            
            try
            {
                var response = await _client.SendQueryAsync<JObject>(graphQLRequest, cancellation);
                
                // Extract the transaction edges from the response
                var edges = response.Data?["me"]?["defaultAccount"]?["walletById"]?["transactions"]?["edges"] as JArray;
                
                if (edges == null || edges.Count == 0)
                    return Array.Empty<LightningPayment>();
                
                // Convert the edges to LightningPayment objects
                var payments = new List<LightningPayment>();
                foreach (var edge in edges)
                {
                    var node = edge["node"] as JObject;
                    
                    // Skip non-outgoing transactions
                    if (node?["direction"]?.Value<string>() != "SEND")
                        continue;
                        
                    var payment = ToLightningPayment(node);
                    
                    // Apply filtering based on request parameters
                    if (payment != null && 
                        (!request.IncludePending.HasValue || !request.IncludePending.Value || payment.Status != LightningPaymentStatus.Pending))
                    {
                        payments.Add(payment);
                    }
                }
                
                return payments.ToArray();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error listing payments");
                throw;
            }
        }
        
        private LightningPayment? ToLightningPayment(JObject? transaction)
        {
            if (transaction == null)
                return null;
                
            // Skip receive transactions
            if (transaction["direction"]?.Value<string>() == "RECEIVE")
                return null;
                
            var initiationVia = transaction["initiationVia"];
            if (initiationVia?["paymentHash"] == null || initiationVia?["paymentRequest"] == null)
                return null;
                
            var bolt11 = initiationVia["paymentRequest"].Value<string>();
            var bolt11Parsed = BOLT11PaymentRequest.Parse(bolt11, _network);
            
            var createdAtUnix = transaction["createdAt"]?.Value<long>() ?? 0;
            var createdAt = createdAtUnix > 0 
                ? DateTimeOffset.FromUnixTimeSeconds(createdAtUnix)
                : DateTimeOffset.UtcNow;
                
            var preimage = transaction["settlementVia"]?["preImage"]?.Value<string>();
            
            return new LightningPayment
            {
                Id = initiationVia["paymentHash"].Value<string>(),
                PaymentHash = initiationVia["paymentHash"].Value<string>(),
                Preimage = preimage,
                Amount = bolt11Parsed.MinimumAmount,
                AmountSent = bolt11Parsed.MinimumAmount,
                CreatedAt = createdAt,
                BOLT11 = bolt11,
                Status = transaction["status"]?.Value<string>() switch
                {
                    "FAILURE" => LightningPaymentStatus.Failed,
                    "PENDING" => LightningPaymentStatus.Pending,
                    "SUCCESS" => LightningPaymentStatus.Complete,
                    _ => LightningPaymentStatus.Unknown
                }
            };
        }

        public async Task<LightningInvoice> CreateInvoice(LightMoney amount, string description, TimeSpan expiry, CancellationToken cancellation = default)
        {
            return await CreateInvoice(new CreateInvoiceParams(amount, description, expiry), cancellation);
        }

        public async Task<LightningInvoice> CreateInvoice(CreateInvoiceParams createInvoiceRequest, CancellationToken cancellation = default)
        {
            if (WalletId == null)
                throw new InvalidOperationException("WalletId is required for creating invoices");
            
            // Determine the appropriate mutation based on wallet currency
            string query;
            string operationName;
            string resultField;
            
            if (WalletCurrency?.Equals("USD", StringComparison.InvariantCultureIgnoreCase) == true)
            {
                // USD invoice creation
                query = @"
                mutation LnUsdInvoiceCreate($input: LnUsdInvoiceCreateInput!) {
                  lnUsdInvoiceCreate(input: $input) {
                    errors { message }
                    invoice {
                      paymentRequest
                      paymentHash
                      paymentSecret
                      satoshis
                    }
                  }
                }";
                operationName = "LnUsdInvoiceCreate";
                resultField = "lnUsdInvoiceCreate";
            }
            else
            {
                // BTC invoice creation
                query = @"
                mutation LnInvoiceCreate($input: LnInvoiceCreateInput!) {
                  lnInvoiceCreate(input: $input) {
                    errors { message }
                    invoice {
                      paymentRequest
                      paymentHash
                      paymentSecret
                      satoshis
                    }
                  }
                }";
                operationName = "LnInvoiceCreate";
                resultField = "lnInvoiceCreate";
            }
            
            var request = new GraphQLRequest
            {
                Query = query,
                OperationName = operationName,
                Variables = new
                {
                    input = new
                    {
                        walletId = WalletId,
                        // Convert to appropriate amount based on currency
                        amount = WalletCurrency?.Equals("USD", StringComparison.InvariantCultureIgnoreCase) == true 
                            ? Convert.ToInt64(createInvoiceRequest.Amount.ToUnit(LightMoneyUnit.Satoshi) / 100) // Convert sats to cents
                            : Convert.ToInt64(createInvoiceRequest.Amount.ToUnit(LightMoneyUnit.Satoshi)), // Keep as sats
                        memo = createInvoiceRequest.Description,
                        expiresIn = (int)createInvoiceRequest.Expiry.TotalMinutes // Convert TimeSpan to minutes
                    }
                }
            };
            
            try
            {
                var response = await _client.SendQueryAsync<JObject>(request, cancellation);
                
                // Extract the result object
                var result = response.Data[resultField];
                
                // Check for errors
                var errors = result["errors"] as JArray;
                if (errors != null && errors.Count > 0)
                {
                    var errorMessage = errors[0]["message"].ToString();
                    throw new Exception($"Error creating invoice: {errorMessage}");
                }
                
                // Extract invoice data
                var invoiceData = result["invoice"];
                if (invoiceData == null)
                    throw new Exception("Invoice data is null in response");
                
                // Parse BOLT11 to get additional info
                var bolt11 = invoiceData["paymentRequest"].ToString();
                var bolt11Parsed = BOLT11PaymentRequest.Parse(bolt11, _network);
                
                // Create and return a LightningInvoice object
                return new LightningInvoice
                {
                    Id = invoiceData["paymentHash"].ToString(),
                    BOLT11 = bolt11,
                    PaymentHash = invoiceData["paymentHash"].ToString(),
                    Preimage = invoiceData["paymentSecret"]?.ToString(),
                    Amount = invoiceData["satoshis"] != null 
                        ? LightMoney.Satoshis(invoiceData["satoshis"].Value<long>())
                        : bolt11Parsed.MinimumAmount,
                    Status = LightningInvoiceStatus.Unpaid,
                    ExpiresAt = bolt11Parsed.ExpiryDate
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error creating invoice");
                throw;
            }
        }

        public async Task<ILightningInvoiceListener> Listen(CancellationToken cancellation = default)
        {
            return new FlashInvoiceListener(_client, this, Logger);
        }
        
        private class FlashInvoiceListener : ILightningInvoiceListener
        {
            private readonly GraphQLHttpClient _client;
            private readonly FlashLightningClient _lightningClient;
            private readonly Channel<LightningInvoice> _invoices = Channel.CreateUnbounded<LightningInvoice>();
            private readonly IDisposable _subscription;
            private readonly ILogger _logger;
            
            public FlashInvoiceListener(GraphQLHttpClient client, FlashLightningClient lightningClient, ILogger logger)
            {
                _client = client;
                _lightningClient = lightningClient;
                _logger = logger;
                
                try
                {
                    // Subscribe to payment updates
                    var stream = _client.CreateSubscriptionStream<JObject>(new GraphQLRequest
                    {
                        Query = @"
                        subscription MyUpdates {
                          myUpdates {
                            update {
                              ... on LnUpdate {
                                transaction {
                                  initiationVia {
                                    ... on InitiationViaLn {
                                      paymentHash
                                    }
                                  }
                                  direction
                                }
                              }
                            }
                          }
                        }",
                        OperationName = "MyUpdates"
                    });
                    
                    _subscription = stream.Subscribe(async response =>
                    {
                        try
                        {
                            if (response.Data == null)
                                return;
                                
                            // Check if it's a receive transaction
                            if (response.Data.SelectToken("myUpdates.update.transaction.direction")?.Value<string>() != "RECEIVE")
                                return;
                                
                            // Get the payment hash
                            var paymentHash = response.Data.SelectToken("myUpdates.update.transaction.initiationVia.paymentHash")?.Value<string>();
                            if (paymentHash == null)
                                return;
                                
                            // Get the invoice details
                            var invoice = await _lightningClient.GetInvoice(paymentHash);
                            if (invoice != null)
                            {
                                // Write to the channel
                                await _invoices.Writer.WriteAsync(invoice);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing invoice update");
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating invoice listener");
                }
            }
            
            public void Dispose()
            {
                _subscription.Dispose();
                _invoices.Writer.Complete();
            }
            
            public async Task<LightningInvoice> WaitInvoice(CancellationToken cancellation)
            {
                return await _invoices.Reader.ReadAsync(cancellation);
            }
        }

        public Task<LightningNodeInformation> GetInfo(CancellationToken cancellation = default)
        {
            // Flash doesn't expose node information via the API
            // Return a minimal placeholder with limited information
            
            // Create a dummy PubKey for the node ID
            var dummyPubKey = new PubKey("032a7572dc013b6382cde391d79f292ced27305aa4162ec3906279fc4334602543");
            
            var nodeInfo = new LightningNodeInformation
            {
                BlockHeight = 0,
                Alias = "Flash Wallet",
                Color = "#00FF00",
                Version = "Flash API",
                PeersCount = 0,
                ActiveChannelsCount = 0,
                InactiveChannelsCount = 0,
                PendingChannelsCount = 0
            };
            
            // Use reflection to set the NodeId property if it exists
            var nodeIdProp = typeof(LightningNodeInformation).GetProperty("NodeId");
            if (nodeIdProp != null)
            {
                nodeIdProp.SetValue(nodeInfo, dummyPubKey);
            }
            
            return Task.FromResult(nodeInfo);
        }

        public async Task<LightningNodeBalance> GetBalance(CancellationToken cancellation = default)
        {
            if (WalletId == null)
                throw new InvalidOperationException("WalletId is required for getting balance");
                
            var request = new GraphQLRequest
            {
                Query = @"
                query GetWallet($walletId: WalletId!) {
                  me {
                    defaultAccount {
                      walletById(walletId: $walletId) {
                        id
                        balance
                        walletCurrency
                      }
                    }
                  }
                }",
                OperationName = "GetWallet",
                Variables = new
                {
                    walletId = WalletId
                }
            };
            
            try
            {
                var response = await _client.SendQueryAsync<JObject>(request, cancellation);
                
                // Extract the wallet data
                var walletData = response.Data?["me"]?["defaultAccount"]?["walletById"];
                
                if (walletData == null)
                    throw new Exception("Failed to get wallet data");
                
                // Store wallet currency for future use
                WalletCurrency = walletData["walletCurrency"]?.Value<string>();
                
                // Create the balance object based on wallet currency
                if (walletData["walletCurrency"]?.Value<string>()?.Equals("BTC", StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    return new LightningNodeBalance
                    {
                        OffchainBalance = new OffchainBalance
                        {
                            Local = LightMoney.Satoshis(walletData["balance"]?.Value<long>() ?? 0)
                        }
                    };
                }
                
                // For non-BTC wallets, return a zero balance
                return new LightningNodeBalance
                {
                    OffchainBalance = new OffchainBalance
                    {
                        Local = LightMoney.Zero
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting wallet balance");
                throw;
            }
        }

        public async Task<PayResponse> Pay(PayInvoiceParams payParams, CancellationToken cancellation = default)
        {
            // Get the BOLT11 invoice string
            string? bolt11 = null;
            
            // Check if the PayInvoiceParams has a property that stores the BOLT11
            var type = payParams.GetType();
            var boltProp = type.GetProperty("BOLT11") ?? type.GetProperty("PaymentRequest");
            
            if (boltProp != null)
            {
                bolt11 = boltProp.GetValue(payParams) as string;
            }
            
            if (string.IsNullOrEmpty(bolt11))
                throw new ArgumentException("BOLT11 invoice string is required", nameof(payParams));
                
            return await Pay(bolt11, payParams, cancellation);
        }

        public async Task<PayResponse> Pay(string bolt11, PayInvoiceParams payParams, CancellationToken cancellation = default)
        {
            if (WalletId == null)
                throw new InvalidOperationException("WalletId is required for making payments");
                
            var bolt11Parsed = BOLT11PaymentRequest.Parse(bolt11, _network);
            bool isNoAmountInvoice = bolt11Parsed.MinimumAmount == LightMoney.Zero;
            
            GraphQLRequest request;
            string resultField;
            
            // Handle different invoice types (with amount vs. no amount)
            if (isNoAmountInvoice && payParams.Amount != null)
            {
                // No amount invoice with amount specified
                request = new GraphQLRequest
                {
                    Query = @"
                    mutation LnNoAmountInvoicePaymentSend($input: LnNoAmountInvoicePaymentInput!) {
                      lnNoAmountInvoicePaymentSend(input: $input) {
                        errors { message }
                        status
                        transaction {
                          id
                          status
                          settlementVia {
                            ... on SettlementViaLn {
                              preImage
                            }
                          }
                        }
                      }
                    }",
                    OperationName = "LnNoAmountInvoicePaymentSend",
                    Variables = new
                    {
                        input = new
                        {
                            walletId = WalletId,
                            paymentRequest = bolt11,
                            amount = GetAmountSatoshi(payParams),
                            memo = GetDescription(payParams)
                        }
                    }
                };
                resultField = "lnNoAmountInvoicePaymentSend";
            }
            else
            {
                // Regular invoice with amount
                request = new GraphQLRequest
                {
                    Query = @"
                    mutation LnInvoicePaymentSend($input: LnInvoicePaymentInput!) {
                      lnInvoicePaymentSend(input: $input) {
                        errors { message }
                        status
                        transaction {
                          id
                          status
                          initiationVia {
                            ... on InitiationViaLn {
                              paymentHash
                            }
                          }
                          settlementVia {
                            ... on SettlementViaLn {
                              preImage
                            }
                          }
                        }
                      }
                    }",
                    OperationName = "LnInvoicePaymentSend",
                    Variables = new
                    {
                        input = new
                        {
                            walletId = WalletId,
                            paymentRequest = bolt11,
                            memo = GetDescription(payParams)
                        }
                    }
                };
                resultField = "lnInvoicePaymentSend";
            }
            
            try
            {
                // Create a linked cancellation token source with timeout
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellation,
                    new CancellationTokenSource(payParams?.SendTimeout ?? PayInvoiceParams.DefaultSendTimeout).Token);
                
                var response = await _client.SendQueryAsync<JObject>(request, cts.Token);
                
                // Extract the result
                var result = response.Data[resultField];
                
                // Check for errors
                var errors = result["errors"] as JArray;
                if (errors != null && errors.Count > 0)
                {
                    var errorMessage = errors[0]["message"].ToString();
                    Logger.LogError("Payment error: {ErrorMessage}", errorMessage);
                    
                    // Create PayResponse with error result
                    var payResponse = new PayResponse(PayResult.Error);
                    
                    // Try to set error message using reflection (if the property exists)
                    var type = payResponse.GetType();
                    var errorProp = type.GetProperty("ErrorMessage");
                    if (errorProp != null && errorProp.CanWrite)
                    {
                        errorProp.SetValue(payResponse, errorMessage);
                    }
                    
                    Logger.LogError("Payment error: {ErrorMessage}", errorMessage);
                    return payResponse;
                }
                
                // Extract payment status
                var status = result["status"]?.Value<string>();
                var transaction = result["transaction"] as JObject;
                
                if (transaction == null)
                {
                    // Create PayResponse with status result
                    var statusResponse = new PayResponse(MapPaymentStatus(status));
                    
                    // Try to set error message using reflection (if the property exists)
                    var type = statusResponse.GetType();
                    var errorProp = type.GetProperty("ErrorMessage");
                    if (errorProp != null && errorProp.CanWrite)
                    {
                        errorProp.SetValue(statusResponse, "No transaction details returned");
                    }
                    
                    return statusResponse;
                }
                
                // Create response with payment details
                var successResponse = new PayResponse
                {
                    Result = MapPaymentStatus(status)
                };
                
                // Add payment details if available
                if (transaction != null)
                {
                    var paymentHash = bolt11Parsed.PaymentHash;
                    if (paymentHash == null && transaction["initiationVia"]?["paymentHash"] != null)
                    {
                        paymentHash = new uint256(transaction["initiationVia"]["paymentHash"].Value<string>());
                    }
                    
                    var preimage = transaction["settlementVia"]?["preImage"]?.Value<string>();
                    
                    successResponse.Details = new PayDetails
                    {
                        PaymentHash = paymentHash,
                        Preimage = preimage != null ? new uint256(preimage) : null,
                        Status = MapPaymentStatusToLightningPaymentStatus(status)
                    };
                }
                
                return successResponse;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error paying invoice {BOLT11}", bolt11);
                // Create PayResponse with error result
                var errorResponse = new PayResponse(PayResult.Error);
                
                // Try to set error message using reflection (if the property exists)
                var type = errorResponse.GetType();
                var errorProp = type.GetProperty("ErrorMessage");
                if (errorProp != null && errorProp.CanWrite)
                {
                    errorProp.SetValue(errorResponse, ex.Message);
                }
                
                return errorResponse;
            }
        }

        public async Task<PayResponse> Pay(string bolt11, CancellationToken cancellation = default)
        {
            return await Pay(bolt11, new PayInvoiceParams(), cancellation);
        }
        
        private PayResult MapPaymentStatus(string status)
        {
            return status switch
            {
                "SUCCESS" => PayResult.Ok,
                "ALREADY_PAID" => PayResult.Ok,
                "PENDING" => PayResult.Unknown,
                "FAILURE" => PayResult.Error,
                _ => PayResult.Unknown
            };
        }
        
        private LightningPaymentStatus MapPaymentStatusToLightningPaymentStatus(string status)
        {
            return status switch
            {
                "SUCCESS" => LightningPaymentStatus.Complete,
                "ALREADY_PAID" => LightningPaymentStatus.Complete,
                "PENDING" => LightningPaymentStatus.Pending,
                "FAILURE" => LightningPaymentStatus.Failed,
                _ => LightningPaymentStatus.Unknown
            };
        }
        
        private string? GetDescription(PayInvoiceParams payParams)
        {
            // Try to get Description property value using reflection
            var type = payParams.GetType();
            var descProp = type.GetProperty("Description") ?? type.GetProperty("Comment") ?? type.GetProperty("Memo");
            
            if (descProp != null)
            {
                return descProp.GetValue(payParams) as string;
            }
            
            return null;
        }
        
        private long GetAmountSatoshi(PayInvoiceParams payParams)
        {
            // Try to get Amount property value using reflection
            var type = payParams.GetType();
            var amountProp = type.GetProperty("Amount");
            
            if (amountProp != null)
            {
                var amount = amountProp.GetValue(payParams);
                if (amount != null)
                {
                    // Check if it's a LightMoney type
                    if (amount is LightMoney lightMoney)
                    {
                        // Access satoshis via reflection since the property might be different
                        var satoshiProp = typeof(LightMoney).GetProperty("Satoshi") ?? 
                                         typeof(LightMoney).GetProperty("SatoshiValue") ??
                                         typeof(LightMoney).GetProperty("Value");
                        
                        if (satoshiProp != null)
                        {
                            var value = satoshiProp.GetValue(lightMoney);
                            if (value is long longValue)
                                return longValue;
                            if (value is decimal decimalValue)
                                return (long)decimalValue;
                        }
                        
                        // Fallback: try to use ToUnit method to get satoshis
                        var toUnitMethod = typeof(LightMoney).GetMethod("ToUnit");
                        if (toUnitMethod != null)
                        {
                            try
                            {
                                var unitType = typeof(LightMoneyUnit);
                                var satoshiUnit = LightMoneyUnit.Satoshi;
                                var result = toUnitMethod.Invoke(lightMoney, new object[] { satoshiUnit });
                                if (result is decimal decimalResult)
                                    return (long)decimalResult;
                            }
                            catch
                            {
                                // Just in case there's an error with reflection
                            }
                        }
                        
                        // Last resort, try to cast to long
                        try
                        {
                            return Convert.ToInt64(lightMoney);
                        }
                        catch
                        {
                            // Ignore conversion errors
                        }
                    }
                    
                    // Try to convert other numeric types
                    if (amount is IConvertible convertible)
                    {
                        return convertible.ToInt64(null);
                    }
                }
            }
            
            // Default to 0 if we can't get the amount
            return 0;
        }

        public Task CancelInvoice(string invoiceId, CancellationToken cancellation = default)
        {
            // Flash doesn't support cancelling invoices via the API
            Logger.LogWarning("CancelInvoice is not supported by Flash API");
            return Task.CompletedTask;
        }

        public Task<BitcoinAddress> GetDepositAddress(CancellationToken cancellation = default)
        {
            // This method is meant to get an on-chain deposit address
            // For Lightning-only implementations like Flash, we don't support this
            throw new NotSupportedException("GetDepositAddress is not supported by Flash API");
        }

        public Task<OpenChannelResponse> OpenChannel(OpenChannelRequest openChannelRequest, CancellationToken cancellation = default)
        {
            // Flash doesn't expose channel management via the API
            throw new NotSupportedException("OpenChannel is not supported by Flash API");
        }

        public Task<ConnectionResult> ConnectTo(NodeInfo nodeInfo, CancellationToken cancellation = default)
        {
            // Flash doesn't expose node connection via the API
            throw new NotSupportedException("ConnectTo is not supported by Flash API");
        }

        public Task<LightningChannel[]> ListChannels(CancellationToken cancellation = default)
        {
            // Flash doesn't expose channel information via the API
            return Task.FromResult(Array.Empty<LightningChannel>());
        }
        
        #endregion
    }
}