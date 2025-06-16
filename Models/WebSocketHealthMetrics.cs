using System;
using System.Threading;

namespace BTCPayServer.Plugins.Flash.Models
{
    /// <summary>
    /// Health metrics for WebSocket connection monitoring
    /// </summary>
    public class WebSocketHealthMetrics
    {
        private long _messagesReceived;
        private long _messagesSent;
        private long _reconnectCount;
        private long _errorCount;
        private DateTime? _lastMessageReceivedAt;
        private DateTime? _lastMessageSentAt;
        private DateTime? _connectedAt;
        private DateTime? _lastErrorAt;
        
        /// <summary>
        /// Total messages received since connection established
        /// </summary>
        public long MessagesReceived => _messagesReceived;
        
        /// <summary>
        /// Total messages sent since connection established
        /// </summary>
        public long MessagesSent => _messagesSent;
        
        /// <summary>
        /// Number of reconnection attempts
        /// </summary>
        public long ReconnectCount => _reconnectCount;
        
        /// <summary>
        /// Number of errors encountered
        /// </summary>
        public long ErrorCount => _errorCount;
        
        /// <summary>
        /// Timestamp of last received message
        /// </summary>
        public DateTime? LastMessageReceivedAt => _lastMessageReceivedAt;
        
        /// <summary>
        /// Timestamp of last sent message
        /// </summary>
        public DateTime? LastMessageSentAt => _lastMessageSentAt;
        
        /// <summary>
        /// Timestamp when current connection was established
        /// </summary>
        public DateTime? ConnectedAt => _connectedAt;
        
        /// <summary>
        /// Timestamp of last error
        /// </summary>
        public DateTime? LastErrorAt => _lastErrorAt;
        
        /// <summary>
        /// Duration of current connection
        /// </summary>
        public TimeSpan? ConnectionDuration => 
            _connectedAt.HasValue ? DateTime.UtcNow - _connectedAt.Value : null;
        
        /// <summary>
        /// Time since last message received
        /// </summary>
        public TimeSpan? TimeSinceLastReceived => 
            _lastMessageReceivedAt.HasValue ? DateTime.UtcNow - _lastMessageReceivedAt.Value : null;
        
        public void RecordMessageReceived()
        {
            Interlocked.Increment(ref _messagesReceived);
            _lastMessageReceivedAt = DateTime.UtcNow;
        }
        
        public void RecordMessageSent()
        {
            Interlocked.Increment(ref _messagesSent);
            _lastMessageSentAt = DateTime.UtcNow;
        }
        
        public void RecordReconnect()
        {
            Interlocked.Increment(ref _reconnectCount);
        }
        
        public void RecordError()
        {
            Interlocked.Increment(ref _errorCount);
            _lastErrorAt = DateTime.UtcNow;
        }
        
        public void RecordConnectionEstablished()
        {
            _connectedAt = DateTime.UtcNow;
            _messagesReceived = 0;
            _messagesSent = 0;
        }
        
        public void Reset()
        {
            _messagesReceived = 0;
            _messagesSent = 0;
            _reconnectCount = 0;
            _errorCount = 0;
            _lastMessageReceivedAt = null;
            _lastMessageSentAt = null;
            _connectedAt = null;
            _lastErrorAt = null;
        }
    }
}