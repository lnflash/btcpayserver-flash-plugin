#nullable enable
using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Flash.Models;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using NBitcoin;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    [Route("plugins/{storeId}/Flash/Boltcard")]
    public class BoltcardTopupController : Controller
    {
        private readonly StoreRepository _storeRepository;
        private readonly ILogger<BoltcardTopupController> _logger;
        private readonly ILoggerFactory _loggerFactory;

        private const string SettingsKey = "BTCPayServer.Plugins.Flash.Settings";

        public BoltcardTopupController(
            StoreRepository storeRepository,
            ILogger<BoltcardTopupController> logger,
            ILoggerFactory loggerFactory)
        {
            _storeRepository = storeRepository ?? throw new ArgumentNullException(nameof(storeRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        // This is a public endpoint that doesn't require authentication
        [HttpGet("topup")]
        [AllowAnonymous]
        public async Task<IActionResult> Topup(string storeId)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
                return NotFound();

            var settings = await GetSettings(storeId);
            if (!settings.IsConfigured)
            {
                return BadRequest("Flash Lightning is not configured for this store.");
            }

            // Create a new model with default values
            var model = new BoltcardTopupViewModel
            {
                Amount = 5000, // Default 5000 sats
                Description = "Flashcard topup"
            };

            return View("Topup", model);
        }

        // Process the form submission for topup
        [HttpPost("topup")]
        [AllowAnonymous]
        public async Task<IActionResult> Topup(string storeId, BoltcardTopupViewModel model)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
                return NotFound();

            var settings = await GetSettings(storeId);
            if (!settings.IsConfigured)
            {
                return BadRequest("Flash Lightning is not configured for this store.");
            }

            if (!ModelState.IsValid)
            {
                return View("Topup", model);
            }

            // Create the client with store settings
            var endpoint = new Uri(settings.ApiEndpoint ?? "https://api.flashapp.me/graphql");
            // Create with the proper logger
            var clientLogger = _loggerFactory.CreateLogger<FlashLightningClient>();
            var client = new FlashLightningClient(settings.BearerToken!, endpoint, clientLogger);

            try
            {
                // Format memo as JSON array for Flash compatibility
                string memo = JsonConvert.SerializeObject(new[] { new[] { "text/plain", model.Description ?? "Flashcard topup" } });

                // Create the invoice - directly with proper parameters
                var invoice = await client.CreateInvoice(
                    new LightMoney(model.Amount!.Value, LightMoneyUnit.Satoshi),
                    memo,
                    TimeSpan.FromHours(1));

                // Store the invoice details in TempData for potential status checking
                var invoiceViewModel = new BoltcardInvoiceViewModel
                {
                    InvoiceId = invoice.Id,
                    PaymentRequest = invoice.BOLT11,
                    Amount = (long)(invoice.Amount?.MilliSatoshi / 1000 ?? model.Amount!.Value),
                    Description = model.Description ?? "Flashcard topup",
                    Status = "Unpaid",
                    ExpirySeconds = 3600
                };

                TempData["InvoiceData"] = JsonConvert.SerializeObject(invoiceViewModel);

                return RedirectToAction(nameof(Invoice), new { storeId, invoiceId = invoice.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating invoice for Boltcard topup");
                ModelState.AddModelError(string.Empty, $"Error creating invoice: {ex.Message}");
                return View("Topup", model);
            }
        }

        [HttpGet("invoice/{invoiceId}")]
        [AllowAnonymous]
        public async Task<IActionResult> Invoice(string storeId, string invoiceId)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
                return NotFound();

            var settings = await GetSettings(storeId);
            if (!settings.IsConfigured)
            {
                return BadRequest("Flash Lightning is not configured for this store.");
            }

            // Get the invoice data from TempData
            string? invoiceJson = TempData["InvoiceData"] as string;
            if (string.IsNullOrEmpty(invoiceJson))
            {
                return NotFound("Invoice not found. Please create a new invoice.");
            }

            // Keep the data in TempData for potential refreshes
            TempData.Keep("InvoiceData");

            var invoice = JsonConvert.DeserializeObject<BoltcardInvoiceViewModel>(invoiceJson);
            if (invoice == null || invoice.InvoiceId != invoiceId)
            {
                return NotFound("Invoice not found. Please create a new invoice.");
            }

            // Create the client to check status
            var endpoint = new Uri(settings.ApiEndpoint ?? "https://api.flashapp.me/graphql");
            var clientLogger = _loggerFactory.CreateLogger<FlashLightningClient>();
            var client = new FlashLightningClient(settings.BearerToken!, endpoint, clientLogger);

            try
            {
                // Check the invoice status
                var invoiceStatus = await client.GetInvoice(invoiceId);
                if (invoiceStatus != null)
                {
                    invoice.Status = invoiceStatus.Status.ToString();

                    // If paid, redirect to success
                    if (invoiceStatus.Status == LightningInvoiceStatus.Paid)
                    {
                        return RedirectToAction(nameof(Success), new { storeId, invoiceId });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking invoice status");
                // Continue showing the invoice even if status check fails
            }

            return View("Invoice", invoice);
        }

        [HttpGet("success/{invoiceId}")]
        [AllowAnonymous]
        public async Task<IActionResult> Success(string storeId, string invoiceId)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
                return NotFound();

            // Get the invoice data from TempData
            string? invoiceJson = TempData["InvoiceData"] as string;
            if (string.IsNullOrEmpty(invoiceJson))
            {
                var result = new BoltcardResultViewModel
                {
                    Success = true,
                    Message = "Payment completed successfully.",
                    InvoiceId = invoiceId
                };
                return View("Success", result);
            }

            var invoice = JsonConvert.DeserializeObject<BoltcardInvoiceViewModel>(invoiceJson);
            if (invoice == null || invoice.InvoiceId != invoiceId)
            {
                var result = new BoltcardResultViewModel
                {
                    Success = true,
                    Message = "Payment completed successfully.",
                    InvoiceId = invoiceId
                };
                return View("Success", result);
            }

            var resultModel = new BoltcardResultViewModel
            {
                Success = true,
                Message = "Your Boltcard has been topped up successfully.",
                InvoiceId = invoiceId,
                Amount = invoice.Amount
            };

            return View("Success", resultModel);
        }

        private async Task<FlashPluginSettings> GetSettings(string storeId)
        {
            var settings = await _storeRepository.GetSettingAsync<FlashPluginSettings>(storeId, SettingsKey);
            return settings ?? new FlashPluginSettings();
        }
    }
}