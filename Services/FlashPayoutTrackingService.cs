#nullable enable
using System;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Flash.Data;
using BTCPayServer.Plugins.Flash.Models;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Flash.Services
{
    public interface IFlashPayoutTrackingService
    {
        Task<FlashPayout> TrackPayoutAsync(string storeId, string pullPaymentId, long amountSats, string? memo = null);
        Task UpdatePayoutStatusAsync(string payoutId, PayoutStatus status, string? paymentHash = null, string? errorMessage = null);
        Task<bool> AssociateBoltcardAsync(string payoutId, string boltcardId);
        Task<string?> ExtractBoltcardIdFromLnurlAsync(string lnurlOrAddress);
    }

    public class FlashPayoutTrackingService : IFlashPayoutTrackingService
    {
        private readonly FlashPayoutRepository _payoutRepository;
        private readonly ILogger<FlashPayoutTrackingService> _logger;

        public FlashPayoutTrackingService(
            FlashPayoutRepository payoutRepository,
            ILogger<FlashPayoutTrackingService> logger)
        {
            _payoutRepository = payoutRepository;
            _logger = logger;
        }

        public async Task<FlashPayout> TrackPayoutAsync(
            string storeId, 
            string pullPaymentId, 
            long amountSats, 
            string? memo = null)
        {
            var payout = new FlashPayout
            {
                StoreId = storeId,
                PullPaymentId = pullPaymentId,
                AmountSats = amountSats,
                Memo = memo,
                Status = PayoutStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _logger.LogInformation(
                "Tracking new payout for store {StoreId}, pull payment {PullPaymentId}, amount: {Amount} sats",
                storeId, pullPaymentId, amountSats);

            return await _payoutRepository.CreatePayoutAsync(payout);
        }

        public async Task UpdatePayoutStatusAsync(
            string payoutId, 
            PayoutStatus status, 
            string? paymentHash = null, 
            string? errorMessage = null)
        {
            _logger.LogInformation(
                "Updating payout {PayoutId} status to {Status}",
                payoutId, status);

            await _payoutRepository.UpdatePayoutAsync(payoutId, status, paymentHash, errorMessage);
        }

        public async Task<bool> AssociateBoltcardAsync(string payoutId, string boltcardId)
        {
            if (string.IsNullOrEmpty(boltcardId))
            {
                return false;
            }

            _logger.LogInformation(
                "Associating Boltcard {BoltcardId} with payout {PayoutId}",
                boltcardId, payoutId);

            return await _payoutRepository.SetBoltcardIdAsync(payoutId, boltcardId);
        }

        public Task<string?> ExtractBoltcardIdFromLnurlAsync(string lnurlOrAddress)
        {
            try
            {
                // Extract Boltcard ID from LNURL or Lightning Address
                // This could be from:
                // 1. LNURL tag parameter
                // 2. Lightning Address metadata
                // 3. Custom header in the request
                
                if (string.IsNullOrEmpty(lnurlOrAddress))
                {
                    return Task.FromResult<string?>(null);
                }

                // Example patterns:
                // - LNURL with tag: lnurl1234567890?tag=boltcard_ABC123
                // - Lightning Address: boltcard_ABC123@flashapp.me
                
                // Simple extraction logic - can be enhanced based on actual format
                if (lnurlOrAddress.Contains("boltcard_"))
                {
                    var startIndex = lnurlOrAddress.IndexOf("boltcard_");
                    var endIndex = lnurlOrAddress.IndexOfAny(new[] { '@', '?', '&', ' ' }, startIndex);
                    
                    if (endIndex == -1)
                    {
                        endIndex = lnurlOrAddress.Length;
                    }

                    var boltcardId = lnurlOrAddress.Substring(startIndex, endIndex - startIndex);
                    return Task.FromResult<string?>(boltcardId);
                }

                // Check for tag parameter in LNURL
                if (lnurlOrAddress.Contains("tag="))
                {
                    var tagStart = lnurlOrAddress.IndexOf("tag=") + 4;
                    var tagEnd = lnurlOrAddress.IndexOfAny(new[] { '&', ' ' }, tagStart);
                    
                    if (tagEnd == -1)
                    {
                        tagEnd = lnurlOrAddress.Length;
                    }

                    var tag = lnurlOrAddress.Substring(tagStart, tagEnd - tagStart);
                    return Task.FromResult<string?>(tag);
                }

                return Task.FromResult<string?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting Boltcard ID from {LnurlOrAddress}", lnurlOrAddress);
                return Task.FromResult<string?>(null);
            }
        }
    }
}