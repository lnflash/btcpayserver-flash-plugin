#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Service for handling GraphQL operations with the Flash API
    /// </summary>
    public interface IFlashGraphQLService
    {
        /// <summary>
        /// Send a GraphQL query
        /// </summary>
        Task<GraphQLResponse<T>> SendQueryAsync<T>(GraphQLRequest request, CancellationToken cancellation = default);

        /// <summary>
        /// Send a GraphQL mutation
        /// </summary>
        Task<GraphQLResponse<T>> SendMutationAsync<T>(GraphQLRequest request, CancellationToken cancellation = default);

        /// <summary>
        /// Get wallet information
        /// </summary>
        Task<WalletInfo?> GetWalletInfoAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Get current Bitcoin exchange rate
        /// </summary>
        Task<decimal> GetExchangeRateAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Decode a BOLT11 invoice
        /// </summary>
        Task<InvoiceDecodeResult> DecodeInvoiceAsync(string bolt11, CancellationToken cancellation = default);

        /// <summary>
        /// Get transaction history
        /// </summary>
        Task<List<TransactionInfo>> GetTransactionHistoryAsync(int limit = 20, CancellationToken cancellation = default);

        /// <summary>
        /// Get wallet balance
        /// </summary>
        Task<decimal?> GetWalletBalanceAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Get Lightning invoice status by payment hash
        /// </summary>
        Task<InvoiceStatusResult?> GetInvoiceStatusAsync(string paymentHash, CancellationToken cancellation = default);
    }

    /// <summary>
    /// Wallet information from Flash API
    /// </summary>
    public class WalletInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal? Balance { get; set; }
    }

    /// <summary>
    /// Invoice decode result
    /// </summary>
    public class InvoiceDecodeResult
    {
        public string? PaymentHash { get; set; }
        public string? PaymentRequest { get; set; }
        public long? AmountSats { get; set; }
        public long? Timestamp { get; set; }
        public long? Expiry { get; set; }
        public string? Network { get; set; }
        public bool HasAmount => AmountSats.HasValue && AmountSats.Value > 0;
    }

    /// <summary>
    /// Transaction information
    /// </summary>
    public class TransactionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string? Memo { get; set; }
        public string? Status { get; set; }
        public string Direction { get; set; } = string.Empty;
        public long? SettlementAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Lightning invoice status result
    /// </summary>
    public class InvoiceStatusResult
    {
        public string PaymentHash { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsPaid => Status?.ToUpperInvariant() == "PAID" || Status?.ToUpperInvariant() == "SUCCESS";
        public long? AmountReceived { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}