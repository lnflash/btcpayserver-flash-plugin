#nullable enable
using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Lightning;
using BTCPayServer.Models;
using BTCPayServer.Plugins.Flash.Models;
using BTCPayServer.Services.Stores;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    [Route("plugins/flash")]
    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    public class FlashController : Controller
    {
        private readonly StoreRepository _storeRepository;
        private readonly ILogger<FlashController> _logger;

        private const string SettingsKey = "BTCPayServer.Plugins.Flash.Settings";

        public FlashController(
            StoreRepository storeRepository,
            ILogger<FlashController> logger)
        {
            _storeRepository = storeRepository ?? throw new ArgumentNullException(nameof(storeRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("settings/{storeId}")]
        public async Task<IActionResult> Settings(string storeId)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
                return NotFound();

            var settings = await GetSettings(storeId);
            return View("Settings", settings);
        }

        [HttpPost("settings/{storeId}")]
        public async Task<IActionResult> Settings(string storeId, FlashPluginSettings model)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
                return NotFound();

            if (!ModelState.IsValid)
                return View("Settings", model);

            await _storeRepository.UpdateSetting(storeId, SettingsKey, model);
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Severity = StatusMessageModel.StatusSeverity.Success,
                Message = "Flash Lightning settings updated successfully."
            });

            return RedirectToAction(nameof(Settings), new { storeId });
        }

        [HttpGet("test-connection/{storeId}")]
        public async Task<IActionResult> TestConnection(string storeId)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
                return NotFound();

            var settings = await GetSettings(storeId);
            if (!settings.IsConfigured)
            {
                return BadRequest("Flash Lightning is not configured properly.");
            }

            var result = new TestConnectionResult();

            try
            {
                var graphQLClient = new GraphQLHttpClient(
                    new GraphQLHttpClientOptions { EndPoint = new Uri(settings.ApiEndpoint ?? "https://api.flashapp.me/graphql") },
                    new NewtonsoftJsonSerializer());

                graphQLClient.HttpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.BearerToken);

                var query = new GraphQLRequest
                {
                    Query = @"
                    query {
                      wallet {
                        balance
                        currency
                      }
                    }"
                };

                var response = await graphQLClient.SendQueryAsync<WalletResponse>(query);

                if (response.Errors != null && response.Errors.Length > 0)
                {
                    result.Success = false;
                    result.Message = $"Error connecting to Flash API: {response.Errors[0].Message}";
                }
                else
                {
                    result.Success = true;
                    result.Balance = response.Data.wallet.balance;
                    result.Currency = response.Data.wallet.currency;
                    result.Message = $"Successfully connected to Flash. Balance: {response.Data.wallet.balance} sats";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection to Flash API");
                result.Success = false;
                result.Message = $"Error connecting to Flash API: {ex.Message}";
            }

            return Json(result);
        }

        private async Task<FlashPluginSettings> GetSettings(string storeId)
        {
            var settings = await _storeRepository.GetSettingAsync<FlashPluginSettings>(storeId, SettingsKey);
            return settings ?? new FlashPluginSettings();
        }

        private class WalletResponse
        {
            public WalletData wallet { get; set; } = new WalletData();

            public class WalletData
            {
                public long balance { get; set; }
                public string currency { get; set; } = string.Empty;
            }
        }
    }
}