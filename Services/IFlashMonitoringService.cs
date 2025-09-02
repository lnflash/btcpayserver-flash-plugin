#nullable enable
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BTCPayServer.Lightning;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Service for monitoring payment transactions and invoice status changes
    /// </summary>
    public interface IFlashMonitoringService : IDisposable
    {
        /// <summary>
        /// Start monitoring for invoice payments
        /// </summary>
        Task StartMonitoringAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Stop monitoring for invoice payments
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Wait for the next paid invoice notification
        /// </summary>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>The paid invoice</returns>
        Task<LightningInvoice> WaitInvoiceAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Enhanced tracking specifically for Boltcard payments
        /// </summary>
        /// <param name="paymentHash">Payment hash to track</param>
        /// <param name="amountSats">Expected amount in satoshis</param>
        /// <param name="boltcardId">Boltcard identifier</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>True if payment was detected</returns>
        Task<bool> EnhancedBoltcardTrackingAsync(
            string paymentHash, 
            long amountSats, 
            string boltcardId,
            CancellationToken cancellation = default);

        /// <summary>
        /// Get the channel reader for paid invoice notifications
        /// </summary>
        ChannelReader<LightningInvoice> GetInvoiceReader();

        /// <summary>
        /// Check if monitoring is currently active
        /// </summary>
        bool IsMonitoring { get; }

        /// <summary>
        /// Event raised when a payment is detected
        /// </summary>
        event EventHandler<PaymentDetectedEventArgs>? PaymentDetected;
    }

    /// <summary>
    /// Event args for payment detection events
    /// </summary>
    public class PaymentDetectedEventArgs : EventArgs
    {
        public string PaymentHash { get; }
        public long AmountSats { get; }
        public string? BoltcardId { get; }
        public DateTime DetectedAt { get; }
        public string? TransactionId { get; }

        public PaymentDetectedEventArgs(
            string paymentHash, 
            long amountSats, 
            string? boltcardId = null,
            string? transactionId = null)
        {
            PaymentHash = paymentHash;
            AmountSats = amountSats;
            BoltcardId = boltcardId;
            TransactionId = transactionId;
            DetectedAt = DateTime.UtcNow;
        }
    }
}