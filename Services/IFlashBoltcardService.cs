#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Service for handling Boltcard operations
    /// </summary>
    public interface IFlashBoltcardService
    {
        /// <summary>
        /// Start enhanced tracking for a Boltcard payment
        /// </summary>
        Task StartEnhancedTrackingAsync(string paymentHash, long amountSats, string boltcardId);

        /// <summary>
        /// Extract Boltcard ID from memo/description
        /// </summary>
        string ExtractBoltcardId(string memo);

        /// <summary>
        /// Generate unique sequence for transaction correlation
        /// </summary>
        string GenerateUniqueSequence();

        /// <summary>
        /// Create enhanced memo with correlation identifiers
        /// </summary>
        string CreateEnhancedMemo(string originalMemo, string boltcardId, string sequence, long amountSats);

        /// <summary>
        /// Calculate amount tolerance for matching
        /// </summary>
        long CalculateAmountTolerance(long amountSats);

        /// <summary>
        /// Extract sequence number from transaction memo
        /// </summary>
        string ExtractSequenceFromMemo(string memo);

        /// <summary>
        /// Get all Boltcard transactions
        /// </summary>
        List<BoltcardTransaction> GetBoltcardTransactions(int limit = 50);

        /// <summary>
        /// Get Boltcard transactions for a specific card
        /// </summary>
        List<BoltcardTransaction> GetBoltcardTransactionsByCardId(string cardId, int limit = 20);

        /// <summary>
        /// Get Boltcard statistics
        /// </summary>
        BoltcardStats GetBoltcardStats();

        /// <summary>
        /// Check if payment detection is working properly
        /// </summary>
        Task<bool> CheckFlashTransactionHistoryAsync(string paymentHash, long expectedAmount);
    }

    // Note: BoltcardTransaction and BoltcardStats classes are defined in FlashBoltcardService.cs
}