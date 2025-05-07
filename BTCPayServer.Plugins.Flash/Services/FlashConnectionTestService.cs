using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Flash.Data.Models;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Http.Websocket;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NBitcoin;

namespace BTCPayServer.Plugins.Flash.Services
{
    public class FlashConnectionTestService
    {
        private readonly ILogger<FlashConnectionTestService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public FlashConnectionTestService(
            ILogger<FlashConnectionTestService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public class ConnectionTestResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public string? Network { get; set; }
            public string? DefaultWalletId { get; set; }
            public string? DefaultWalletCurrency { get; set; }
            public decimal? Balance { get; set; }
        }

        public class FlashConnectionInit
        {
            [JsonProperty("Authorization")] public string AuthToken { get; set; } = string.Empty;
        }

        public async Task<ConnectionTestResult> TestConnection(string authToken, Uri endpoint, string? walletId = null)
        {
            try
            {
                _logger.LogInformation("Testing connection to Flash API at {Endpoint}", endpoint);

                // Make sure the authorization token is in the correct format
                if (!authToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    authToken = $"Bearer {authToken}";
                }

                // Configure HTTP client
                var httpClient = _httpClientFactory.CreateClient();
                
                // Configure GraphQL client
                var client = new GraphQLHttpClient(new GraphQLHttpClientOptions() {
                    EndPoint = endpoint,
                    WebSocketEndPoint = new Uri("wss://ws.flashapp.me/graphql"),
                    WebSocketProtocol = WebSocketProtocols.GRAPHQL_TRANSPORT_WS,
                    ConfigureWebSocketConnectionInitPayload = options => new FlashConnectionInit() {AuthToken = authToken},
                }, new NewtonsoftJsonSerializer(settings =>
                {
                    if (settings.ContractResolver is CamelCasePropertyNamesContractResolver camelCasePropertyNamesContractResolver)
                    {
                        camelCasePropertyNamesContractResolver.NamingStrategy.OverrideSpecifiedNames = false;
                        camelCasePropertyNamesContractResolver.NamingStrategy.ProcessDictionaryKeys = false;
                    }
                }), httpClient);

                // Add the authorization token to the HTTP headers
                client.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken.Replace("Bearer ", ""));

                // Test query to fetch network and default wallet
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
                            balance
                          }
                        }
                      }
                    }",
                    OperationName = "GetNetworkAndDefaultWallet"
                };

                var response = await client.SendQueryAsync<dynamic>(request);

                // Extract data from the response
                var network = response.Data.globals.network.ToString();
                var defaultWalletId = (string)response.Data.me.defaultAccount.defaultWallet.id;
                var defaultWalletCurrency = (string)response.Data.me.defaultAccount.defaultWallet.currency;
                var balance = (decimal)response.Data.me.defaultAccount.defaultWallet.balance;

                // If a specific wallet ID was provided, test accessing that wallet
                if (!string.IsNullOrEmpty(walletId) && walletId != defaultWalletId)
                {
                    var walletRequest = new GraphQLRequest
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
                            walletId = walletId
                        }
                    };

                    var walletResponse = await client.SendQueryAsync<dynamic>(walletRequest);
                    if (walletResponse.Data.me.defaultAccount.walletById == null)
                    {
                        return new ConnectionTestResult
                        {
                            Success = false,
                            Message = $"Specified wallet ID {walletId} was not found"
                        };
                    }
                }

                return new ConnectionTestResult
                {
                    Success = true,
                    Message = "Successfully connected to Flash API",
                    Network = network,
                    DefaultWalletId = defaultWalletId,
                    DefaultWalletCurrency = defaultWalletCurrency,
                    Balance = balance
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection to Flash API");
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = $"Connection failed: {ex.Message}"
                };
            }
        }
    }
}