#nullable enable
using System;
using System.Linq;
using System.Net.Http;
using BTCPayServer.Lightning;
using Microsoft.Extensions.Logging;
using Network = NBitcoin.Network;

namespace BTCPayServer.Plugins.Flash
{
    public class FlashLightningConnectionStringHandler : ILightningConnectionStringHandler
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILoggerFactory _loggerFactory;

        public FlashLightningConnectionStringHandler(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory;
            _loggerFactory = loggerFactory;
        }

        public ILightningClient? Create(string connectionString, Network network, out string? error)
        {
            var kv = LightningConnectionStringHelper.ExtractValues(connectionString, out var type);
            if (type != "flash")
            {
                error = null;
                return null;
            }

            if (!kv.TryGetValue("server", out var server))
            {
                server = "https://api.flashapp.me/graphql";
                // Use default server if not specified
            }

            if (!Uri.TryCreate(server, UriKind.Absolute, out var uri)
                || uri.Scheme != "http" && uri.Scheme != "https")
            {
                error = "The key 'server' should be an URI starting with http:// or https://";
                return null;
            }

            bool allowInsecure = false;
            if (kv.TryGetValue("allowinsecure", out var allowinsecureStr))
            {
                var allowedValues = new[] {"true", "false"};
                if (!allowedValues.Any(v => v.Equals(allowinsecureStr, StringComparison.OrdinalIgnoreCase)))
                {
                    error = "The key 'allowinsecure' should be true or false";
                    return null;
                }

                allowInsecure = allowinsecureStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            if (!LightningConnectionStringHelper.VerifySecureEndpoint(uri, allowInsecure))
            {
                error = "The key 'allowinsecure' is false, but server's Uri is not using https";
                return null;
            }

            if (!kv.TryGetValue("api-key", out var apiKey))
            {
                error = "The key 'api-key' is not found";
                return null;
            }

            error = null;

            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);

            client.BaseAddress = uri;

            kv.TryGetValue("wallet-id", out var walletId);
            var fclient = new FlashLightningClient(apiKey, uri, walletId, network, client, 
                _loggerFactory.CreateLogger($"{nameof(FlashLightningClient)}:{walletId}"));
            
            try
            {
                // Validate connection by checking network and wallet
                var res = fclient.GetNetworkAndDefaultWallet().GetAwaiter().GetResult();
                if (res.Network != network)
                {
                    error = $"The wallet is not on the right network ({res.Network.Name} instead of {network.Name})";
                    return null;
                }

                if (walletId is null && string.IsNullOrEmpty(res.DefaultWalletId))
                {
                    error = $"The wallet-id is not set and no default wallet is set";
                    return null;
                }
                
                // Use default wallet if none specified
                if (walletId is null)
                {
                    fclient.WalletId = res.DefaultWalletId;
                    fclient.WalletCurrency = res.DefaultWalletCurrency;
                    fclient.Logger = _loggerFactory.CreateLogger($"{nameof(FlashLightningClient)}:{res.DefaultWalletId}");
                }
                else
                {
                    // Validate wallet ID
                    fclient.GetBalance().GetAwaiter().GetResult();
                }
            }
            catch (Exception e)
            {
                error = $"Invalid server or api key: {e.Message}";
                return null;
            }

            return fclient;
        }
    }
}