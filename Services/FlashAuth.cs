using System;
using System.Net.Http.Headers;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Headers every Flash API request needs.
    /// API keys from console.flashapp.me (fk_&lt;keyId&gt;_&lt;secret&gt;) go in the X-API-KEY header;
    /// anything else is treated as a Kratos session token and sent as a Bearer token.
    /// Requests also declare USDT cash-wallet support: accounts moved to the USDT cash wallet
    /// keep their funds there, and without the capability the API presents the old USD wallet,
    /// whose payments are sent from an empty account.
    /// </summary>
    public static class FlashAuth
    {
        public const string ApiKeyHeader = "X-API-KEY";
        public const string ClientCapabilitiesHeader = "X-Flash-Client-Capabilities";
        public const string UsdtCashWalletCapability = "cash-wallet-usdt-v1";
        private const string ApiKeyPrefix = "fk_";

        public static bool IsApiKey(string? token) =>
            token != null && token.StartsWith(ApiKeyPrefix, StringComparison.Ordinal);

        /// <summary>
        /// Sets the credential on the headers, replacing any credential already there.
        /// </summary>
        public static void Apply(HttpRequestHeaders headers, string token)
        {
            headers.Authorization = null;
            headers.Remove(ApiKeyHeader);

            if (IsApiKey(token))
                headers.TryAddWithoutValidation(ApiKeyHeader, token);
            else
                headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            headers.Remove(ClientCapabilitiesHeader);
            headers.TryAddWithoutValidation(ClientCapabilitiesHeader, UsdtCashWalletCapability);
        }

        public static bool HasCredential(HttpRequestHeaders headers) =>
            headers.Authorization != null || headers.Contains(ApiKeyHeader);

        /// <summary>
        /// Flash subscriptions authenticate with Kratos sessions only, so API keys
        /// cannot open a WebSocket and the plugin falls back to polling.
        /// </summary>
        public static bool SupportsWebSocket(string? token) => !IsApiKey(token);
    }
}
