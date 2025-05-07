using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.Flash.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    [Route("~/plugins/flash-connection-test")]
    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    public class UIFlashConnectionTestController : Controller
    {
        private readonly FlashConnectionTestService _connectionTestService;
        private readonly ILogger<UIFlashConnectionTestController> _logger;

        public UIFlashConnectionTestController(
            FlashConnectionTestService connectionTestService,
            ILogger<UIFlashConnectionTestController> logger)
        {
            _connectionTestService = connectionTestService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        public class ConnectionTestViewModel
        {
            public string AuthToken { get; set; } = string.Empty;
            public string ApiEndpoint { get; set; } = "https://api.flashapp.me/graphql";
            public string? WalletId { get; set; }
            public FlashConnectionTestService.ConnectionTestResult? TestResult { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> TestConnection(ConnectionTestViewModel model)
        {
            if (string.IsNullOrEmpty(model.AuthToken))
            {
                ModelState.AddModelError(nameof(model.AuthToken), "Authorization Token is required");
                return View("Index", model);
            }

            if (!Uri.TryCreate(model.ApiEndpoint, UriKind.Absolute, out var endpoint))
            {
                ModelState.AddModelError(nameof(model.ApiEndpoint), "Invalid API Endpoint URL");
                return View("Index", model);
            }

            try
            {
                var result = await _connectionTestService.TestConnection(model.AuthToken, endpoint, model.WalletId);
                model.TestResult = result;
                return View("Index", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Flash connection");
                ModelState.AddModelError(string.Empty, $"Error: {ex.Message}");
                return View("Index", model);
            }
        }
    }
}