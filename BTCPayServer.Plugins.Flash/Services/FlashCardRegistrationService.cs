#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.HostedServices;
using BTCPayServer.Plugins.Flash.Data;
using BTCPayServer.Plugins.Flash.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Services
{
    public class FlashCardRegistrationService
    {
        private readonly FlashCardDbContextFactory _dbContextFactory;
        private readonly PullPaymentHostedService _pullPaymentService;
        private readonly ILogger<FlashCardRegistrationService> _logger;
        
        public FlashCardRegistrationService(
            FlashCardDbContextFactory dbContextFactory,
            PullPaymentHostedService pullPaymentService,
            ILogger<FlashCardRegistrationService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _pullPaymentService = pullPaymentService;
            _logger = logger;
        }
        
        public async Task<CardRegistration> RegisterCard(string cardUid, string pullPaymentId, string storeId, string? userId = null, string? cardName = null)
        {
            await using var context = _dbContextFactory.CreateContext();
            
            // Check if card already registered
            var existingCard = await context.CardRegistrations
                .FirstOrDefaultAsync(c => c.CardUID == cardUid);
                
            if (existingCard != null)
            {
                // Update card if it exists
                existingCard.PullPaymentId = pullPaymentId;
                existingCard.StoreId = storeId;
                existingCard.UserId = userId ?? existingCard.UserId;
                existingCard.CardName = cardName ?? existingCard.CardName;
                existingCard.Version++;
                
                await context.SaveChangesAsync();
                return existingCard;
            }
            
            // Create new card registration
            var cardRegistration = new CardRegistration
            {
                CardUID = cardUid,
                PullPaymentId = pullPaymentId,
                StoreId = storeId,
                UserId = userId,
                CardName = cardName ?? "Flash Card",
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow
            };
            
            await context.CardRegistrations.AddAsync(cardRegistration);
            await context.SaveChangesAsync();
            
            _logger.LogInformation("Registered new Flash card with UID {CardUid} for store {StoreId}", cardUid, storeId);
            
            return cardRegistration;
        }
        
        public async Task<CardRegistration?> GetCardRegistration(string cardUid)
        {
            await using var context = _dbContextFactory.CreateContext();
            return await context.CardRegistrations
                .FirstOrDefaultAsync(c => c.CardUID == cardUid);
        }
        
        public async Task<bool> CardHasAvailableFunds(string cardRegistrationId)
        {
            await using var context = _dbContextFactory.CreateContext();
            
            var card = await context.CardRegistrations
                .FirstOrDefaultAsync(c => c.Id == cardRegistrationId);
                
            if (card == null || card.IsBlocked)
                return false;
                
            // Get PullPayment to check available funds
            var pullPayment = await _pullPaymentService.GetPullPayment(card.PullPaymentId, false);
            if (pullPayment == null)
                return false;
                
            // Check if the pull payment has remaining funds
            // We need to calculate this ourselves since GetPayoutAmountForPullPayment isn't available
            
            // Calculate total amount of payouts for this pull payment
            // Logic based on similar code in PullPaymentHostedService.HandleCreatePayout
            var payoutsRaw = await context.Set<BTCPayServer.Data.PayoutData>()
                .Where(p => p.PullPaymentDataId == card.PullPaymentId)
                .Where(p => p.State != PayoutState.Cancelled)
                .ToListAsync();
                
            decimal totalPayout = 0;
            if (payoutsRaw != null && payoutsRaw.Any())
            {
                totalPayout = payoutsRaw.Sum(p => p.OriginalAmount);
            }
            
            // Use Limit directly from PullPaymentData
            var remaining = pullPayment.Limit - totalPayout;
            
            return remaining > 0;
        }
        
        public async Task<List<CardRegistration>> GetCardsByStoreId(string storeId)
        {
            await using var context = _dbContextFactory.CreateContext();
            return await context.CardRegistrations
                .Where(c => c.StoreId == storeId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
        
        public async Task<List<CardRegistration>> GetCardsByUserId(string userId)
        {
            await using var context = _dbContextFactory.CreateContext();
            return await context.CardRegistrations
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
        
        public async Task LogCardTransaction(string cardRegistrationId, decimal amount, CardTransactionType type, 
            string? payoutId = null, string? invoiceId = null)
        {
            await using var context = _dbContextFactory.CreateContext();
            
            var transaction = new CardTransaction
            {
                CardRegistrationId = cardRegistrationId,
                Amount = amount,
                Type = type,
                Status = CardTransactionStatus.Pending,
                PayoutId = payoutId,
                InvoiceId = invoiceId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            
            await context.CardTransactions.AddAsync(transaction);
            
            // Update card last used timestamp
            var card = await context.CardRegistrations.FindAsync(cardRegistrationId);
            if (card != null)
            {
                card.LastUsedAt = DateTimeOffset.UtcNow;
            }
            
            await context.SaveChangesAsync();
        }
        
        public async Task UpdateCardTransactionStatus(string transactionId, CardTransactionStatus status)
        {
            await using var context = _dbContextFactory.CreateContext();
            
            var transaction = await context.CardTransactions.FindAsync(transactionId);
            if (transaction != null)
            {
                transaction.Status = status;
                if (status == CardTransactionStatus.Completed)
                {
                    transaction.CompletedAt = DateTimeOffset.UtcNow;
                }
                
                await context.SaveChangesAsync();
            }
        }
        
        public async Task<List<CardTransaction>> GetTransactionsByCardId(string cardRegistrationId)
        {
            await using var context = _dbContextFactory.CreateContext();
            return await context.CardTransactions
                .Where(t => t.CardRegistrationId == cardRegistrationId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }
        
        public async Task BlockCard(string cardRegistrationId)
        {
            await using var context = _dbContextFactory.CreateContext();
            
            var card = await context.CardRegistrations.FindAsync(cardRegistrationId);
            if (card != null)
            {
                card.IsBlocked = true;
                await context.SaveChangesAsync();
                
                _logger.LogInformation("Blocked Flash card {CardId}", cardRegistrationId);
            }
        }
        
        public async Task UnblockCard(string cardRegistrationId)
        {
            await using var context = _dbContextFactory.CreateContext();
            
            var card = await context.CardRegistrations.FindAsync(cardRegistrationId);
            if (card != null)
            {
                card.IsBlocked = false;
                await context.SaveChangesAsync();
                
                _logger.LogInformation("Unblocked Flash card {CardId}", cardRegistrationId);
            }
        }
    }
}