#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using Microsoft.Extensions.Logging;
using BTCPayServer.Lightning;
using BTCPayServer.Services.Stores;
using BTCPayServer.Plugins.Flash.Models;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    [Route("plugins/{storeId}/Flash/boltcard")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [Authorize(Policy = Policies.CanViewStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    public class UIFlashBoltcardController : Controller
    {
        private readonly ILogger<UIFlashBoltcardController> _logger;
        private readonly StoreRepository _storeRepository;
        private readonly ILoggerFactory _loggerFactory;

        private const string SettingsKey = "BTCPayServer.Plugins.Flash.Settings";

        public UIFlashBoltcardController(
            ILogger<UIFlashBoltcardController> logger,
            StoreRepository storeRepository,
            ILoggerFactory loggerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storeRepository = storeRepository ?? throw new ArgumentNullException(nameof(storeRepository));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        [HttpGet("")]
        public async Task<IActionResult> BoltcardDashboard(string storeId)
        {
            try
            {
                var client = await GetFlashLightningClient(storeId);
                if (client == null)
                {
                    ViewBag.ErrorMessage = "Flash Lightning client not configured for this store.";
                    return View("Error");
                }

                var stats = client.GetBoltcardStats();
                var recentTransactions = client.GetBoltcardTransactions(20);

                var model = new BoltcardDashboardViewModel
                {
                    StoreId = storeId,
                    Stats = stats,
                    RecentTransactions = recentTransactions
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Boltcard dashboard for store {StoreId}", storeId);
                ViewBag.ErrorMessage = "Error loading Boltcard dashboard.";
                return View("Error");
            }
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> TransactionHistory(string storeId, int page = 1, int pageSize = 50)
        {
            try
            {
                var client = await GetFlashLightningClient(storeId);
                if (client == null)
                {
                    return BadRequest("Flash Lightning client not configured.");
                }

                var allTransactions = client.GetBoltcardTransactions(pageSize * 5); // Get more for pagination
                var pagedTransactions = allTransactions
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var model = new BoltcardTransactionHistoryViewModel
                {
                    StoreId = storeId,
                    Transactions = pagedTransactions,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalTransactions = allTransactions.Count
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Boltcard transaction history for store {StoreId}", storeId);
                return BadRequest("Error loading transaction history.");
            }
        }

        [HttpGet("card/{cardId}")]
        public async Task<IActionResult> CardDetails(string storeId, string cardId)
        {
            try
            {
                var client = await GetFlashLightningClient(storeId);
                if (client == null)
                {
                    return BadRequest("Flash Lightning client not configured.");
                }

                var cardTransactions = client.GetBoltcardTransactionsByCardId(cardId, 100);

                var model = new BoltcardDetailsViewModel
                {
                    StoreId = storeId,
                    CardId = cardId,
                    Transactions = cardTransactions,
                    TotalAmount = cardTransactions.Where(t => t.Status == "Paid").Sum(t => t.AmountSats),
                    SuccessfulTransactions = cardTransactions.Count(t => t.Status == "Paid"),
                    TotalTransactions = cardTransactions.Count
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Boltcard details for card {CardId} in store {StoreId}", cardId, storeId);
                return BadRequest("Error loading card details.");
            }
        }

        [HttpGet("api/stats")]
        public async Task<IActionResult> GetStats(string storeId)
        {
            try
            {
                var client = await GetFlashLightningClient(storeId);
                if (client == null)
                {
                    return BadRequest("Flash Lightning client not configured.");
                }

                var stats = client.GetBoltcardStats();
                return Json(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Boltcard stats for store {StoreId}", storeId);
                return BadRequest("Error getting stats.");
            }
        }

        private async Task<FlashLightningClient?> GetFlashLightningClient(string storeId)
        {
            try
            {
                var store = await _storeRepository.FindStore(storeId);
                if (store == null)
                {
                    _logger.LogWarning("Store {StoreId} not found", storeId);
                    return null;
                }

                var settings = await GetSettings(storeId);
                if (!settings.IsConfigured)
                {
                    _logger.LogWarning("Flash Lightning is not configured for store {StoreId}", storeId);
                    return null;
                }

                // Create the Flash Lightning client using the store settings
                var endpoint = new Uri(settings.ApiEndpoint ?? "https://api.flashapp.me/graphql");
                var clientLogger = _loggerFactory.CreateLogger<FlashLightningClient>();
                var client = new FlashLightningClient(settings.BearerToken!, endpoint, clientLogger);

                return client;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Flash Lightning client for store {StoreId}", storeId);
                return null;
            }
        }

        private async Task<FlashPluginSettings> GetSettings(string storeId)
        {
            var settings = await _storeRepository.GetSettingAsync<FlashPluginSettings>(storeId, SettingsKey);
            return settings ?? new FlashPluginSettings();
        }
    }

    public class BoltcardDashboardViewModel
    {
        public string StoreId { get; set; } = string.Empty;
        public FlashLightningClient.BoltcardStats Stats { get; set; } = new();
        public List<FlashLightningClient.BoltcardTransaction> RecentTransactions { get; set; } = new();
    }

    public class BoltcardTransactionHistoryViewModel
    {
        public string StoreId { get; set; } = string.Empty;
        public List<FlashLightningClient.BoltcardTransaction> Transactions { get; set; } = new();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalTransactions { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalTransactions / PageSize);
    }

    public class BoltcardDetailsViewModel
    {
        public string StoreId { get; set; } = string.Empty;
        public string CardId { get; set; } = string.Empty;
        public List<FlashLightningClient.BoltcardTransaction> Transactions { get; set; } = new();
        public long TotalAmount { get; set; }
        public int SuccessfulTransactions { get; set; }
        public int TotalTransactions { get; set; }
    }
}