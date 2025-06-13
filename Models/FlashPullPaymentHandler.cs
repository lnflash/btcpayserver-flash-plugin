using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Lightning;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Payouts;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Models
{
    /// <summary>
    /// Handles the integration with BTCPayServer Pull Payments for Flash
    /// </summary>
    public class FlashPullPaymentHandler
    {
        private readonly ILogger<FlashPullPaymentHandler> _logger;
        private readonly FlashLightningClient _flashClient;

        public FlashPullPaymentHandler(ILogger<FlashPullPaymentHandler> logger, FlashLightningClient flashClient = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _flashClient = flashClient;

            if (_flashClient == null)
            {
                _logger.LogWarning("FlashLightningClient was not provided to FlashPullPaymentHandler - Flash pull payments will not be available until configured");
            }
        }

        /// <summary>
        /// Processes an LNURL-withdraw request, creating an invoice via Flash
        /// </summary>
        /// <param name="lnurlWithdrawEndpoint">The LNURL-withdraw endpoint</param>
        /// <param name="amountSat">Amount in satoshis</param>
        /// <param name="storeName">Store name for descriptive metadata</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Tuple with (bolt11Invoice, errorMessage)</returns>
        public async Task<(string bolt11, string errorMessage)> ProcessLNURLWithdraw(
            string lnurlWithdrawEndpoint,
            long amountSat,
            string storeName = null,
            CancellationToken cancellation = default)
        {
            try
            {
                if (_flashClient == null)
                {
                    _logger.LogWarning("Flash Lightning client not configured - cannot process LNURL-withdraw");
                    return (null, "Flash Lightning client not configured");
                }

                _logger.LogInformation($"Processing LNURL-withdraw for endpoint: {lnurlWithdrawEndpoint} with amount {amountSat} sats");

                // Use the existing LnurlHandler to parse the withdraw endpoint
                var lnurlHelper = new Models.FlashLnurlHelper(_logger);
                var handler = new LnurlHandler(_logger);

                // First, get the withdraw parameters from the endpoint
                var (withdrawParams, error) = await GetWithdrawParameters(lnurlWithdrawEndpoint, cancellation);

                if (withdrawParams == null)
                {
                    return (null, error ?? "Failed to get LNURL-withdraw parameters");
                }

                // Check if amount is within allowed range
                if (amountSat * 1000 < withdrawParams.MinWithdrawable || amountSat * 1000 > withdrawParams.MaxWithdrawable)
                {
                    return (null, $"Amount out of range. Min: {withdrawParams.MinWithdrawable / 1000} sats, Max: {withdrawParams.MaxWithdrawable / 1000} sats");
                }

                // Create an enhanced description with store name
                var description = string.IsNullOrEmpty(storeName)
                    ? withdrawParams.DefaultDescription ?? "BTCPay Server Withdrawal"
                    : $"Withdrawal from {storeName}";

                // Create an invoice via Flash
                var invoice = await CreateInvoiceForWithdraw(
                    amountSat,
                    description,
                    cancellation);

                if (invoice == null || string.IsNullOrEmpty(invoice.BOLT11))
                {
                    return (null, "Failed to create invoice for withdraw");
                }

                // Return the bolt11 invoice
                _logger.LogInformation($"Successfully created invoice for LNURL-withdraw: {invoice.BOLT11.Substring(0, Math.Min(invoice.BOLT11.Length, 30))}...");

                return (invoice.BOLT11, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LNURL-withdraw");
                return (null, $"Error processing LNURL-withdraw: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a Lightning invoice using the Flash GraphQL API
        /// </summary>
        private async Task<LightningInvoice> CreateInvoiceForWithdraw(
            long amountSat,
            string description,
            CancellationToken cancellation)
        {
            try
            {
                // Create invoice directly with parameters
                var invoice = await _flashClient.CreateInvoice(
                    LightMoney.Satoshis(amountSat),
                    description,
                    TimeSpan.FromHours(24),
                    cancellation);

                _logger.LogInformation($"Created invoice for withdraw: {invoice.Id}, Amount: {amountSat} sats");

                return invoice;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating invoice for withdraw: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the withdrawal parameters from the LNURL-withdraw endpoint
        /// </summary>
        private async Task<(WithdrawParameters parameters, string error)> GetWithdrawParameters(
            string lnurlWithdrawEndpoint,
            CancellationToken cancellation)
        {
            try
            {
                // Use a simple HTTP client to avoid circular dependencies with LnurlHandler
                using var httpClient = new System.Net.Http.HttpClient();

                var response = await httpClient.GetAsync(lnurlWithdrawEndpoint, cancellation);
                if (!response.IsSuccessStatusCode)
                {
                    return (null, $"Failed to get LNURL-withdraw parameters: {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();

                // Parse the response
                var withdrawParams = Newtonsoft.Json.JsonConvert.DeserializeObject<WithdrawParameters>(content);

                if (withdrawParams == null || withdrawParams.Tag != "withdrawRequest")
                {
                    return (null, "Invalid LNURL-withdraw response");
                }

                return (withdrawParams, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting withdraw parameters");
                return (null, $"Error getting withdraw parameters: {ex.Message}");
            }
        }

        /// <summary>
        /// Represents LNURL-withdraw parameters
        /// </summary>
        public class WithdrawParameters
        {
            [Newtonsoft.Json.JsonProperty("tag")]
            public string Tag { get; set; }

            [Newtonsoft.Json.JsonProperty("callback")]
            public string Callback { get; set; }

            [Newtonsoft.Json.JsonProperty("k1")]
            public string K1 { get; set; }

            [Newtonsoft.Json.JsonProperty("minWithdrawable")]
            public long MinWithdrawable { get; set; }

            [Newtonsoft.Json.JsonProperty("maxWithdrawable")]
            public long MaxWithdrawable { get; set; }

            [Newtonsoft.Json.JsonProperty("defaultDescription")]
            public string DefaultDescription { get; set; }
        }
    }
}