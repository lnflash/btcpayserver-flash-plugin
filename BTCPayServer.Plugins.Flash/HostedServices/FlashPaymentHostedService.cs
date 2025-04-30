#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Plugins.Flash.Services;
using BTCPayServer.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Reactive.Disposables;

namespace BTCPayServer.Plugins.Flash.HostedServices
{
    public class CardTapEvent
    {
        public string CardUid { get; set; } = null!;
        public decimal Amount { get; set; }
        public string MerchantId { get; set; } = null!;
        public string? LocationId { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }
    
    public class FlashPaymentHostedService : IHostedService
    {
        private readonly EventAggregator _eventAggregator;
        private readonly FlashCardRegistrationService _cardService;
        private readonly PullPaymentHostedService _pullPaymentService;
        private readonly SettingsRepository _settingsRepository;
        private readonly ILogger<FlashPaymentHostedService> _logger;
        private readonly CompositeDisposable _subscriptions = new();
        
        public FlashPaymentHostedService(
            EventAggregator eventAggregator,
            FlashCardRegistrationService cardService,
            PullPaymentHostedService pullPaymentService,
            SettingsRepository settingsRepository,
            ILogger<FlashPaymentHostedService> logger)
        {
            _eventAggregator = eventAggregator;
            _cardService = cardService;
            _pullPaymentService = pullPaymentService;
            _settingsRepository = settingsRepository;
            _logger = logger;
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Subscribe to invoice payment events
            _subscriptions.Add(_eventAggregator.Subscribe<InvoiceEvent>((sub, evt) => _ = HandleInvoiceEvent(evt)));
            
            // Subscribe to card tap events
            _subscriptions.Add(_eventAggregator.Subscribe<CardTapEvent>((sub, evt) => _ = HandleCardTapEvent(evt)));
            
            return Task.CompletedTask;
        }
        
        private async Task HandleInvoiceEvent(InvoiceEvent evt)
        {
            // If the invoice is associated with a card top-up, update the transaction status
            string? flashCardId = null;
            
            // Try to access metadata as JObject
            if (evt.Invoice.Metadata != null)
            {
                // Try to access as dictionary or JObject
                try
                {
                    var metadataObj = JObject.FromObject(evt.Invoice.Metadata);
                    flashCardId = metadataObj["flashCard"]?.ToString();
                }
                catch
                {
                    // Failed to convert metadata to JObject
                }
            }
            if (flashCardId != null)
            {
                if (evt.Name == InvoiceEvent.Expired)
                {
                    // Handle expired invoice
                    _logger.LogInformation("Flash card top-up invoice expired for card {CardId}", flashCardId);
                    // Update the transaction status
                }
                else if (evt.Name == InvoiceEvent.ReceivedPayment)
                {
                    // Handle received payment
                    _logger.LogInformation("Received payment for Flash card top-up for card {CardId}", flashCardId);
                    // Update the transaction status
                }
                else if (evt.Name == InvoiceEvent.PaidInFull)
                {
                    // Handle paid in full
                    _logger.LogInformation("Flash card top-up completed for card {CardId}", flashCardId);
                    // Update the transaction status
                }
            }
        }
        
        private async Task HandleCardTapEvent(CardTapEvent evt)
        {
            try
            {
                // Look up card registration
                var cardRegistration = await _cardService.GetCardRegistration(evt.CardUid);
                if (cardRegistration == null)
                {
                    _logger.LogWarning("Card tap rejected: Card {CardUid} not registered", evt.CardUid);
                    return;
                }
                
                // Check if card is blocked
                if (cardRegistration.IsBlocked)
                {
                    _logger.LogWarning("Card tap rejected: Card {CardUid} is blocked", evt.CardUid);
                    return;
                }
                
                // Check if card has available funds
                var hasAvailableFunds = await _cardService.CardHasAvailableFunds(cardRegistration.Id);
                if (!hasAvailableFunds)
                {
                    _logger.LogWarning("Card tap rejected: Insufficient funds for card {CardUid}", evt.CardUid);
                    return;
                }
                
                // Create transaction record
                await _cardService.LogCardTransaction(
                    cardRegistration.Id,
                    evt.Amount,
                    Data.Models.CardTransactionType.Payment,
                    null,
                    null);
                
                // Process the payment through BTCPay's pull payment system
                // This will be implemented in full version
                _logger.LogInformation("Processing Flash card payment of {Amount} for card {CardUid}", 
                    evt.Amount, evt.CardUid);
                
                // Update the card's last used timestamp
                cardRegistration.LastUsedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Flash card tap for card {CardUid}", evt.CardUid);
            }
        }
        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _subscriptions.Dispose();
            return Task.CompletedTask;
        }
    }
}