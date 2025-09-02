#nullable enable
using System;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    // Simple, standalone controller with a basic route
    [Route("plugins/flash")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    public class UIFlashController : Controller
    {
        private readonly ILogger<UIFlashController> _logger;
        private readonly FlashPlugin _plugin;

        public UIFlashController(
            ILogger<UIFlashController> logger,
            FlashPlugin plugin)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        // Basic route that should definitely work
        [HttpGet("")]
        [AllowAnonymous] // Allow access without authentication for testing
        public IActionResult Index()
        {
            ViewData["PluginVersion"] = _plugin.Version.ToString();
            return View();
        }

        [HttpGet("boltcard")]
        [AllowAnonymous]
        public IActionResult Boltcard()
        {
            ViewData["PluginVersion"] = _plugin.Version.ToString();
            return View();
        }
        
        [HttpGet("boltcard/process")]
        [AllowAnonymous]
        public IActionResult BoltcardProcess(long amount = 5000, string description = "Flashcard topup")
        {
            if (amount < 500 || amount > 100000)
            {
                return BadRequest("Amount must be between 500 and 100,000 satoshis");
            }
            
            ViewData["PluginVersion"] = _plugin.Version.ToString();
            ViewData["Amount"] = amount;
            ViewData["Description"] = description;
            
            // Generate a dummy Lightning invoice for demonstration
            // In production, this would come from the Flash API
            string dummyInvoice = "lnbc500n1pjvdywwpp5xck8wwqdyr6jrfwx5kgkqxklal0l35hzktf4a4gf9f8kx4rzrfsdq4xysyjzmr0da68ymmjwqhxjmnyqcqqgqqqqqpqqysgqs3wf2tz37ug322jcm7cmquh5ewh6jru4nc5zuycf89t9vy93v4kwhd9rqgrd8tr24hx2nwh5hwh4pe2g8hm7kkfkfs8jyp8fdy36xsqyajl8n";
            ViewData["Invoice"] = dummyInvoice;
            
            // In a real implementation, we would store this in a database and check its status
            ViewData["InvoiceId"] = "inv_" + DateTime.UtcNow.Ticks.ToString();
            
            return View("BoltcardInvoice");
        }
        
        [HttpGet("boltcard/success")]
        [AllowAnonymous]
        public IActionResult BoltcardSuccess(string invoiceId)
        {
            if (string.IsNullOrEmpty(invoiceId))
            {
                return BadRequest("Invoice ID is required");
            }
            
            ViewData["PluginVersion"] = _plugin.Version.ToString();
            ViewData["InvoiceId"] = invoiceId;
            
            return View("BoltcardSuccess");
        }
    }
}