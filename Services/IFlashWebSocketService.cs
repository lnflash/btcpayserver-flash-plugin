#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
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
        /// Event raised when an invoice is updated
        /// </summary>
        event EventHandler<InvoiceUpdateEventArgs> InvoiceUpdated;

        /// <summary>
        /// Subscribe to real-time updates for a specific invoice
        /// </summary>
        Task SubscribeToInvoiceUpdatesAsync(string invoiceId, CancellationToken cancellation = default);

        /// <summary>
        /// Unsubscribe from updates for a specific invoice
        /// </summary>
        Task UnsubscribeFromInvoiceUpdatesAsync(string invoiceId, CancellationToken cancellation = default);
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
}