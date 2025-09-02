#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Service for handling transaction history, balance queries, and transaction verification
    /// </summary>
    public interface IFlashTransactionService
    {
        /// <summary>
        /// Check Flash transaction history for a specific payment
        /// </summary>
        /// <param name="paymentHash">The payment hash to search for</param>
        /// <param name="expectedAmount">Expected amount in satoshis (optional)</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Transaction info if found, null otherwise</returns>
        Task<TransactionInfo?> CheckTransactionHistoryAsync(
            string paymentHash, 
            long? expectedAmount = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Check for recent incoming transactions
        /// </summary>
        /// <param name="sinceMinutes">Check transactions from the last N minutes</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Array of recent incoming transactions</returns>
        Task<TransactionInfo[]> GetRecentIncomingTransactionsAsync(
            int sinceMinutes = 5,
            CancellationToken cancellation = default);

        /// <summary>
        /// Check if account balance has increased
        /// </summary>
        /// <param name="previousBalance">Previous balance to compare against</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>True if balance increased, along with the new balance</returns>
        Task<(bool increased, decimal newBalance)> CheckBalanceIncreaseAsync(
            decimal previousBalance,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get current wallet balance
        /// </summary>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Current balance in wallet currency</returns>
        Task<decimal> GetWalletBalanceAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Get transaction by ID
        /// </summary>
        /// <param name="transactionId">Transaction ID</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Transaction info if found</returns>
        Task<TransactionInfo?> GetTransactionAsync(
            string transactionId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Search for a transaction by memo content
        /// </summary>
        /// <param name="memoContent">Content to search for in transaction memos</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Transaction info if found</returns>
        Task<TransactionInfo?> FindTransactionByMemoAsync(
            string memoContent,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get transaction history with pagination
        /// </summary>
        /// <param name="limit">Number of transactions to retrieve</param>
        /// <param name="offset">Offset for pagination</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Array of transactions</returns>
        Task<TransactionInfo[]> GetTransactionHistoryAsync(
            int limit = 50,
            int offset = 0,
            CancellationToken cancellation = default);

        /// <summary>
        /// Check if a specific transaction exists and matches criteria
        /// </summary>
        /// <param name="criteria">Transaction matching criteria</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Matching transaction if found</returns>
        Task<TransactionInfo?> FindTransactionAsync(
            TransactionSearchCriteria criteria,
            CancellationToken cancellation = default);

        /// <summary>
        /// Enhanced Boltcard transaction checking with multiple detection strategies
        /// </summary>
        /// <param name="paymentHash">Payment hash to track</param>
        /// <param name="amountSats">Expected amount in satoshis</param>
        /// <param name="boltcardId">Boltcard identifier</param>
        /// <param name="sequenceNumber">Optional sequence number for correlation</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>True if transaction was detected</returns>
        Task<bool> CheckBoltcardTransactionAsync(
            string paymentHash,
            long amountSats,
            string boltcardId,
            string? sequenceNumber = null,
            CancellationToken cancellation = default);
    }

    // TransactionInfo class is defined in IFlashGraphQLService.cs

    /// <summary>
    /// Criteria for searching transactions
    /// </summary>
    public class TransactionSearchCriteria
    {
        public string? PaymentHash { get; set; }
        public string? MemoContains { get; set; }
        public decimal? AmountMin { get; set; }
        public decimal? AmountMax { get; set; }
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
        public string? Status { get; set; }
        public string? Direction { get; set; }
    }
}