#nullable enable
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    // Emergency fallback controller for direct access
    [Route("flash")]
    public class FlashRedirectController : Controller
    {
        [HttpGet("")]
        public IActionResult Index()
        {
            // Redirect to the main plugin UI
            return RedirectToAction("Index", "UIFlash", new { area = "plugins" });
        }
    }
}