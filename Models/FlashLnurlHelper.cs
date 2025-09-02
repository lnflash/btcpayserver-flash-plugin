using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Models
{
    /// <summary>
    /// A high-level wrapper for LNURL handling in the Flash plugin.
    /// Provides LNURL detection, resolution, and payment capabilities.
    /// </summary>
    public class FlashLnurlHelper
    {
        private readonly ILogger _logger;
        private readonly LnurlHandler _handler;

        public FlashLnurlHelper(ILogger logger)
        {
            _logger = logger;
            _handler = new LnurlHandler(logger);
        }

        /// <summary>
        /// Checks if a destination is an LNURL or Lightning Address
        /// </summary>
        /// <param name="destination">The destination to check</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>A tuple with (isLnurl, errorMessage)</returns>
        public async Task<(bool isLnurl, string errorMessage)> CheckForLnurl(string destination, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(destination))
                return (false, null);

            try
            {
                // Convert to lowercase for consistent comparison
                string lowercaseDestination = destination.ToLowerInvariant();

                // Check for explicit LNURL prefix
                if (lowercaseDestination.StartsWith("lnurl", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, null);
                }

                // Check for lightning address format (contains @ symbol)
                if (lowercaseDestination.Contains('@'))
                {
                    return (true, null);
                }

                // For other cases, use the handler
                return await _handler.ValidateLnurl(lowercaseDestination, cancellation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for LNURL in destination");
                return (false, null);
            }
        }

        /// <summary>
        /// Resolves a LNURL or Lightning address into a BOLT11 invoice
        /// </summary>
        /// <param name="destination">The LNURL or Lightning address</param>
        /// <param name="amountSat">Amount in satoshis</param>
        /// <param name="memo">Optional memo/comment for the payment</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>A tuple with (bolt11Invoice, errorMessage)</returns>
        public async Task<(string bolt11, string errorMessage)> ResolveLnurlPayment(
            string destination,
            long amountSat,
            string memo = null,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation($"Resolving LNURL payment for {destination} with amount {amountSat} sats");

                // Convert to lowercase for consistent comparison and processing
                string lowercaseDestination = destination.ToLowerInvariant();
                _logger.LogInformation($"Using lowercase destination for consistency: {lowercaseDestination}");

                // Ensure the amount is set (Flash API may need this)
                if (amountSat <= 0)
                {
                    _logger.LogWarning("Amount must be greater than 0 for LNURL payments");
                    return (null, "Amount must be greater than 0 for LNURL payments");
                }

                // Log the original amount being used
                _logger.LogInformation($"[PAYMENT DEBUG] Using amount {amountSat} satoshis for LNURL resolution");

                // Use the LnurlHandler to resolve the payment
                var result = await _handler.ResolveLnurlPayment(lowercaseDestination, amountSat, memo, cancellation);

                if (result.bolt11 == null)
                {
                    _logger.LogWarning($"Failed to resolve LNURL payment: {result.error}");
                }
                else
                {
                    _logger.LogInformation($"Successfully resolved LNURL to BOLT11: {result.bolt11.Substring(0, Math.Min(result.bolt11.Length, 30))}...");

                    // Double check if the resolved BOLT11 has an amount
                    if (!result.bolt11.Contains("lnbc1") &&
                        !result.bolt11.Contains("lnbc2") &&
                        !result.bolt11.Contains("lnbc3") &&
                        !result.bolt11.Contains("lnbc4") &&
                        !result.bolt11.Contains("lnbc5") &&
                        !result.bolt11.Contains("lnbc6") &&
                        !result.bolt11.Contains("lnbc7") &&
                        !result.bolt11.Contains("lnbc8") &&
                        !result.bolt11.Contains("lnbc9"))
                    {
                        // Very basic check - if it starts with lnbc1... etc., it likely has an amount
                        // If not, it's probably a no-amount invoice
                        _logger.LogWarning($"[PAYMENT DEBUG] Resolved BOLT11 appears to be a no-amount invoice. Original amount was {amountSat} satoshis.");
                    }
                    else
                    {
                        _logger.LogInformation($"[PAYMENT DEBUG] Resolved BOLT11 appears to include an amount. Original amount was {amountSat} satoshis.");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving LNURL payment");
                return (null, $"Error resolving LNURL payment: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a PayResponse for LNURL errors when needed
        /// </summary>
        /// <param name="errorMessage">Optional custom error message</param>
        /// <returns>PayResponse with error</returns>
        public PayResponse CreateLnurlErrorResponse(string errorMessage = null)
        {
            var message = errorMessage ?? "Error processing LNURL payment";
            return new PayResponse(PayResult.Error, message);
        }

        /// <summary>
        /// Checks if a type is related to LNURL based on its name.
        /// </summary>
        /// <param name="typeName">The name of the type</param>
        /// <returns>True if the type appears to be LNURL-related</returns>
        public bool IsLnurlType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return false;

            return typeName.Contains("LNURL") ||
                   typeName.Contains("LnUrl") ||
                   typeName.Contains("Lightning");
        }
    }
}