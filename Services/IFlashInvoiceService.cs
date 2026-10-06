#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using NBitcoin;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Service for handling Lightning invoice operations
    /// </summary>
    public interface IFlashInvoiceService
    {
        /// <summary>
        /// Create a Lightning invoice
        /// </summary>
        Task<LightningInvoice> CreateInvoiceAsync(
            CreateInvoiceParams createParams,
            CancellationToken cancellation = default);

        /// <summary>
        /// Create a Lightning invoice with amount and description
        /// </summary>
        Task<LightningInvoice> CreateInvoiceAsync(
            LightMoney amount,
            string description,
            TimeSpan expiry,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get invoice status
        /// </summary>
        Task<LightningInvoice> GetInvoiceAsync(
            string invoiceId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get invoice status by payment hash
        /// </summary>
        Task<LightningInvoice> GetInvoiceAsync(
            uint256 invoiceId,
            CancellationToken cancellation = default);

        /// <summary>
        /// List invoices
        /// </summary>
        Task<LightningInvoice[]> ListInvoicesAsync(
            ListInvoicesParams? request = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Cancel an invoice
        /// </summary>
        Task CancelInvoiceAsync(
            string invoiceId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Track a pending invoice for payment monitoring
        /// </summary>
        void TrackPendingInvoice(LightningInvoice invoice);

        /// <summary>
        /// Record a payment for an invoice, but only once Flash confirms that exact invoice is paid
        /// </summary>
        Task MarkInvoiceAsPaidAsync(string paymentHash, long amountSats, string? boltcardId = null);

        /// <summary>
        /// Ask Flash for the status of an invoice this plugin created, by its payment request.
        /// Returns null when the invoice is unknown or Flash cannot report a status.
        /// </summary>
        Task<LightningInvoice?> GetVerifiedInvoiceAsync(string invoiceId, CancellationToken cancellation = default);
    }
}