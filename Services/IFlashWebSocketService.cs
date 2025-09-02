#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Flash.Models;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Interface for WebSocket communication with Flash API for real-time updates
    /// </summary>
    public interface IFlashWebSocketService : IDisposable
    {
        /// <summary>
        /// Connect to the Flash WebSocket endpoint
        /// </summary>
        Task ConnectAsync(string bearerToken, Uri websocketEndpoint, CancellationToken cancellation = default);

        /// <summary>
        /// Disconnect from the WebSocket
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Check if WebSocket is currently connected
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Current connection state
        /// </summary>
        WebSocketConnectionState ConnectionState { get; }
        
        /// <summary>
        /// Connection health metrics
        /// </summary>
        WebSocketHealthMetrics HealthMetrics { get; }

        /// <summary>
        /// Event raised when an invoice is updated
        /// </summary>
        event EventHandler<InvoiceUpdateEventArgs> InvoiceUpdated;

        /// <summary>
        /// Event raised when a payment is received
        /// </summary>
        event EventHandler<PaymentReceivedEventArgs> PaymentReceived;
        
        /// <summary>
        /// Event raised when connection state changes
        /// </summary>
        event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

        /// <summary>
        /// Subscribe to real-time updates for a specific invoice payment request
        /// </summary>
        Task SubscribeToInvoiceUpdatesAsync(string paymentRequest, CancellationToken cancellation = default);

        /// <summary>
        /// Unsubscribe from updates for a specific invoice
        /// </summary>
        Task UnsubscribeFromInvoiceUpdatesAsync(string invoiceId, CancellationToken cancellation = default);
        
        /// <summary>
        /// Create an invoice via WebSocket GraphQL mutation
        /// </summary>
        /// <param name="amountSats">Amount in satoshis</param>
        /// <param name="description">Invoice description/memo</param>
        /// <param name="cancellation">Cancellation token</param>
        /// <returns>Invoice data including payment request, payment hash, etc.</returns>
        Task<InvoiceCreationResult?> CreateInvoiceAsync(long amountSats, string description, CancellationToken cancellation = default);
    }
    
    /// <summary>
    /// Result from invoice creation via WebSocket
    /// </summary>
    public class InvoiceCreationResult
    {
        public string PaymentHash { get; set; } = string.Empty;
        public string PaymentRequest { get; set; } = string.Empty;
        public string? PaymentSecret { get; set; }
        public long Satoshis { get; set; }
        public string? ErrorMessage { get; set; }
        public bool Success { get; set; }
    }

    /// <summary>
    /// Event arguments for invoice update notifications
    /// </summary>
    public class InvoiceUpdateEventArgs : EventArgs
    {
        /// <summary>
        /// The invoice ID that was updated
        /// </summary>
        public string InvoiceId { get; set; } = string.Empty;

        /// <summary>
        /// The new status of the invoice
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// When the invoice was paid (if applicable)
        /// </summary>
        public DateTime? PaidAt { get; set; }

        /// <summary>
        /// The transaction hash (if available)
        /// </summary>
        public string? TransactionHash { get; set; }

        /// <summary>
        /// Raw data from the WebSocket update
        /// </summary>
        public JObject? RawData { get; set; }
    }

    /// <summary>
    /// Event arguments for payment received notifications
    /// </summary>
    public class PaymentReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// The payment hash of the received payment
        /// </summary>
        public string PaymentHash { get; set; } = string.Empty;

        /// <summary>
        /// The amount received in satoshis
        /// </summary>
        public long AmountSats { get; set; }

        /// <summary>
        /// The transaction ID if available
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// When the payment was received
        /// </summary>
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Raw data from the WebSocket update
        /// </summary>
        public JObject? RawData { get; set; }
    }
}