#nullable enable
using System;

namespace BTCPayServer.Plugins.Flash.Models
{
    /// <summary>
    /// Represents the current state of a WebSocket connection
    /// </summary>
    public enum WebSocketConnectionState
    {
        /// <summary>
        /// Initial state, not yet connected
        /// </summary>
        Disconnected,
        
        /// <summary>
        /// Currently attempting to establish connection
        /// </summary>
        Connecting,
        
        /// <summary>
        /// Connection established and handshake completed
        /// </summary>
        Connected,
        
        /// <summary>
        /// Connection lost, waiting before reconnection attempt
        /// </summary>
        Reconnecting,
        
        /// <summary>
        /// Intentionally disconnecting
        /// </summary>
        Disconnecting,
        
        /// <summary>
        /// Connection permanently failed or disposed
        /// </summary>
        Failed
    }
    
    /// <summary>
    /// Event args for connection state changes
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public WebSocketConnectionState PreviousState { get; }
        public WebSocketConnectionState CurrentState { get; }
        public string? Reason { get; }
        public Exception? Exception { get; }
        
        public ConnectionStateChangedEventArgs(
            WebSocketConnectionState previousState, 
            WebSocketConnectionState currentState,
            string? reason = null,
            Exception? exception = null)
        {
            PreviousState = previousState;
            CurrentState = currentState;
            Reason = reason;
            Exception = exception;
        }
    }
}