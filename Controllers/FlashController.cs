using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Flash.Models;
using BTCPayServer.Plugins.Flash.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Controllers;

[Route("plugins/[controller]")]
[Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
public class FlashController : Controller
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly FlashClientProvider _flashClientProvider;
    private readonly ILogger<FlashController> _logger;

    public FlashController(
        ISettingsRepository settingsRepository,
        FlashClientProvider flashClientProvider,
        ILogger<FlashController> logger)
    {
        _settingsRepository = settingsRepository;
        _flashClientProvider = flashClientProvider;
        _logger = logger;
    }

    [HttpGet("{storeId}")]
    public async Task<IActionResult> UpdateFlashSettings(string storeId)
    {
        var storeBlob = await _settingsRepository.GetSettingAsync<FlashSettings>(storeId) ?? new FlashSettings { StoreId = storeId };
        
        // Don't show the token in the UI
        storeBlob.BearerToken = string.Empty;
        
        return View("~/Plugins/BTCPayServer.Plugins.Flash/UI/FlashSettings.cshtml", storeBlob);
    }

    [HttpPost("{storeId}")]
    public async Task<IActionResult> UpdateFlashSettings(string storeId, FlashSettings settings, string command)
    {
        settings.StoreId = storeId;

        // If the token field is empty, it means the user hasn't changed it, so we should keep the old value
        if (string.IsNullOrEmpty(settings.BearerToken))
        {
            var existingSettings = await _settingsRepository.GetSettingAsync<FlashSettings>(storeId);
            if (existingSettings != null)
            {
                settings.BearerToken = existingSettings.BearerToken;
            }
        }

        ModelState.Clear();
        TryValidateModel(settings);

        if (!ModelState.IsValid)
        {
            return View("~/Plugins/BTCPayServer.Plugins.Flash/UI/FlashSettings.cshtml", settings);
        }

        // Test connection if requested
        if (command == "test")
        {
            var client = _flashClientProvider.GetClient(settings);
            if (client == null)
            {
                ModelState.AddModelError(string.Empty, "Unable to create Flash client with the provided settings.");
                settings.BearerToken = string.Empty; // Clear for security
                return View("~/Plugins/BTCPayServer.Plugins.Flash/UI/FlashSettings.cshtml", settings);
            }

            try
            {
                // Test the connection by getting wallet info
                await client.GetInfo();
                TempData.SetStatusMessageModel(new StatusMessageModel
                {
                    Message = "Successfully connected to Flash!",
                    Severity = StatusMessageModel.StatusSeverity.Success
                });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error connecting to Flash: {ex.Message}");
                _logger.LogError(ex, "Error testing Flash connection");
                settings.BearerToken = string.Empty; // Clear for security
                return View("~/Plugins/BTCPayServer.Plugins.Flash/UI/FlashSettings.cshtml", settings);
            }
        }

        // Save settings
        await _settingsRepository.UpdateSetting(settings, storeId);
        
        TempData.SetStatusMessageModel(new StatusMessageModel
        {
            Message = "Flash settings updated successfully!",
            Severity = StatusMessageModel.StatusSeverity.Success
        });

        return RedirectToAction(nameof(UpdateFlashSettings), new { storeId });
    }

    // API endpoints for getting wallet info
    [HttpGet("api/{storeId}/wallet-info")]
    public async Task<IActionResult> GetWalletInfo(string storeId)
    {
        var settings = await _settingsRepository.GetSettingAsync<FlashSettings>(storeId);
        if (settings == null || string.IsNullOrEmpty(settings.BearerToken))
        {
            return BadRequest(new { error = "Flash not configured for this store" });
        }

        var client = _flashClientProvider.GetClient(settings);
        if (client == null)
        {
            return BadRequest(new { error = "Unable to create Flash client" });
        }

        try
        {
            var info = await client.GetInfo();
            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Flash wallet info");
            return BadRequest(new { error = $"Error getting wallet info: {ex.Message}" });
        }
    }

    // API endpoint for creating a connection string
    [HttpGet("{storeId}/connection-string")]
    [Authorize(Policy = Policies.CanModifyStoreSettings)]
    public async Task<IActionResult> GetConnectionString(string storeId)
    {
        var settings = await _settingsRepository.GetSettingAsync<FlashSettings>(storeId);
        if (settings == null || string.IsNullOrEmpty(settings.BearerToken))
        {
            return BadRequest(new { error = "Flash not configured for this store" });
        }

        var connectionString = $"type=flash;server={settings.ApiUrl};token={settings.BearerToken}";
        return Ok(new { connectionString });
    }
}