#nullable enable
using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Plugins.Flash.Models;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    [Route("plugins/{storeId}/Flash")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    public class FlashMainController : Controller
    {
        private readonly ILogger<FlashMainController> _logger;
        private readonly FlashPlugin _plugin;
        private readonly StoreRepository _storeRepository;
        private const string SettingsKey = "BTCPayServer.Plugins.Flash.Settings";

        public FlashMainController(
            ILogger<FlashMainController> logger,
            FlashPlugin plugin,
            StoreRepository storeRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _storeRepository = storeRepository ?? throw new ArgumentNullException(nameof(storeRepository));
        }

        [HttpGet("")]
        [Authorize(Policy = Policies.CanViewStoreSettings)]
        public async Task<IActionResult> Index(string storeId)
        {
            // This is the main entry point, so we'll redirect to Dashboard
            return RedirectToAction(nameof(Dashboard), new { storeId });
        }

        [HttpGet("dashboard")]
        [Authorize(Policy = Policies.CanViewStoreSettings)]
        public async Task<IActionResult> Dashboard(string storeId)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
                return NotFound();

            var settings = await GetSettings(storeId);
            ViewData["IsConfigured"] = settings.IsConfigured;
            ViewData["PluginVersion"] = _plugin.Version.ToString();
            ViewData["StoreId"] = storeId;

            return View("Dashboard", storeId);
        }

        [HttpGet("settings")]
        [Authorize(Policy = Policies.CanModifyStoreSettings)]
        public async Task<IActionResult> Settings(string storeId)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
                return NotFound();

            var settings = await GetSettings(storeId);
            return View(settings);
        }

        [HttpPost("settings")]
        [Authorize(Policy = Policies.CanModifyStoreSettings)]
        public async Task<IActionResult> Settings(string storeId, FlashPluginSettings model)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await _storeRepository.UpdateSetting(storeId, SettingsKey, model);
            TempData[WellKnownTempData.SuccessMessage] = "Flash settings updated successfully.";

            return RedirectToAction(nameof(Settings), new { storeId });
        }

        private async Task<FlashPluginSettings> GetSettings(string storeId)
        {
            var settings = await _storeRepository.GetSettingAsync<FlashPluginSettings>(storeId, SettingsKey);
            return settings ?? new FlashPluginSettings();
        }
    }
}