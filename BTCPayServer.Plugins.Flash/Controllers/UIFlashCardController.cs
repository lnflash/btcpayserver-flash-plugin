#nullable enable
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.HostedServices;
using BTCPayServer.Models;
using BTCPayServer.Plugins.Flash.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    [Route("flash-cards")]
    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    public class UIFlashCardController : Controller
    {
        private readonly FlashCardRegistrationService _cardService;
        private readonly PullPaymentHostedService _pullPaymentService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UIFlashCardController> _logger;
        
        public UIFlashCardController(
            FlashCardRegistrationService cardService,
            PullPaymentHostedService pullPaymentService,
            UserManager<ApplicationUser> userManager,
            ILogger<UIFlashCardController> logger)
        {
            _cardService = cardService;
            _pullPaymentService = pullPaymentService;
            _userManager = userManager;
            _logger = logger;
        }
        
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var storeId = HttpContext.GetStoreData()?.Id;
            if (storeId == null)
                return NotFound();
                
            var cards = await _cardService.GetCardsByStoreId(storeId);
            
            // In the full implementation, we'd create a proper view model here
            return View("~/Views/FlashCard/Index.cshtml", cards);
        }
        
        [HttpGet("register")]
        public IActionResult Register()
        {
            // In the full implementation, we'd create a proper view model here
            return View("~/Views/FlashCard/Register.cshtml");
        }
        
        [HttpPost("register")]
        public async Task<IActionResult> Register(string cardUid, string cardName)
        {
            var storeId = HttpContext.GetStoreData()?.Id;
            if (storeId == null)
                return NotFound();
                
            // Validation
            if (string.IsNullOrEmpty(cardUid))
            {
                ModelState.AddModelError(nameof(cardUid), "Card UID is required");
                return View("~/Views/FlashCard/Register.cshtml");
            }
            
            // In a full implementation, we would create a pull payment here
            // For now, we'll just use a placeholder pull payment ID
            var pullPaymentId = "placeholder";
            
            // Register the card
            var userId = _userManager.GetUserId(User);
            var registration = await _cardService.RegisterCard(cardUid, pullPaymentId, storeId, userId, cardName);
            
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Severity = StatusMessageModel.StatusSeverity.Success,
                Message = "Flash card registered successfully"
            });
            
            return RedirectToAction(nameof(Index));
        }
        
        [HttpGet("details/{id}")]
        public async Task<IActionResult> Details(string id)
        {
            var storeId = HttpContext.GetStoreData()?.Id;
            if (storeId == null)
                return NotFound();
                
            var card = await _cardService.GetCardRegistration(id);
            if (card == null || card.StoreId != storeId)
                return NotFound();
                
            var transactions = await _cardService.GetTransactionsByCardId(card.Id);
            
            // In the full implementation, we'd create a proper view model here
            return View("~/Views/FlashCard/Details.cshtml", (card, transactions));
        }
        
        [HttpPost("block/{id}")]
        public async Task<IActionResult> Block(string id)
        {
            var storeId = HttpContext.GetStoreData()?.Id;
            if (storeId == null)
                return NotFound();
                
            var card = await _cardService.GetCardRegistration(id);
            if (card == null || card.StoreId != storeId)
                return NotFound();
                
            await _cardService.BlockCard(card.Id);
            
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Severity = StatusMessageModel.StatusSeverity.Success,
                Message = "Flash card blocked successfully"
            });
            
            return RedirectToAction(nameof(Details), new { id });
        }
        
        [HttpPost("unblock/{id}")]
        public async Task<IActionResult> Unblock(string id)
        {
            var storeId = HttpContext.GetStoreData()?.Id;
            if (storeId == null)
                return NotFound();
                
            var card = await _cardService.GetCardRegistration(id);
            if (card == null || card.StoreId != storeId)
                return NotFound();
                
            await _cardService.UnblockCard(card.Id);
            
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Severity = StatusMessageModel.StatusSeverity.Success,
                Message = "Flash card unblocked successfully"
            });
            
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}