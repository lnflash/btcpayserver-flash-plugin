#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BTCPayServer.Lightning;

using System.Collections.Generic;
using static BTCPayServer.Plugins.Flash.FlashLightningClient; // For BoltcardTransaction

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Implementation of transaction service for Flash
    /// </summary>
    public class FlashTransactionService : IFlashTransactionService
    {
        // Transaction tracking for Boltcard payments
        private static readonly Dictionary<string, BoltcardTransaction> _boltcardTransactions = new();
        private static readonly Dictionary<string, string> _transactionSequences = new();
        private static readonly object _sequenceLock = new();
        
        // Balance tracking
        private decimal? _lastKnownBalance;
        private DateTime _lastBalanceCheck = DateTime.MinValue;
        private readonly IFlashGraphQLService _graphQLService;
        private readonly IFlashBoltcardService _boltcardService;
        private readonly ILogger<FlashTransactionService> _logger;

        public FlashTransactionService(
            IFlashGraphQLService graphQLService,
            IFlashBoltcardService boltcardService,
            ILogger<FlashTransactionService> logger)
        {
            _graphQLService = graphQLService ?? throw new ArgumentNullException(nameof(graphQLService));
            _boltcardService = boltcardService ?? throw new ArgumentNullException(nameof(boltcardService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TransactionInfo?> CheckTransactionHistoryAsync(
            string paymentHash,
            long? expectedAmount = null,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("Checking transaction history for payment hash: {PaymentHash}", paymentHash);

                // Get recent transaction history
                var transactions = await _graphQLService.GetTransactionHistoryAsync(100, cancellation);

                // Look for matching transaction
                var matchingTransaction = transactions.FirstOrDefault(t =>
                    t.Id == paymentHash ||
                    (t.Memo != null && t.Memo.Contains(paymentHash)));

                if (matchingTransaction != null)
                {
                    _logger.LogInformation("Found matching transaction: {TransactionId}, Status: {Status}, Amount: {Amount}",
                        matchingTransaction.Id, matchingTransaction.Status, matchingTransaction.SettlementAmount);

                    // If expected amount is provided, verify it matches
                    if (expectedAmount.HasValue && matchingTransaction.SettlementAmount.HasValue)
                    {
                        var actualAmount = Math.Abs(matchingTransaction.SettlementAmount.Value);
                        var tolerance = expectedAmount.Value * 0.02m; // 2% tolerance
                        
                        if (Math.Abs(actualAmount - expectedAmount.Value) > tolerance)
                        {
                            _logger.LogWarning("Transaction amount mismatch. Expected: {Expected}, Actual: {Actual}",
                                expectedAmount.Value, actualAmount);
                            return null;
                        }
                    }

                    return matchingTransaction;
                }

                _logger.LogDebug("No matching transaction found for payment hash: {PaymentHash}", paymentHash);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking transaction history for payment hash: {PaymentHash}", paymentHash);
                throw;
            }
        }

        public async Task<TransactionInfo[]> GetRecentIncomingTransactionsAsync(
            int sinceMinutes = 5,
            CancellationToken cancellation = default)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-sinceMinutes);
                _logger.LogInformation("Getting incoming transactions since: {CutoffTime}", cutoffTime);

                var transactions = await _graphQLService.GetTransactionHistoryAsync(50, cancellation);

                var incomingTransactions = transactions
                    .Where(t => t.Direction?.ToLowerInvariant() == "receive" &&
                                t.CreatedAt >= cutoffTime &&
                                t.Status?.ToLowerInvariant() == "success")
                    .ToArray();

                _logger.LogInformation("Found {Count} recent incoming transactions", incomingTransactions.Length);
                return incomingTransactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent incoming transactions");
                throw;
            }
        }

        public async Task<(bool increased, decimal newBalance)> CheckBalanceIncreaseAsync(
            decimal previousBalance,
            CancellationToken cancellation = default)
        {
            try
            {
                var currentBalance = await GetWalletBalanceAsync(cancellation);
                var increased = currentBalance > previousBalance;

                if (increased)
                {
                    _logger.LogInformation("Balance increased from {Previous} to {Current}",
                        previousBalance, currentBalance);
                }
                else
                {
                    _logger.LogDebug("Balance unchanged or decreased. Previous: {Previous}, Current: {Current}",
                        previousBalance, currentBalance);
                }

                return (increased, currentBalance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking balance increase");
                throw;
            }
        }

        public async Task<decimal> GetWalletBalanceAsync(CancellationToken cancellation = default)
        {
            try
            {
                var walletInfo = await _graphQLService.GetWalletInfoAsync(cancellation);
                if (walletInfo == null)
                {
                    throw new InvalidOperationException("Could not retrieve wallet information");
                }

                _logger.LogDebug("Current wallet balance: {Balance} {Currency}",
                    walletInfo.Balance, walletInfo.Currency);

                return walletInfo.Balance ?? 0m;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wallet balance");
                throw;
            }
        }

        public async Task<TransactionInfo?> GetTransactionAsync(
            string transactionId,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("Getting transaction by ID: {TransactionId}", transactionId);

                // For now, search through transaction history
                // In the future, this could use a direct query if Flash API supports it
                var transactions = await _graphQLService.GetTransactionHistoryAsync(100, cancellation);
                
                var transaction = transactions.FirstOrDefault(t => t.Id == transactionId);

                if (transaction != null)
                {
                    _logger.LogInformation("Found transaction: {TransactionId}", transactionId);
                }
                else
                {
                    _logger.LogDebug("Transaction not found: {TransactionId}", transactionId);
                }

                return transaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction: {TransactionId}", transactionId);
                throw;
            }
        }

        public async Task<TransactionInfo?> FindTransactionByMemoAsync(
            string memoContent,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("Searching for transaction with memo containing: {MemoContent}", memoContent);

                var transactions = await _graphQLService.GetTransactionHistoryAsync(100, cancellation);
                
                var transaction = transactions.FirstOrDefault(t =>
                    t.Memo != null && t.Memo.Contains(memoContent, StringComparison.OrdinalIgnoreCase));

                if (transaction != null)
                {
                    _logger.LogInformation("Found transaction with matching memo: {TransactionId}", transaction.Id);
                }
                else
                {
                    _logger.LogDebug("No transaction found with memo containing: {MemoContent}", memoContent);
                }

                return transaction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding transaction by memo: {MemoContent}", memoContent);
                throw;
            }
        }

        public async Task<TransactionInfo[]> GetTransactionHistoryAsync(
            int limit = 50,
            int offset = 0,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogDebug("Getting transaction history. Limit: {Limit}, Offset: {Offset}", limit, offset);

                // Note: Flash API doesn't support offset, so we get max and slice
                var allTransactions = await _graphQLService.GetTransactionHistoryAsync(limit + offset, cancellation);
                
                var paginatedTransactions = allTransactions
                    .Skip(offset)
                    .Take(limit)
                    .ToArray();

                _logger.LogInformation("Retrieved {Count} transactions", paginatedTransactions.Length);
                return paginatedTransactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction history");
                throw;
            }
        }

        public async Task<TransactionInfo?> FindTransactionAsync(
            TransactionSearchCriteria criteria,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("Searching for transaction with criteria");

                var transactions = await _graphQLService.GetTransactionHistoryAsync(200, cancellation);

                var query = transactions.AsEnumerable();

                if (!string.IsNullOrEmpty(criteria.PaymentHash))
                {
                    query = query.Where(t => 
                        t.Id == criteria.PaymentHash || 
                        (t.Memo != null && t.Memo.Contains(criteria.PaymentHash)));
                }

                if (!string.IsNullOrEmpty(criteria.MemoContains))
                {
                    query = query.Where(t => 
                        t.Memo != null && 
                        t.Memo.Contains(criteria.MemoContains, StringComparison.OrdinalIgnoreCase));
                }

                if (criteria.AmountMin.HasValue || criteria.AmountMax.HasValue)
                {
                    query = query.Where(t => t.SettlementAmount.HasValue);

                    if (criteria.AmountMin.HasValue)
                    {
                        query = query.Where(t => Math.Abs(t.SettlementAmount!.Value) >= criteria.AmountMin.Value);
                    }

                    if (criteria.AmountMax.HasValue)
                    {
                        query = query.Where(t => Math.Abs(t.SettlementAmount!.Value) <= criteria.AmountMax.Value);
                    }
                }

                if (criteria.CreatedAfter.HasValue)
                {
                    query = query.Where(t => t.CreatedAt >= criteria.CreatedAfter.Value);
                }

                if (criteria.CreatedBefore.HasValue)
                {
                    query = query.Where(t => t.CreatedAt <= criteria.CreatedBefore.Value);
                }

                if (!string.IsNullOrEmpty(criteria.Status))
                {
                    query = query.Where(t => 
                        t.Status?.Equals(criteria.Status, StringComparison.OrdinalIgnoreCase) == true);
                }

                if (!string.IsNullOrEmpty(criteria.Direction))
                {
                    query = query.Where(t => 
                        t.Direction?.Equals(criteria.Direction, StringComparison.OrdinalIgnoreCase) == true);
                }

                var result = query.FirstOrDefault();

                if (result != null)
                {
                    _logger.LogInformation("Found matching transaction: {TransactionId}", result.Id);
                }
                else
                {
                    _logger.LogDebug("No transaction found matching criteria");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding transaction with criteria");
                throw;
            }
        }

        #region Boltcard Transaction Methods

        /// <summary>
        /// Enhanced Boltcard transaction tracking
        /// </summary>
        public async Task<bool> CheckBoltcardTransactionAsync(
            string paymentHash,
            long amountSats,
            string boltcardId,
            string? sequenceNumber = null,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("[BOLTCARD] Starting enhanced tracking for {PaymentHash}, amount: {Amount} sats, card: {BoltcardId}",
                    paymentHash, amountSats, boltcardId);

                // Store transaction info for correlation
                lock (_sequenceLock)
                {
                    _boltcardTransactions[paymentHash] = new BoltcardTransaction
                    {
                        InvoiceId = paymentHash,
                        AmountSats = amountSats,
                        BoltcardId = boltcardId,
                        UniqueSequence = sequenceNumber ?? string.Empty,
                        CreatedAt = DateTime.UtcNow,
                        Status = "Pending"
                    };

                    if (!string.IsNullOrEmpty(sequenceNumber))
                    {
                        _transactionSequences[sequenceNumber] = paymentHash;
                    }
                }

                // Multiple detection strategies
                var maxAttempts = 30;
                var checkInterval = TimeSpan.FromSeconds(1);

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    _logger.LogDebug("[BOLTCARD] Detection attempt {Attempt}/{Max} for {PaymentHash}",
                        attempt + 1, maxAttempts, paymentHash);

                    // Strategy 1: Check Flash transaction history
                    var found = await CheckFlashTransactionHistoryAsync(paymentHash, amountSats, sequenceNumber, cancellation);
                    if (found)
                    {
                        _logger.LogInformation("[BOLTCARD] Payment found via transaction history!");
                        return true;
                    }

                    // Strategy 2: Check for recent incoming transactions
                    found = await CheckForRecentIncomingTransactionAsync(amountSats, cancellation);
                    if (found)
                    {
                        _logger.LogInformation("[BOLTCARD] Payment found via recent transactions!");
                        return true;
                    }

                    // Strategy 3: Check account balance increase
                    found = await CheckAccountBalanceIncreaseAsync(amountSats, cancellation);
                    if (found)
                    {
                        _logger.LogInformation("[BOLTCARD] Payment detected via balance increase!");
                        return true;
                    }

                    await Task.Delay(checkInterval, cancellation);
                }

                _logger.LogWarning("[BOLTCARD] Payment not detected after {MaxAttempts} attempts", maxAttempts);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BOLTCARD] Error in enhanced tracking");
                throw;
            }
        }

        private async Task<bool> CheckFlashTransactionHistoryAsync(
            string paymentHash,
            long expectedAmount,
            string? sequenceNumber,
            CancellationToken cancellation)
        {
            try
            {
                var transactions = await _graphQLService.GetTransactionHistoryAsync(20, cancellation);
                var now = DateTime.UtcNow;

                foreach (var tx in transactions)
                {
                    if (tx.Status?.ToLowerInvariant() != "success")
                        continue;

                    var actualAmount = Math.Abs(tx.SettlementAmount ?? 0);
                    var tolerance = CalculateAmountTolerance(expectedAmount);

                    // Check for sequence match in memo
                    if (!string.IsNullOrEmpty(sequenceNumber) && tx.Memo != null)
                    {
                        var memoSequence = _boltcardService.ExtractSequenceFromMemo(tx.Memo);
                        if (memoSequence == sequenceNumber)
                        {
                            _logger.LogInformation("[BOLTCARD] PERFECT MATCH: Found exact sequence correlation: {Sequence}",
                                sequenceNumber);
                            return true;
                        }
                    }

                    // Amount and timing correlation
                    if (Math.Abs(actualAmount - expectedAmount) <= tolerance)
                    {
                        var timeDiff = (now - tx.CreatedAt).TotalSeconds;
                        if (timeDiff < 60)
                        {
                            _logger.LogInformation("[BOLTCARD] TIMING + AMOUNT MATCH: Found recent transaction within tolerance");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BOLTCARD] Error checking transaction history");
                return false;
            }
        }

        private async Task<bool> CheckForRecentIncomingTransactionAsync(
            long expectedAmountSats,
            CancellationToken cancellation)
        {
            try
            {
                var recent = await GetRecentIncomingTransactionsAsync(2, cancellation);
                var tolerance = CalculateAmountTolerance(expectedAmountSats);

                foreach (var tx in recent)
                {
                    var actualAmount = Math.Abs(tx.SettlementAmount ?? 0);
                    if (Math.Abs(actualAmount - expectedAmountSats) <= tolerance)
                    {
                        _logger.LogInformation("[BOLTCARD] Found matching recent transaction: {TxId}, Amount: {Amount}",
                            tx.Id, actualAmount);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BOLTCARD] Error checking recent transactions");
                return false;
            }
        }

        private async Task<bool> CheckAccountBalanceIncreaseAsync(
            long expectedAmountSats,
            CancellationToken cancellation)
        {
            try
            {
                var currentBalance = await GetWalletBalanceAsync(cancellation);
                
                if (_lastKnownBalance.HasValue && 
                    (DateTime.UtcNow - _lastBalanceCheck).TotalSeconds < 120)
                {
                    var balanceIncrease = currentBalance - _lastKnownBalance.Value;
                    
                    // Check if balance increased by approximately the expected amount
                    if (balanceIncrease > 0)
                    {
                        var expectedIncreaseCents = expectedAmountSats / 100m; // Rough conversion
                        var tolerance = expectedIncreaseCents * 0.1m; // 10% tolerance
                        
                        if (Math.Abs(balanceIncrease - expectedIncreaseCents) <= tolerance)
                        {
                            _logger.LogInformation("[BOLTCARD] Balance increased by expected amount: {Increase}",
                                balanceIncrease);
                            return true;
                        }
                    }
                }

                _lastKnownBalance = currentBalance;
                _lastBalanceCheck = DateTime.UtcNow;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BOLTCARD] Error checking balance increase");
                return false;
            }
        }


        private decimal CalculateAmountTolerance(long amountSats)
        {
            // For small amounts, use absolute tolerance
            if (amountSats < 1000)
            {
                return 10; // 10 sats tolerance
            }
            // For larger amounts, use percentage
            return amountSats * 0.02m; // 2% tolerance
        }

        #endregion
    }

}