#nullable enable
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Service for handling Lightning payment operations
    /// </summary>
    public interface IFlashPaymentService
    {
        /// <summary>
        /// Pay a BOLT11 invoice
        /// </summary>
        Task<PayResponse> PayInvoiceAsync(
            string bolt11,
            CancellationToken cancellation = default);

        /// <summary>
        /// Pay a BOLT11 invoice with parameters
        /// </summary>
        Task<PayResponse> PayInvoiceAsync(
            string bolt11,
            PayInvoiceParams? payParams,
            CancellationToken cancellation = default);

        /// <summary>
        /// Pay using invoice parameters
        /// </summary>
        Task<PayResponse> PayInvoiceAsync(
            PayInvoiceParams invoice,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get payment status
        /// </summary>
        Task<LightningPayment> GetPaymentAsync(
            string paymentHash,
            CancellationToken cancellation = default);

        /// <summary>
        /// List payments
        /// </summary>
        Task<LightningPayment[]> ListPaymentsAsync(
            ListPaymentsParams? request = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Set amount for no-amount invoices (used in pull payments)
        /// </summary>
        void SetNoAmountInvoiceAmount(long amountSat);

        /// <summary>
        /// Process LNURL payment
        /// </summary>
        Task<(string bolt11, string? error)> ResolveLnurlPaymentAsync(
            string lnurlString,
            long amountSats,
            string? memo = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get BOLT11 invoice from LNURL for pull payments
        /// </summary>
        Task<(string bolt11, string paymentHash)> GetInvoiceFromLNURLAsync(
            object payoutData,
            object handler,
            object blob,
            object lnurlPayClaimDestination,
            CancellationToken cancellation = default);

        /// <summary>
        /// Process pull payment payout
        /// </summary>
        Task<PayoutData> ProcessPullPaymentPayoutAsync(
            string pullPaymentId,
            string payoutId,
            string bolt11,
            CancellationToken cancellation = default);
    }

    /// <summary>
    /// Payout data for pull payments
    /// </summary>
    public class PayoutData
    {
        public string? Id { get; set; }
        public string? PullPaymentId { get; set; }
        public string? Proof { get; set; }
        public PayoutState State { get; set; }
    }

    /// <summary>
    /// Payout state enumeration
    /// </summary>
    public enum PayoutState
    {
        Pending,
        Completed,
        Failed
    }
}