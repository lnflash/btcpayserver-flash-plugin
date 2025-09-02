using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using BTCPayServer.Lightning;
using BTCPayServer.Payments.Lightning;
using System.Threading;
using System.Text;
using LNURL;
using System.Linq;
using System.Text.RegularExpressions;

namespace BTCPayServer.Plugins.Flash.Models
{
    public class LnurlHandler
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;

        public LnurlHandler(ILogger logger)
        {
            _logger = logger;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public class LnurlPayResponse
        {
            [JsonProperty("callback")]
            public string Callback { get; set; }

            [JsonProperty("maxSendable")]
            public long MaxSendable { get; set; }

            [JsonProperty("minSendable")]
            public long MinSendable { get; set; }

            [JsonProperty("metadata")]
            public string Metadata { get; set; }

            [JsonProperty("tag")]
            public string Tag { get; set; }
        }

        public class LnurlErrorResponse
        {
            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("reason")]
            public string Reason { get; set; }
        }

        public class LnurlPayCallbackResponse
        {
            [JsonProperty("pr")]
            public string PaymentRequest { get; set; }

            [JsonProperty("routes")]
            public object[] Routes { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }

            [JsonProperty("successAction")]
            public object SuccessAction { get; set; }
        }

        /// <summary>
        /// Validates if a string is a valid LNURL or Lightning address
        /// </summary>
        public async Task<(bool isLnurl, string error)> ValidateLnurl(string lnurlOrAddress, CancellationToken cancellation = default)
        {
            try
            {
                // Check if it's a Lightning Address
                if (IsLightningAddress(lnurlOrAddress))
                {
                    _logger.LogInformation($"Detected Lightning Address: {lnurlOrAddress}");
                    return (true, null);
                }

                // Check if it's a bech32 encoded LNURL
                if (lnurlOrAddress.StartsWith("lnurl", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Detected bech32 encoded LNURL");
                    return (true, null);
                }

                // Check if it's a lightning: URI
                if (lnurlOrAddress.StartsWith("lightning:", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Detected lightning: URI scheme");
                    return (true, null);
                }

                // Check if it's a direct HTTP LNURL
                if ((lnurlOrAddress.StartsWith("http://") || lnurlOrAddress.StartsWith("https://")) &&
                    (lnurlOrAddress.Contains("lnurl") ||
                     lnurlOrAddress.Contains("/.well-known/lnurlp/") ||
                     lnurlOrAddress.Contains("/lnurlp/")))
                {
                    _logger.LogInformation($"Detected LNURL HTTP endpoint: {lnurlOrAddress}");
                    return (true, null);
                }

                return (false, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating LNURL");
                return (false, $"Error validating LNURL: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves a LNURL or Lightning address into a BOLT11 invoice
        /// </summary>
        public async Task<(string bolt11, string error)> ResolveLnurlPayment(
            string lnurlOrAddress,
            long amountSat,
            string memo = null,
            CancellationToken cancellation = default)
        {
            try
            {
                // Convert to lowercase to avoid case sensitivity issues
                lnurlOrAddress = lnurlOrAddress?.ToLowerInvariant();

                _logger.LogInformation($"Resolving LNURL payment for {lnurlOrAddress} with amount {amountSat} sats");

                Uri httpEndpoint = null;

                // Handle Lightning Address
                if (IsLightningAddress(lnurlOrAddress))
                {
                    try
                    {
                        string[] parts = lnurlOrAddress.Split('@');
                        if (parts.Length != 2)
                        {
                            return (null, "Invalid Lightning Address format");
                        }

                        string username = parts[0];
                        string domain = parts[1];

                        // Construct the LNURL endpoint URL
                        httpEndpoint = new Uri($"https://{domain}/.well-known/lnurlp/{username}");
                        _logger.LogInformation($"Constructed LNURL endpoint from Lightning Address: {httpEndpoint}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error converting Lightning Address to LNURL");
                        return (null, $"Error resolving Lightning Address: {ex.Message}");
                    }
                }
                // Handle bech32 encoded LNURL or lightning: URI
                else if (lnurlOrAddress.StartsWith("lnurl", StringComparison.OrdinalIgnoreCase) ||
                         lnurlOrAddress.StartsWith("lightning:", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // Extract the LNURL from lightning: URI if needed
                        string lnurlData = lnurlOrAddress;
                        if (lnurlOrAddress.StartsWith("lightning:", StringComparison.OrdinalIgnoreCase))
                        {
                            var match = Regex.Match(lnurlOrAddress, @"lightning:(?:LNURL)?([A-Za-z0-9]+)");
                            if (match.Success)
                            {
                                lnurlData = "lnurl" + match.Groups[1].Value;
                            }
                            else
                            {
                                return (null, "Invalid lightning: URI format");
                            }
                        }

                        try
                        {
                            // Make sure it's lowercase for LNURL library
                            lnurlData = lnurlData.ToLowerInvariant();
                            _logger.LogInformation($"Using lowercase LNURL: {lnurlData}");

                            // Parse the LNURL with the LNURL library
                            var url = LNURL.LNURL.ExtractUriFromInternetIdentifier(lnurlData);
                            httpEndpoint = url;
                            _logger.LogInformation($"Decoded LNURL bech32 to: {httpEndpoint}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error decoding LNURL bech32");
                            return (null, $"Error decoding LNURL: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling LNURL or lightning URI");
                        return (null, $"Error decoding LNURL: {ex.Message}");
                    }
                }
                // Handle direct HTTP LNURL
                else if (lnurlOrAddress.StartsWith("http"))
                {
                    httpEndpoint = new Uri(lnurlOrAddress);
                }

                if (httpEndpoint == null)
                {
                    return (null, "Could not determine LNURL endpoint");
                }

                // Step 1: Fetch the LNURL pay parameters from the endpoint
                var response = await _httpClient.GetAsync(httpEndpoint, cancellation);
                if (!response.IsSuccessStatusCode)
                {
                    return (null, $"Failed to contact LNURL endpoint: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"LNURL endpoint response: {responseContent}");

                // Check if we got HTML instead of JSON (common error)
                if (responseContent.StartsWith("<") || responseContent.Contains("<html"))
                {
                    _logger.LogError("LNURL endpoint returned HTML instead of JSON");
                    return (null, "LNURL endpoint returned HTML instead of JSON. The service might be down or misconfigured.");
                }

                // Check for LNURL error response
                try
                {
                    var errorResponse = JsonConvert.DeserializeObject<LnurlErrorResponse>(responseContent);
                    if (errorResponse?.Status?.ToLowerInvariant() == "error")
                    {
                        return (null, $"LNURL error: {errorResponse.Reason}");
                    }
                }
                catch { /* Not an error response */ }

                // Parse the LNURL pay parameters
                var lnurlPayResponse = JsonConvert.DeserializeObject<LnurlPayResponse>(responseContent);

                if (lnurlPayResponse?.Tag != "payRequest")
                {
                    return (null, "Not a valid LNURL-pay endpoint");
                }

                // Check if amount is within range
                if (amountSat * 1000 < lnurlPayResponse.MinSendable || amountSat * 1000 > lnurlPayResponse.MaxSendable)
                {
                    return (null, $"Amount out of range. Min: {lnurlPayResponse.MinSendable / 1000} sats, Max: {lnurlPayResponse.MaxSendable / 1000} sats");
                }

                // Step 2: Request the invoice by calling the callback URL
                string callbackUrl = lnurlPayResponse.Callback;

                // Add parameters to callback URL
                var uriBuilder = new UriBuilder(callbackUrl);
                string query = uriBuilder.Query;
                if (query.StartsWith("?"))
                {
                    query = query.Substring(1);
                }

                var queryParams = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(query))
                {
                    queryParams.AddRange(query.Split('&'));
                }

                // Add the amount parameter (converted to millisatoshis)
                queryParams.Add($"amount={(amountSat * 1000)}");

                // Add comment if provided and metadata contains a comment placeholder
                if (!string.IsNullOrEmpty(memo) && lnurlPayResponse.Metadata?.Contains("\"text/plain\"") == true)
                {
                    queryParams.Add($"comment={Uri.EscapeDataString(memo)}");
                }

                uriBuilder.Query = string.Join("&", queryParams);
                var finalCallbackUrl = uriBuilder.Uri;

                _logger.LogInformation($"Requesting invoice from callback URL: {finalCallbackUrl}");

                // Call the callback URL to get the invoice
                var callbackResponse = await _httpClient.GetAsync(finalCallbackUrl, cancellation);
                if (!callbackResponse.IsSuccessStatusCode)
                {
                    return (null, $"Failed to get invoice from LNURL endpoint: {callbackResponse.StatusCode}");
                }

                var callbackContent = await callbackResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"LNURL callback response: {callbackContent}");

                // Check if we got HTML instead of JSON (common error)
                if (callbackContent.StartsWith("<") || callbackContent.Contains("<html"))
                {
                    _logger.LogError("LNURL callback returned HTML instead of JSON");
                    return (null, "LNURL callback returned HTML instead of JSON. The service might be down or misconfigured.");
                }

                // Check for error in callback response
                try
                {
                    var errorResponse = JsonConvert.DeserializeObject<LnurlErrorResponse>(callbackContent);
                    if (errorResponse?.Status?.ToLowerInvariant() == "error")
                    {
                        return (null, $"LNURL callback error: {errorResponse.Reason}");
                    }
                }
                catch { /* Not an error response */ }

                // Parse the callback response to get the BOLT11 invoice
                var payCallbackResponse = JsonConvert.DeserializeObject<LnurlPayCallbackResponse>(callbackContent);

                if (string.IsNullOrEmpty(payCallbackResponse?.PaymentRequest))
                {
                    return (null, "No payment request returned from LNURL endpoint");
                }

                _logger.LogInformation($"Successfully resolved LNURL to BOLT11: {payCallbackResponse.PaymentRequest.Substring(0, Math.Min(payCallbackResponse.PaymentRequest.Length, 30))}...");

                return (payCallbackResponse.PaymentRequest, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving LNURL payment");
                return (null, $"Error resolving LNURL payment: {ex.Message}");
            }
        }

        private bool IsLightningAddress(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            // Simple regex-like check for email format but without using regex
            return input.Contains("@") &&
                   !input.Contains(" ") &&
                   input.IndexOf("@") == input.LastIndexOf("@") &&
                   input.IndexOf("@") > 0 &&
                   input.IndexOf("@") < input.Length - 1;
        }
    }
}