#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Flash.Exceptions;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Implementation of GraphQL service for Flash API operations
    /// </summary>
    public class FlashGraphQLService : IFlashGraphQLService, IDisposable
    {
        private readonly IGraphQLClient _graphQLClient;
        private readonly ILogger<FlashGraphQLService> _logger;
        private readonly string _bearerToken;
        private readonly HttpClient _httpClient;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly IAsyncPolicy _retryPolicy;
        private readonly IAsyncPolicy<HttpResponseMessage> _httpPolicy;

        // Cache for wallet information
        private WalletInfo? _cachedWallet;
        private DateTime _walletCacheTime = DateTime.MinValue;
        private readonly TimeSpan _walletCacheDuration = TimeSpan.FromMinutes(30);

        public FlashGraphQLService(
            string bearerToken,
            Uri endpoint,
            ILogger<FlashGraphQLService> logger,
            HttpClient? httpClient = null,
            ILoggerFactory? loggerFactory = null)
        {
            _bearerToken = bearerToken ?? throw new ArgumentNullException(nameof(bearerToken));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory;
            
            // Initialize retry policies
            _retryPolicy = FlashRetryPolicies.GetGraphQLRetryPolicy(_logger);
            _httpPolicy = FlashRetryPolicies.GetCombinedHttpPolicy(_logger);

            // Create HttpClient with logging handler if logger factory is available
            if (httpClient != null)
            {
                _httpClient = httpClient;
            }
            else if (_loggerFactory != null)
            {
                var loggingHandler = new LoggingHttpMessageHandler(
                    _loggerFactory.CreateLogger<LoggingHttpMessageHandler>(),
                    new HttpClientHandler());
                _httpClient = new HttpClient(loggingHandler);
            }
            else
            {
                _httpClient = new HttpClient();
            }

            // Configure authorization
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
            
            // Log token info for debugging (first 10 chars only for security)
            _logger.LogInformation("[GraphQL Init] Endpoint: {Endpoint}, Token: {TokenPrefix}...", 
                endpoint, 
                _bearerToken.Length > 10 ? _bearerToken.Substring(0, 10) : "INVALID");

            // Configure GraphQL client
            var options = new GraphQLHttpClientOptions
            {
                EndPoint = endpoint
            };

            _graphQLClient = new GraphQLHttpClient(options, new NewtonsoftJsonSerializer(), _httpClient);

            _logger.LogInformation("[GraphQL Init] Flash GraphQL service initialized successfully");
            
            // Test that we can at least create a request
            try
            {
                var testRequest = new GraphQLRequest { Query = "{ __typename }" };
                _logger.LogInformation("[GraphQL Init] Test request created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GraphQL Init] Failed to create test request");
            }
        }

        public async Task<GraphQLResponse<T>> SendQueryAsync<T>(GraphQLRequest request, CancellationToken cancellation = default)
        {
            return await FlashRetryPolicies.ExecuteWithRetryAsync(
                async () =>
                {
                    try
                    {
                        _logger.LogInformation("[GraphQL Request] Query: {Query}, Variables: {Variables}, Operation: {Operation}", 
                            request.Query?.Replace("\n", " ").Replace("  ", " ").Trim(), 
                            request.Variables != null ? JsonConvert.SerializeObject(request.Variables) : "null",
                            request.OperationName ?? "null");
                        
                        var response = await _graphQLClient.SendQueryAsync<T>(request, cancellation);
                        
                        _logger.LogInformation("[GraphQL Response] Data: {Data}, Errors: {Errors}", 
                            response.Data != null ? JsonConvert.SerializeObject(response.Data) : "null",
                            response.Errors != null ? JsonConvert.SerializeObject(response.Errors) : "null");
                        
                        if (response.Errors?.Length > 0)
                        {
                            var errorMessages = string.Join(", ", response.Errors.Select(e => e.Message));
                            
                            // Check for specific error types
                            if (response.Errors.Any(e => e.Message?.Contains("authentication", StringComparison.OrdinalIgnoreCase) == true ||
                                                         e.Message?.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) == true))
                            {
                                throw new FlashAuthenticationException($"Authentication failed: {errorMessages}");
                            }
                            
                            // GraphQL errors are generally not retryable unless they indicate a server issue
                            var isRetryable = response.Errors.Any(e => e.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true ||
                                                                       e.Message?.Contains("unavailable", StringComparison.OrdinalIgnoreCase) == true);
                            
                            throw new FlashApiException($"GraphQL query failed: {errorMessages}", response.Errors, isRetryable);
                        }
                        
                        return response;
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError(ex, "HTTP error sending GraphQL query");
                        throw new FlashApiException("Network error communicating with Flash API", 
                            null, null, true, ex);
                    }
                    catch (TaskCanceledException ex)
                    {
                        _logger.LogError(ex, "GraphQL query timeout");
                        throw new FlashApiException("Flash API request timeout", 
                            HttpStatusCode.RequestTimeout, null, true, ex);
                    }
                    catch (Exception ex) when (!(ex is FlashPluginException))
                    {
                        _logger.LogError(ex, "Unexpected error sending GraphQL query: {Query}", request.Query);
                        throw new FlashPluginException("Unexpected error in GraphQL query", "GRAPHQL_UNEXPECTED_ERROR", false, ex);
                    }
                },
                _retryPolicy,
                "GraphQL Query",
                _logger);
        }

        public async Task<GraphQLResponse<T>> SendMutationAsync<T>(GraphQLRequest request, CancellationToken cancellation = default)
        {
            try
            {
                return await _graphQLClient.SendMutationAsync<T>(request, cancellation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending GraphQL mutation: {Query}", request.Query);
                throw;
            }
        }

        public async Task<WalletInfo?> GetWalletInfoAsync(CancellationToken cancellation = default)
        {
            _logger.LogInformation("[WALLET QUERY] GetWalletInfoAsync called");
            
            // Check cache first
            if (_cachedWallet != null && (DateTime.UtcNow - _walletCacheTime) < _walletCacheDuration)
            {
                _logger.LogInformation("[WALLET QUERY] Returning cached wallet: {WalletId}", _cachedWallet.Id);
                return _cachedWallet;
            }

            try
            {
                var query = new GraphQLRequest
                {
                    Query = @"
                    query getWallets {
                      me {
                        defaultAccount {
                          wallets {
                            id
                            walletCurrency
                            balance
                          }
                        }
                      }
                    }",
                    OperationName = "getWallets"
                };

                _logger.LogInformation("[WALLET QUERY] Sending wallet query to Flash API endpoint");
                var response = await SendQueryAsync<WalletQueryResponse>(query, cancellation);
                
                // Log the raw response for debugging
                _logger.LogInformation("[WALLET QUERY] GraphQL Response - Has Errors: {HasErrors}, Has Data: {HasData}, Data is null: {DataNull}", 
                    response.Errors?.Length > 0, response.Data != null, response.Data == null);

                if (response.Errors != null && response.Errors.Length > 0)
                {
                    var errorMessages = response.Errors.Select(e => 
                    {
                        var pathStr = e.Path != null ? string.Join(".", e.Path) : "N/A";
                        return $"{e.Message} (Path: {pathStr})";
                    });
                    _logger.LogError("GraphQL errors when fetching wallet: {Errors}", string.Join("; ", errorMessages));
                    
                    // Check for authentication errors
                    var authError = response.Errors.FirstOrDefault(e => 
                        e.Message.Contains("authenticate", StringComparison.OrdinalIgnoreCase) || 
                        e.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                        e.Extensions?.GetValueOrDefault("code")?.ToString() == "UNAUTHENTICATED");
                        
                    if (authError != null)
                    {
                        throw new UnauthorizedAccessException(
                            "Authentication failed. Please check your Flash API token in Store Settings → Lightning → Flash");
                    }
                }

                if (response.Data?.me?.defaultAccount?.wallets == null)
                {
                    _logger.LogWarning("[WALLET QUERY] No wallet data returned from Flash API. Response structure: me={Me}, defaultAccount={Account}, wallets={Wallets}",
                        response.Data?.me != null ? "present" : "null",
                        response.Data?.me?.defaultAccount != null ? "present" : "null",
                        response.Data?.me?.defaultAccount?.wallets != null ? "present" : "null");
                    return null;
                }

                // Prioritize USD wallet, fallback to BTC
                var wallet = response.Data.me.defaultAccount.wallets
                    .FirstOrDefault(w => string.Equals(w.walletCurrency, "USD", StringComparison.OrdinalIgnoreCase))
                    ?? response.Data.me.defaultAccount.wallets
                    .FirstOrDefault(w => string.Equals(w.walletCurrency, "BTC", StringComparison.OrdinalIgnoreCase));

                if (wallet != null)
                {
                    _cachedWallet = new WalletInfo
                    {
                        Id = wallet.id,
                        Currency = wallet.walletCurrency,
                        Balance = wallet.balance
                    };

                    _walletCacheTime = DateTime.UtcNow;
                    _logger.LogInformation("Found Flash wallet: ID={WalletId}, Currency={Currency}",
                        _cachedWallet.Id, _cachedWallet.Currency);

                    return _cachedWallet;
                }

                _logger.LogWarning("[WALLET QUERY] No suitable wallet found. Total wallets: {Count}, Looking for USD or BTC", 
                    response.Data.me.defaultAccount.wallets.Count);
                    
                // Log all available wallets for debugging
                foreach (var w in response.Data.me.defaultAccount.wallets)
                {
                    _logger.LogInformation("[WALLET QUERY] Available wallet: ID={Id}, Currency={Currency}, Balance={Balance}",
                        w.id, w.walletCurrency, w.balance);
                }
                return null;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "[WALLET QUERY] HTTP error getting wallet information. Status: {Status}, Message: {Message}", 
                    httpEx.StatusCode, httpEx.Message);
                return null;
            }
            catch (GraphQL.Client.Http.GraphQLHttpRequestException graphQLEx)
            {
                _logger.LogError(graphQLEx, "[WALLET QUERY] GraphQL HTTP error. Status: {Status}, Content: {Content}", 
                    graphQLEx.StatusCode, graphQLEx.Content);
                return null;
            }
            catch (Exception ex) when (!(ex is UnauthorizedAccessException))
            {
                _logger.LogError(ex, "[WALLET QUERY] Unexpected error getting wallet information: {Type} - {Message}", 
                    ex.GetType().Name, ex.Message);
                return null;
            }
        }

        public async Task<decimal> GetExchangeRateAsync(CancellationToken cancellation = default)
        {
            try
            {
                var query = new GraphQLRequest
                {
                    Query = @"
                    query realtimePrice {
                      realtimePrice(currency: ""USD"") {
                        btcSatPrice {
                          base
                          offset
                        }
                        usdCentPrice {
                          base
                          offset
                        }
                      }
                    }",
                    OperationName = "realtimePrice"
                };

                var response = await SendQueryAsync<ExchangeRateResponse>(query, cancellation);

                if (response.Errors != null && response.Errors.Length > 0)
                {
                    string errorMessage = string.Join(", ", response.Errors.Select(e => e.Message));
                    _logger.LogWarning("Error getting exchange rate from Flash API: {Error}", errorMessage);
                    throw new InvalidOperationException($"Failed to get exchange rate: {errorMessage}");
                }

                if (response.Data?.realtimePrice?.btcSatPrice != null)
                {
                    // btcSatPrice tells us how many minor units (cents) equal 1 satoshi
                    // For USD, this means cents per satoshi
                    double baseValue = response.Data.realtimePrice.btcSatPrice.BaseValue ?? 0;
                    int offset = response.Data.realtimePrice.btcSatPrice.offset;
                    
                    _logger.LogInformation("Flash API exchange rate data: base={Base}, offset={Offset}", baseValue, offset);
                    
                    // To avoid decimal overflow with large offsets, we need to handle the calculation carefully
                    // btcSatPrice with offset means: cents per satoshi = base * 10^(-offset)
                    // To get BTC/USD: (base * 10^-offset) cents/sat * 100,000,000 sats/BTC / 100 cents/dollar
                    // This simplifies to: base * 10^(8-offset-2) = base * 10^(6-offset)
                    
                    double btcUsdRateDouble;
                    
                    // Calculate the exponent: 6 - offset
                    int exponent = 6 - offset;
                    
                    if (exponent >= 0)
                    {
                        // Positive exponent, multiply
                        btcUsdRateDouble = baseValue * Math.Pow(10, exponent);
                    }
                    else
                    {
                        // Negative exponent, divide to avoid overflow
                        btcUsdRateDouble = baseValue / Math.Pow(10, -exponent);
                    }
                    
                    _logger.LogInformation("Calculated BTC/USD rate: base={Base}, offset={Offset}, exponent={Exponent}, result={Result}", 
                        baseValue, offset, exponent, btcUsdRateDouble);
                    
                    // Ensure the result is reasonable (BTC price should be between $1,000 and $10,000,000)
                    if (btcUsdRateDouble < 1000 || btcUsdRateDouble > 10000000)
                    {
                        _logger.LogWarning("Calculated BTC/USD rate {Rate} seems unreasonable, using fallback", btcUsdRateDouble);
                        throw new InvalidOperationException($"Unreasonable exchange rate: {btcUsdRateDouble}");
                    }
                    
                    // Check if the result is within decimal range before converting
                    if (btcUsdRateDouble > (double)decimal.MaxValue)
                    {
                        _logger.LogError("Exchange rate calculation overflow even after truncation: base={Base}, offset={Offset}, result={Result}", 
                            baseValue, offset, btcUsdRateDouble);
                        throw new OverflowException($"Exchange rate too large to handle: {btcUsdRateDouble}");
                    }
                    
                    decimal btcUsdRate = (decimal)btcUsdRateDouble;

                    _logger.LogDebug("Retrieved BTC/USD exchange rate from Flash API: {Rate} (base: {Base}, offset: {Offset})", 
                        btcUsdRate, baseValue, offset);
                    return btcUsdRate;
                }

                throw new InvalidOperationException("Exchange rate data from Flash API was null or incomplete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving exchange rate from Flash API");
                throw;
            }
        }

        public async Task<InvoiceDecodeResult> DecodeInvoiceAsync(string bolt11, CancellationToken cancellation = default)
        {
            try
            {
                var query = new GraphQLRequest
                {
                    Query = @"
                    query getInvoiceData($invoice: String!) {
                        decodeInvoice(invoice: $invoice) {
                            paymentHash
                            paymentRequest
                            amount
                            timestamp
                            expiry
                            network
                        }
                    }",
                    OperationName = "getInvoiceData",
                    Variables = new { invoice = bolt11 }
                };

                var response = await SendQueryAsync<InvoiceDataResponse>(query, cancellation);

                if (response.Errors != null && response.Errors.Length > 0)
                {
                    string errorMessage = string.Join(", ", response.Errors.Select(e => e.Message));
                    _logger.LogWarning("Error decoding invoice: {Error}", errorMessage);

                    // If decodeInvoice is not available, return basic result
                    if (errorMessage.Contains("cannot query field 'decodeInvoice'"))
                    {
                        return new InvoiceDecodeResult { PaymentRequest = bolt11 };
                    }

                    throw new InvalidOperationException($"Failed to decode invoice: {errorMessage}");
                }

                var decoded = response.Data?.decodeInvoice;
                return new InvoiceDecodeResult
                {
                    PaymentHash = decoded?.paymentHash,
                    PaymentRequest = decoded?.paymentRequest ?? bolt11,
                    AmountSats = decoded?.amount,
                    Timestamp = decoded?.timestamp,
                    Expiry = decoded?.expiry,
                    Network = decoded?.network
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decoding invoice: {Invoice}", bolt11.Substring(0, Math.Min(bolt11.Length, 20)));

                // Return basic result on error
                return new InvoiceDecodeResult { PaymentRequest = bolt11 };
            }
        }

        public async Task<List<TransactionInfo>> GetTransactionHistoryAsync(int limit = 20, CancellationToken cancellation = default)
        {
            try
            {
                var wallet = await GetWalletInfoAsync(cancellation);
                if (wallet == null)
                {
                    _logger.LogWarning("Cannot get transaction history: No wallet found");
                    return new List<TransactionInfo>();
                }

                var query = new GraphQLRequest
                {
                    Query = $@"
                    query getTransactionHistory {{
                      me {{
                        defaultAccount {{
                          wallets {{
                            id
                            transactions(first: {limit}) {{
                              edges {{
                                node {{
                                  id
                                  direction
                                  settlementAmount
                                  status
                                  createdAt
                                  memo
                                }}
                              }}
                            }}
                          }}
                        }}
                      }}
                    }}",
                    OperationName = "getTransactionHistory"
                };

                var response = await SendQueryAsync<TransactionHistoryResponse>(query, cancellation);

                if (response.Data?.me?.defaultAccount?.wallets == null)
                {
                    return new List<TransactionInfo>();
                }

                var targetWallet = response.Data.me.defaultAccount.wallets
                    .FirstOrDefault(w => w.id == wallet.Id);

                if (targetWallet?.transactions?.edges == null)
                {
                    return new List<TransactionInfo>();
                }

                return targetWallet.transactions.edges
                    .Select(edge => new TransactionInfo
                    {
                        Id = edge.node.id,
                        Memo = edge.node.memo,
                        Status = edge.node.status,
                        Direction = edge.node.direction,
                        SettlementAmount = edge.node.settlementAmount,
                        CreatedAt = DateTimeOffset.FromUnixTimeSeconds(edge.node.createdAtTimestamp).DateTime
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction history");
                return new List<TransactionInfo>();
            }
        }

        public async Task<decimal?> GetWalletBalanceAsync(CancellationToken cancellation = default)
        {
            try
            {
                var wallet = await GetWalletInfoAsync(cancellation);
                return wallet?.Balance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wallet balance");
                return null;
            }
        }

        public async Task<InvoiceStatusResult?> GetInvoiceStatusAsync(string paymentHash, CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("[INVOICE STATUS] Checking status for payment hash: {PaymentHash}", paymentHash);

                // First, try to find the invoice in recent transactions by checking memo
                var transactions = await GetTransactionHistoryAsync(100, cancellation);
                
                // Look for transactions that might be related to this payment hash
                // Flash might include the payment hash in the memo or have a recent payment with matching timing
                var recentPayments = transactions
                    .Where(t => t.Direction == "RECEIVE" && 
                               t.Status?.ToUpperInvariant() == "SUCCESS" &&
                               t.CreatedAt >= DateTime.UtcNow.AddMinutes(-10))
                    .OrderByDescending(t => t.CreatedAt)
                    .ToList();

                if (recentPayments.Any())
                {
                    _logger.LogInformation("[INVOICE STATUS] Found {Count} recent successful payments, checking for matches", recentPayments.Count);
                    
                    // If we find a recent payment, assume it's for our invoice
                    // This is a workaround since Flash doesn't provide direct payment hash lookup
                    var mostRecent = recentPayments.First();
                    
                    return new InvoiceStatusResult
                    {
                        PaymentHash = paymentHash,
                        Status = "PAID",
                        AmountReceived = mostRecent.SettlementAmount,
                        PaidAt = mostRecent.CreatedAt
                    };
                }

                _logger.LogInformation("[INVOICE STATUS] No matching payment found for hash: {PaymentHash}", paymentHash);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking invoice status for payment hash: {PaymentHash}", paymentHash);
                return null;
            }
        }

        public void Dispose()
        {
            // Check if the GraphQL client implements IDisposable before disposing
            if (_graphQLClient is IDisposable disposableGraphQLClient)
            {
                disposableGraphQLClient.Dispose();
            }
            _httpClient?.Dispose();
        }

        #region Response Classes

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
                public decimal balance { get; set; }
            }
        }

        private class ExchangeRateResponse
        {
            public RealTimePriceData? realtimePrice { get; set; }

            public class RealTimePriceData
            {
                public PriceData? btcSatPrice { get; set; }
                public PriceData? usdCentPrice { get; set; }
                public string? denominatorCurrency { get; set; }
                public string? id { get; set; }
                public long? timestamp { get; set; }
            }

            public class PriceData
            {
                [JsonProperty("base")]
                public double? BaseValue { get; set; }
                public int offset { get; set; }
            }
        }

        private class InvoiceDataResponse
        {
            public DecodeInvoiceData? decodeInvoice { get; set; }

            public class DecodeInvoiceData
            {
                public string? paymentHash { get; set; }
                public string? paymentRequest { get; set; }
                public long? amount { get; set; }
                public long? timestamp { get; set; }
                public long? expiry { get; set; }
                public string? network { get; set; }
            }
        }

        private class TransactionHistoryResponse
        {
            public MeData? me { get; set; }

            public class MeData
            {
                public AccountData? defaultAccount { get; set; }
            }

            public class AccountData
            {
                public List<WalletData>? wallets { get; set; }
            }

            public class WalletData
            {
                public string id { get; set; } = null!;
                public TransactionsData? transactions { get; set; }
            }

            public class TransactionsData
            {
                public List<EdgeData>? edges { get; set; }
            }

            public class EdgeData
            {
                public NodeData node { get; set; } = null!;
            }

            public class NodeData
            {
                public string id { get; set; } = null!;
                public string? memo { get; set; }
                public string? status { get; set; }
                public string direction { get; set; } = null!;
                public long? settlementAmount { get; set; }

                [JsonProperty("createdAt")]
                public long createdAtTimestamp { get; set; }
            }
        }

        #endregion
    }
}