#nullable enable
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Flash.Models;
using BTCPayServer.Plugins.Flash.Services;
using BTCPayServer.Services;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NBitcoin;
using LNURL;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    [Route("plugins/{storeId}/Flash")]
    public class FlashLNURLController : Controller
    {
        private readonly StoreRepository _storeRepository;
        private readonly ILogger<FlashLNURLController> _logger;
        private readonly ILoggerFactory _loggerFactory;

        private const string SettingsKey = "BTCPayServer.Plugins.Flash.Settings";

        public FlashLNURLController(
            StoreRepository storeRepository,
            ILogger<FlashLNURLController> logger,
            ILoggerFactory loggerFactory)
        {
            _storeRepository = storeRepository ?? throw new ArgumentNullException(nameof(storeRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        // LNURL-pay discovery endpoint
        [HttpGet(".well-known/lnurlp/{cardId}")]
        [AllowAnonymous]
        public async Task<IActionResult> LnurlPay(string storeId, string cardId)
        {
            try
            {
                var store = await _storeRepository.FindStore(storeId);
                if (store == null)
                {
                    return NotFound(new { status = "ERROR", reason = "Store not found" });
                }

                var settings = await GetSettings(storeId);
                if (!settings.IsConfigured)
                {
                    return BadRequest(new { status = "ERROR", reason = "Flash Lightning is not configured for this store" });
                }

                // Build the callback URL
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var callbackUrl = $"{baseUrl}/plugins/{storeId}/Flash/lnurlp/{cardId}/callback";

                // Create metadata as a proper JSON array string
                // According to LNURL spec, metadata should be a JSON string of array format: [["text/plain", "description"]]
                var metadataArray = new[] { new[] { "text/plain", $"Flashcard {cardId} top-up" } };
                var metadataJson = JsonConvert.SerializeObject(metadataArray);
                
                var response = new
                {
                    callback = callbackUrl,
                    maxSendable = 100000000L, // 1 BTC in millisats
                    minSendable = 1000L,      // 1 sat in millisats
                    metadata = metadataJson,  // This is already a JSON string, will not be double-serialized
                    tag = "payRequest"
                };

                _logger.LogInformation("LNURL-pay discovery for flashcard {CardId}: {Response}", cardId, JsonConvert.SerializeObject(response));

                // Return as Ok with explicit JSON content to ensure proper serialization
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling LNURL-pay discovery for flashcard {CardId}", cardId);
                return Ok(new { status = "ERROR", reason = "Internal server error" });
            }
        }

        // LNURL-pay callback endpoint
        [HttpGet("lnurlp/{cardId}/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> LnurlPayCallback(string storeId, string cardId, [FromQuery] long amount, [FromQuery] string? comment)
        {
            _logger.LogInformation("=== FlashLNURLController.LnurlPayCallback CALLED ===");
            _logger.LogInformation($"StoreId: {storeId}, CardId: {cardId}, Amount: {amount}, Comment: {comment}");
            
            try
            {
                var store = await _storeRepository.FindStore(storeId);
                if (store == null)
                {
                    _logger.LogWarning($"Store not found: {storeId}");
                    return Ok(new { status = "ERROR", reason = "Store not found" });
                }

                var settings = await GetSettings(storeId);
                if (!settings.IsConfigured)
                {
                    return Ok(new { status = "ERROR", reason = "Flash Lightning is not configured for this store" });
                }

                // Validate amount (amount is in millisats)
                if (amount < 1000L || amount > 100000000L)
                {
                    return Ok(new { status = "ERROR", reason = "Amount out of range" });
                }

                // Convert millisats to sats
                var amountSats = amount / 1000;

                // Prepare description with flashcard info
                var description = $"Flashcard {cardId} top-up";
                if (!string.IsNullOrEmpty(comment))
                {
                    description += $" - {comment}";
                }

                try
                {
                    var endpoint = new Uri(settings.ApiEndpoint ?? "https://api.flashapp.me/graphql");
                    using var graphQLService = new FlashGraphQLService(settings.BearerToken!, endpoint,
                        _loggerFactory.CreateLogger<FlashGraphQLService>(), null, _loggerFactory);
                    var invoiceService = new FlashInvoiceService(
                        graphQLService,
                        new FlashExchangeRateService(graphQLService, _loggerFactory.CreateLogger<FlashExchangeRateService>()),
                        new FlashBoltcardService(_loggerFactory.CreateLogger<FlashBoltcardService>()),
                        _loggerFactory.CreateLogger<FlashInvoiceService>());

                    var invoice = await invoiceService.CreateInvoiceAsync(
                        LightMoney.Satoshis(amountSats), description, TimeSpan.FromHours(1));

                    var response = new
                    {
                        pr = invoice.BOLT11,
                        routes = new object[0] // Empty routes array
                    };

                    _logger.LogInformation("LNURL-pay callback for flashcard {CardId}: Created invoice {InvoiceId} for {Amount} sats", 
                        cardId, invoice.Id, amountSats);

                    // Return as Ok with explicit JSON content to ensure proper serialization
                    return Ok(response);
                }
                catch (Exception invoiceEx)
                {
                    _logger.LogError(invoiceEx, "Failed to create invoice for flashcard {CardId}", cardId);
                    return Ok(new { status = "ERROR", reason = "Failed to create invoice: " + invoiceEx.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling LNURL-pay callback for flashcard {CardId}", cardId);
                return Ok(new { status = "ERROR", reason = "Failed to create invoice" });
            }
        }

        // Generate LNURL string for a flashcard
        [HttpGet("flashcard/{cardId}/lnurl")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFlashcardLnurl(string storeId, string cardId)
        {
            try
            {
                var store = await _storeRepository.FindStore(storeId);
                if (store == null)
                {
                    return NotFound(new { error = "Store not found" });
                }

                var settings = await GetSettings(storeId);
                if (!settings.IsConfigured)
                {
                    return BadRequest(new { error = "Flash Lightning is not configured for this store" });
                }

                // Build the LNURL endpoint URL
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var lnurlUrl = $"{baseUrl}/plugins/{storeId}/Flash/.well-known/lnurlp/{cardId}";

                // Convert to LNURL bech32 format
                var lnurlBech32 = LNURL.LNURL.EncodeUri(new Uri(lnurlUrl), "lnurl", true);

                _logger.LogInformation("Generated LNURL for flashcard {CardId}: {Lnurl}", cardId, lnurlBech32);

                return Ok(new 
                { 
                    cardId = cardId,
                    lnurl = lnurlBech32,
                    url = lnurlUrl,
                    qrCode = $"lightning:{lnurlBech32}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating LNURL for flashcard {CardId}", cardId);
                return Ok(new { error = "Failed to generate LNURL" });
            }
        }

        // Test endpoint for LNURL functionality
        [HttpGet("test-lnurl")]
        [AllowAnonymous]
        public async Task<IActionResult> TestLnurl(string storeId)
        {
            try
            {
                var store = await _storeRepository.FindStore(storeId);
                if (store == null)
                {
                    return NotFound("Store not found");
                }

                return View("TestLnurl");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test LNURL page");
                return BadRequest($"Error: {ex.Message}");
            }
        }

        private async Task<FlashPluginSettings> GetSettings(string storeId)
        {
            var settings = await _storeRepository.GetSettingAsync<FlashPluginSettings>(storeId, SettingsKey);
            return settings ?? new FlashPluginSettings();
        }
    }
}