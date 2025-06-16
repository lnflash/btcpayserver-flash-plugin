using System;

namespace BTCPayServer.Plugins.Flash.Models
{
    /// <summary>
    /// Configuration for WebSocket reconnection retry policy
    /// </summary>
    public class WebSocketRetryPolicy
    {
        /// <summary>
        /// Initial delay before first reconnection attempt
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
        
        /// <summary>
        /// Maximum delay between reconnection attempts
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
        
        /// <summary>
        /// Multiplier for exponential backoff (e.g., 2.0 = double the delay each time)
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;
        
        /// <summary>
        /// Maximum jitter to add to delays (randomization to prevent thundering herd)
        /// </summary>
        public TimeSpan MaxJitter { get; set; } = TimeSpan.FromSeconds(3);
        
        /// <summary>
        /// Maximum number of reconnection attempts (0 = unlimited)
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 0;
        
        /// <summary>
        /// Timeout for connection attempts
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
        
        /// <summary>
        /// Calculate next retry delay based on attempt number
        /// </summary>
        public TimeSpan CalculateDelay(int attemptNumber)
        {
            if (attemptNumber < 1) attemptNumber = 1;
            
            // Calculate exponential backoff
            var delay = TimeSpan.FromMilliseconds(
                InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attemptNumber - 1)
            );
            
            // Cap at maximum delay
            if (delay > MaxDelay)
                delay = MaxDelay;
            
            // Add jitter
            if (MaxJitter.TotalMilliseconds > 0)
            {
                var random = new Random();
                var jitter = TimeSpan.FromMilliseconds(
                    random.NextDouble() * MaxJitter.TotalMilliseconds
                );
                delay = delay.Add(jitter);
            }
            
            return delay;
        }
    }
}