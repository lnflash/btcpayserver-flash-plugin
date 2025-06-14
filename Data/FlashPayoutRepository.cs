#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Flash.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Flash.Data
{
    public class FlashPayoutRepository
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FlashPayoutRepository> _logger;

        public FlashPayoutRepository(
            IServiceScopeFactory scopeFactory,
            ILogger<FlashPayoutRepository> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<FlashPayout> CreatePayoutAsync(FlashPayout payout)
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<FlashPluginDbContext>();
            
            _logger.LogInformation("Creating Flash payout for store {StoreId}, amount: {Amount} sats", 
                payout.StoreId, payout.AmountSats);
            
            context.Add(payout);
            await context.SaveChangesAsync();
            
            return payout;
        }

        public async Task<FlashPayout?> GetPayoutAsync(string payoutId)
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<FlashPluginDbContext>();
            
            return await context.Set<FlashPayout>()
                .FirstOrDefaultAsync(p => p.Id == payoutId);
        }

        public async Task<List<FlashPayout>> GetPayoutsForStoreAsync(
            string storeId, 
            PayoutStatus? status = null,
            int skip = 0, 
            int take = 50)
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<FlashPluginDbContext>();
            
            var query = context.Set<FlashPayout>()
                .Where(p => p.StoreId == storeId);

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<PayoutDashboardStats> GetDashboardStatsAsync(string storeId)
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<FlashPluginDbContext>();
            
            var payouts = await context.Set<FlashPayout>()
                .Where(p => p.StoreId == storeId)
                .ToListAsync();

            var stats = new PayoutDashboardStats
            {
                TotalPayouts = payouts.Count,
                ActivePayouts = payouts.Count(p => p.Status == PayoutStatus.Pending || p.Status == PayoutStatus.Processing),
                CompletedPayouts = payouts.Count(p => p.Status == PayoutStatus.Completed),
                FailedPayouts = payouts.Count(p => p.Status == PayoutStatus.Failed),
                TotalAmountSats = payouts.Where(p => p.Status == PayoutStatus.Completed).Sum(p => p.AmountSats),
                UniqueBoltcards = payouts.Where(p => !string.IsNullOrEmpty(p.BoltcardId))
                    .Select(p => p.BoltcardId)
                    .Distinct()
                    .Count()
            };

            return stats;
        }

        public async Task<List<BoltcardStats>> GetBoltcardStatsAsync(string storeId, int topCount = 10)
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<FlashPluginDbContext>();
            
            var boltcardStats = await context.Set<FlashPayout>()
                .Where(p => p.StoreId == storeId && !string.IsNullOrEmpty(p.BoltcardId))
                .GroupBy(p => p.BoltcardId)
                .Select(g => new BoltcardStats
                {
                    BoltcardId = g.Key,
                    TotalPayouts = g.Count(),
                    TotalAmountSats = g.Sum(p => p.AmountSats),
                    LastUsed = g.Max(p => p.CreatedAt),
                    FirstUsed = g.Min(p => p.CreatedAt)
                })
                .OrderByDescending(b => b.TotalPayouts)
                .Take(topCount)
                .ToListAsync();

            return boltcardStats;
        }

        public async Task<FlashPayout> UpdatePayoutAsync(string payoutId, PayoutStatus status, string? paymentHash = null, string? errorMessage = null)
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<FlashPluginDbContext>();
            
            var payout = await context.Set<FlashPayout>()
                .FirstOrDefaultAsync(p => p.Id == payoutId);

            if (payout == null)
            {
                throw new InvalidOperationException($"Payout {payoutId} not found");
            }

            _logger.LogInformation("Updating Flash payout {PayoutId} status from {OldStatus} to {NewStatus}", 
                payoutId, payout.Status, status);

            payout.Status = status;
            
            if (!string.IsNullOrEmpty(paymentHash))
            {
                payout.PaymentHash = paymentHash;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                payout.ErrorMessage = errorMessage;
            }

            if (status == PayoutStatus.Completed)
            {
                payout.CompletedAt = DateTimeOffset.UtcNow;
            }

            payout.UpdatedAt = DateTimeOffset.UtcNow;

            await context.SaveChangesAsync();
            
            return payout;
        }

        public async Task<List<PayoutTimelineEntry>> GetPayoutTimelineAsync(string storeId, int days = 30)
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<FlashPluginDbContext>();
            
            var startDate = DateTimeOffset.UtcNow.AddDays(-days);
            
            var timeline = await context.Set<FlashPayout>()
                .Where(p => p.StoreId == storeId && p.CreatedAt >= startDate)
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new PayoutTimelineEntry
                {
                    Date = g.Key,
                    Count = g.Count(),
                    TotalAmountSats = g.Sum(p => p.AmountSats)
                })
                .OrderBy(t => t.Date)
                .ToListAsync();

            return timeline;
        }

        public async Task<bool> SetBoltcardIdAsync(string payoutId, string boltcardId)
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<FlashPluginDbContext>();
            
            var payout = await context.Set<FlashPayout>()
                .FirstOrDefaultAsync(p => p.Id == payoutId);

            if (payout == null)
            {
                return false;
            }

            _logger.LogInformation("Setting Boltcard ID {BoltcardId} for payout {PayoutId}", 
                boltcardId, payoutId);

            payout.BoltcardId = boltcardId;
            payout.UpdatedAt = DateTimeOffset.UtcNow;

            // Store Boltcard info in metadata
            var metadata = string.IsNullOrEmpty(payout.Metadata) 
                ? new Dictionary<string, object>() 
                : JsonConvert.DeserializeObject<Dictionary<string, object>>(payout.Metadata) ?? new Dictionary<string, object>();
            
            metadata["boltcard_collected_at"] = DateTimeOffset.UtcNow.ToString("O");
            payout.Metadata = JsonConvert.SerializeObject(metadata);

            await context.SaveChangesAsync();
            
            return true;
        }

        public async Task<List<FlashPayout>> GetRecentPayoutsWithBoltcardAsync(string storeId, int count = 10)
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<FlashPluginDbContext>();
            
            return await context.Set<FlashPayout>()
                .Where(p => p.StoreId == storeId && !string.IsNullOrEmpty(p.BoltcardId))
                .OrderByDescending(p => p.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task CleanupOldPayoutsAsync(string storeId, int daysToKeep = 90)
        {
            using var scope = _scopeFactory.CreateScope();
            await using var context = scope.ServiceProvider.GetRequiredService<FlashPluginDbContext>();
            
            var cutoffDate = DateTimeOffset.UtcNow.AddDays(-daysToKeep);
            
            var oldPayouts = await context.Set<FlashPayout>()
                .Where(p => p.StoreId == storeId && 
                           p.CreatedAt < cutoffDate && 
                           (p.Status == PayoutStatus.Completed || p.Status == PayoutStatus.Failed))
                .ToListAsync();

            if (oldPayouts.Any())
            {
                _logger.LogInformation("Cleaning up {Count} old payouts for store {StoreId}", 
                    oldPayouts.Count, storeId);
                
                context.RemoveRange(oldPayouts);
                await context.SaveChangesAsync();
            }
        }
    }
}