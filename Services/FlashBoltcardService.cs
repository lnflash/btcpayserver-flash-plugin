#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Implementation of Boltcard service for Boltcard operations
    /// </summary>
    public class FlashBoltcardService : IFlashBoltcardService
    {
        private readonly ILogger<FlashBoltcardService> _logger;

        // Shared static Boltcard tracking across all instances
        private static readonly Dictionary<string, BoltcardTransaction> _boltcardTransactions = new Dictionary<string, BoltcardTransaction>();
        private static readonly Dictionary<string, string> _invoiceToBoltcardId = new Dictionary<string, string>();
        private static readonly object _boltcardTrackingLock = new object();

        // Enhanced correlation tracking
        private static readonly Dictionary<string, string> _transactionSequences = new Dictionary<string, string>();
        private static long _sequenceCounter = 0;
        private static readonly object _sequenceLock = new object();

        public FlashBoltcardService(ILogger<FlashBoltcardService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartEnhancedTrackingAsync(string paymentHash, long amountSats, string boltcardId)
        {
            try
            {
                _logger.LogInformation("Starting enhanced tracking for {PaymentHash}, amount: {AmountSats} sats, card: {BoltcardId}",
                    paymentHash, amountSats, boltcardId);

                // Create Boltcard transaction record
                var boltcardTransaction = new BoltcardTransaction
                {
                    InvoiceId = paymentHash,
                    BoltcardId = boltcardId,
                    AmountSats = amountSats,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Pending",
                    UniqueSequence = GenerateUniqueSequence(),
                    ExpectedAmountRange = CalculateAmountTolerance(amountSats)
                };

                // Store in tracking dictionaries with thread safety
                lock (_boltcardTrackingLock)
                {
                    _boltcardTransactions[paymentHash] = boltcardTransaction;
                    _invoiceToBoltcardId[paymentHash] = boltcardId;
                }

                // Store sequence mapping for correlation
                lock (_sequenceLock)
                {
                    _transactionSequences[boltcardTransaction.UniqueSequence] = paymentHash;
                }

                _logger.LogInformation("Created Boltcard transaction record for card {BoltcardId} with sequence {UniqueSequence}",
                    boltcardId, boltcardTransaction.UniqueSequence);

                // TODO: Implement actual tracking logic
                await Task.Delay(100); // Placeholder
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in enhanced Boltcard tracking for {PaymentHash}", paymentHash);
            }
        }

        public string ExtractBoltcardId(string memo)
        {
            try
            {
                if (string.IsNullOrEmpty(memo))
                    return "unknown";

                // Handle JSON format: [["text/plain","Boltcard Top-Up"]]
                if (memo.StartsWith("[") && memo.Contains("Boltcard"))
                {
                    // Parse JSON to extract the actual text
                    try
                    {
                        var jsonArray = Newtonsoft.Json.JsonConvert.DeserializeObject<string[][]>(memo);
                        if (jsonArray?.Length > 0 && jsonArray[0]?.Length > 1)
                        {
                            memo = jsonArray[0][1];
                        }
                    }
                    catch
                    {
                        // If JSON parsing fails, use original memo
                    }
                }

                // Look for Boltcard ID patterns in the memo
                var patterns = new[]
                {
                    @"Boltcard\s+(\w+)",
                    @"Card\s+ID[:\s]+(\w+)",
                    @"ID[:\s]+(\w+)",
                    @"(\w{8,16})" // Generic alphanumeric ID
                };

                foreach (var pattern in patterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(memo, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        var id = match.Groups[1].Value;
                        if (id.Length >= 4 && id.ToLowerInvariant() != "top" && id.ToLowerInvariant() != "up")
                        {
                            return id;
                        }
                    }
                }

                // Fallback: generate ID from memo hash
                var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(memo));
                return Convert.ToHexString(hash)[0..8].ToLowerInvariant();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting Boltcard ID from memo: {Message}", ex.Message);
                return "unknown";
            }
        }

        public string GenerateUniqueSequence()
        {
            lock (_sequenceLock)
            {
                _sequenceCounter++;
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return $"SEQ{_sequenceCounter:D6}T{timestamp}";
            }
        }

        public string CreateEnhancedMemo(string originalMemo, string boltcardId, string sequence, long amountSats)
        {
            // Create a correlation identifier that includes multiple unique elements
            var correlationId = $"BC{boltcardId}#{sequence}#{amountSats}";

            // Keep original memo but add correlation data
            return $"{originalMemo} [{correlationId}]";
        }

        public long CalculateAmountTolerance(long amountSats)
        {
            // Calculate tolerance based on amount size
            if (amountSats <= 1000) return 10; // ±10 sats for small amounts
            if (amountSats <= 10000) return 50; // ±50 sats for medium amounts  
            return Math.Max(100, amountSats / 100); // ±1% or minimum 100 sats for large amounts
        }

        public async Task<bool> CheckFlashTransactionHistoryAsync(string paymentHash, long expectedAmount)
        {
            try
            {
                _logger.LogInformation("Checking Flash transaction history for payment hash: {PaymentHash}, expected amount: {ExpectedAmount} sats",
                    paymentHash, expectedAmount);

                // TODO: Implement actual transaction history checking logic
                // This would typically involve querying the GraphQL service for recent transactions
                await Task.Delay(100); // Placeholder

                return false; // Placeholder return
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Flash transaction history: {Message}", ex.Message);
                return false;
            }
        }

        public List<BoltcardTransaction> GetBoltcardTransactions(int limit = 50)
        {
            try
            {
                lock (_boltcardTrackingLock)
                {
                    return _boltcardTransactions.Values
                        .OrderByDescending(t => t.CreatedAt)
                        .Take(limit)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Boltcard transactions");
                return new List<BoltcardTransaction>();
            }
        }

        public List<BoltcardTransaction> GetBoltcardTransactionsByCardId(string cardId, int limit = 20)
        {
            try
            {
                lock (_boltcardTrackingLock)
                {
                    return _boltcardTransactions.Values
                        .Where(t => t.BoltcardId.Equals(cardId, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(t => t.CreatedAt)
                        .Take(limit)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Boltcard transactions for card {CardId}", cardId);
                return new List<BoltcardTransaction>();
            }
        }

        public BoltcardStats GetBoltcardStats()
        {
            try
            {
                List<BoltcardTransaction> transactions;
                lock (_boltcardTrackingLock)
                {
                    transactions = _boltcardTransactions.Values.ToList();
                }

                var uniqueCards = transactions.Select(t => t.BoltcardId).Distinct().Count();
                var totalAmount = transactions.Where(t => t.Status == "Paid").Sum(t => t.AmountSats);
                var successRate = transactions.Count > 0 ?
                    (double)transactions.Count(t => t.Status == "Paid") / transactions.Count * 100 : 0;

                return new BoltcardStats
                {
                    TotalTransactions = transactions.Count,
                    UniqueCards = uniqueCards,
                    TotalAmountSats = totalAmount,
                    SuccessRate = successRate,
                    Last24Hours = transactions.Count(t => t.CreatedAt > DateTime.UtcNow.AddHours(-24))
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating Boltcard stats");
                return new BoltcardStats();
            }
        }
    }

    /// <summary>
    /// Boltcard transaction data class
    /// </summary>
    public class BoltcardTransaction
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string BoltcardId { get; set; } = string.Empty;
        public long AmountSats { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime? PaidAt { get; set; }
        public string? TransactionHash { get; set; }
        public string UniqueSequence { get; set; } = string.Empty;
        public long ExpectedAmountRange { get; set; } // For tolerance matching
    }

    /// <summary>
    /// Boltcard statistics class
    /// </summary>
    public class BoltcardStats
    {
        public int TotalTransactions { get; set; }
        public int UniqueCards { get; set; }
        public long TotalAmountSats { get; set; }
        public double SuccessRate { get; set; }
        public int Last24Hours { get; set; }
    }
}