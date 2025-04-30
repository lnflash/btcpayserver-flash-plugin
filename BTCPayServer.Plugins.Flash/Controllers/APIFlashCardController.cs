#nullable enable
using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.HostedServices;
using BTCPayServer.Plugins.Flash.HostedServices;
using BTCPayServer.Plugins.Flash.Services;
using BTCPayServer.Services;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    [Route("api/v1/flash-cards")]
    [ApiController]
    public class APIFlashCardController : ControllerBase
    {
        private readonly FlashCardRegistrationService _cardService;
        private readonly PullPaymentHostedService _pullPaymentService;
        private readonly StoreRepository _storeRepository;
        private readonly EventAggregator _eventAggregator;
        private readonly ILogger<APIFlashCardController> _logger;
        
        public APIFlashCardController(
            FlashCardRegistrationService cardService,
            PullPaymentHostedService pullPaymentService,
            StoreRepository storeRepository,
            EventAggregator eventAggregator,
            ILogger<APIFlashCardController> logger)
        {
            _cardService = cardService;
            _pullPaymentService = pullPaymentService;
            _storeRepository = storeRepository;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }
        
        public class RegisterCardRequest
        {
            public string CardUID { get; set; } = null!;
            public string? CardName { get; set; }
            public decimal? InitialBalance { get; set; }
        }
        
        public class CardTapRequest
        {
            public string CardUID { get; set; } = null!;
            public decimal Amount { get; set; }
            public string MerchantId { get; set; } = null!;
            public string? LocationId { get; set; }
        }
        
        [HttpPost("register")]
        [Authorize(AuthenticationSchemes = AuthenticationSchemes.GreenfieldAPIKeys)]
        public async Task<IActionResult> RegisterCard([FromBody] RegisterCardRequest request)
        {
            // Get the store ID from the API key
            var storeId = HttpContext.GetStoreData()?.Id;
            if (storeId == null)
                return NotFound();
                
            if (string.IsNullOrEmpty(request.CardUID))
                return BadRequest(new { error = "Card UID is required" });
                
            // Create a pull payment (placeholder for now)
            // This would be properly implemented in the full version
            var pullPaymentId = "placeholder-" + Guid.NewGuid().ToString();
            
            try
            {
                var registration = await _cardService.RegisterCard(
                    request.CardUID,
                    pullPaymentId,
                    storeId,
                    null, // No user ID for API registrations
                    request.CardName);
                    
                return Ok(new
                {
                    id = registration.Id,
                    cardUid = registration.CardUID,
                    cardName = registration.CardName,
                    createdAt = registration.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering Flash card");
                return StatusCode(500, new { error = "Error registering card" });
            }
        }
        
        [HttpPost("tap")]
        [AllowAnonymous] // Card tap requests don't require authentication
        public async Task<IActionResult> CardTap([FromBody] CardTapRequest request)
        {
            if (string.IsNullOrEmpty(request.CardUID))
                return BadRequest(new { error = "Card UID is required" });
                
            if (request.Amount <= 0)
                return BadRequest(new { error = "Amount must be greater than zero" });
                
            if (string.IsNullOrEmpty(request.MerchantId))
                return BadRequest(new { error = "Merchant ID is required" });
                
            // Process the card tap
            try
            {
                // First check if card is registered
                var card = await _cardService.GetCardRegistration(request.CardUID);
                if (card == null)
                    return NotFound(new { error = "Card not registered" });
                    
                // Dispatch a card tap event to be processed by the hosted service
                // The event aggregator's Publish method is void, so don't await it
                _eventAggregator.Publish(new CardTapEvent
                {
                    CardUid = request.CardUID,
                    Amount = request.Amount,
                    MerchantId = request.MerchantId,
                    LocationId = request.LocationId,
                    Timestamp = DateTimeOffset.UtcNow
                });
                
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Flash card tap");
                return StatusCode(500, new { error = "Error processing card tap" });
            }
        }
        
        [HttpGet("{cardUid}/balance")]
        [AllowAnonymous] // Balance checks don't require authentication
        public async Task<IActionResult> GetCardBalance(string cardUid)
        {
            if (string.IsNullOrEmpty(cardUid))
                return BadRequest(new { error = "Card UID is required" });
                
            try
            {
                // Check if card is registered
                var card = await _cardService.GetCardRegistration(cardUid);
                if (card == null)
                    return NotFound(new { error = "Card not registered" });
                    
                // Get the card's balance
                // In a full implementation, this would calculate the balance from the pull payment
                var transactions = await _cardService.GetTransactionsByCardId(card.Id);
                
                // Return the balance
                return Ok(new
                {
                    cardUid = card.CardUID,
                    cardName = card.CardName,
                    balance = 0, // Placeholder
                    currency = "SATS",
                    isBlocked = card.IsBlocked
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Flash card balance");
                return StatusCode(500, new { error = "Error getting card balance" });
            }
        }
    }
}