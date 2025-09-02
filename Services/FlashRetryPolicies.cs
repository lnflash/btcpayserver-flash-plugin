#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BTCPayServer.Plugins.Flash.Exceptions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.CircuitBreaker;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Retry policies for Flash plugin services
    /// </summary>
    public static class FlashRetryPolicies
    {
        /// <summary>
        /// Get HTTP retry policy with exponential backoff
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetHttpRetryPolicy(ILogger? logger = null)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // Handles HttpRequestException, 5XX and 408
                .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
                .OrResult(msg => msg.StatusCode == HttpStatusCode.ServiceUnavailable)
                .OrResult(msg => msg.StatusCode == HttpStatusCode.GatewayTimeout)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var statusCode = outcome.Result?.StatusCode;
                        logger?.LogWarning(
                            "HTTP retry {RetryCount} after {Delay}ms. Status: {StatusCode}",
                            retryCount,
                            timespan.TotalMilliseconds,
                            statusCode);
                    });
        }

        /// <summary>
        /// Get circuit breaker policy for HTTP requests
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetHttpCircuitBreakerPolicy(ILogger? logger = null)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .CircuitBreakerAsync(
                    5, // Number of consecutive failures before opening circuit
                    TimeSpan.FromMinutes(1), // Duration to keep circuit open
                    onBreak: (result, duration) =>
                    {
                        logger?.LogError(
                            "Circuit breaker opened for {Duration}s due to consecutive failures",
                            duration.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        logger?.LogInformation("Circuit breaker reset, resuming normal operation");
                    },
                    onHalfOpen: () =>
                    {
                        logger?.LogInformation("Circuit breaker is half-open, testing with next request");
                    });
        }

        /// <summary>
        /// Get retry policy for GraphQL operations
        /// </summary>
        public static IAsyncPolicy GetGraphQLRetryPolicy(ILogger? logger = null)
        {
            return Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .Or<FlashApiException>(ex => ((FlashPluginException)ex).IsRetryable)
                .Or<FlashRateLimitException>()
                .WaitAndRetryAsync(
                    new[]
                    {
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(5)
                    },
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        logger?.LogWarning(
                            exception,
                            "GraphQL retry {RetryCount} after {Delay}ms. Error: {ErrorMessage}",
                            retryCount,
                            timespan.TotalMilliseconds,
                            exception.Message);
                    });
        }

        /// <summary>
        /// Get retry policy for invoice operations
        /// </summary>
        public static IAsyncPolicy GetInvoiceRetryPolicy(ILogger? logger = null)
        {
            return Policy
                .Handle<FlashInvoiceException>(ex => ex.IsRetryable)
                .Or<FlashApiException>(ex => ((FlashPluginException)ex).IsRetryable)
                .Or<HttpRequestException>()
                .WaitAndRetryAsync(
                    5, // More retries for invoice checking
                    retryAttempt => TimeSpan.FromSeconds(retryAttempt * 2),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        logger?.LogWarning(
                            "Invoice operation retry {RetryCount} after {Delay}ms",
                            retryCount,
                            timespan.TotalMilliseconds);
                    });
        }

        /// <summary>
        /// Get retry policy for payment operations (more conservative)
        /// </summary>
        public static IAsyncPolicy GetPaymentRetryPolicy(ILogger? logger = null)
        {
            return Policy
                .Handle<FlashPaymentException>(ex => ex.IsRetryable)
                .Or<FlashApiException>(ex => ((FlashPluginException)ex).IsRetryable && ex.StatusCode != HttpStatusCode.Conflict)
                .WaitAndRetryAsync(
                    2, // Limited retries for payments
                    retryAttempt => TimeSpan.FromSeconds(retryAttempt * 3),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        logger?.LogWarning(
                            exception,
                            "Payment operation retry {RetryCount} after {Delay}ms. CAUTION: Verify payment wasn't processed",
                            retryCount,
                            timespan.TotalMilliseconds);
                    });
        }

        /// <summary>
        /// Get retry policy for WebSocket operations
        /// </summary>
        public static IAsyncPolicy GetWebSocketRetryPolicy(ILogger? logger = null)
        {
            return Policy
                .Handle<FlashWebSocketException>(ex => ex.IsRetryable)
                .Or<System.Net.WebSockets.WebSocketException>()
                .Or<InvalidOperationException>()
                .WaitAndRetryAsync(
                    new[]
                    {
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(30),
                        TimeSpan.FromSeconds(60)
                    },
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        logger?.LogWarning(
                            "WebSocket reconnection attempt {RetryCount} after {Delay}s",
                            retryCount,
                            timespan.TotalSeconds);
                    });
        }

        /// <summary>
        /// Get retry policy for exchange rate operations
        /// </summary>
        public static IAsyncPolicy GetExchangeRateRetryPolicy(ILogger? logger = null)
        {
            return Policy
                .Handle<FlashExchangeRateException>()
                .Or<FlashApiException>(ex => ((FlashPluginException)ex).IsRetryable)
                .Or<HttpRequestException>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(retryAttempt),
                    onRetry: (exception, timespan, retryCount, context) =>
                    {
                        if (exception is FlashExchangeRateException rateEx && rateEx.LastKnownRate.HasValue)
                        {
                            logger?.LogWarning(
                                "Exchange rate retry {RetryCount} after {Delay}ms. Using cached rate: {Rate}",
                                retryCount,
                                timespan.TotalMilliseconds,
                                rateEx.LastKnownRate.Value);
                        }
                        else
                        {
                            logger?.LogWarning(
                                "Exchange rate retry {RetryCount} after {Delay}ms",
                                retryCount,
                                timespan.TotalMilliseconds);
                        }
                    });
        }

        /// <summary>
        /// Wrap an operation with retry policy and proper error handling
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            IAsyncPolicy retryPolicy,
            string operationName,
            ILogger? logger = null)
        {
            try
            {
                return await retryPolicy.ExecuteAsync(async () =>
                {
                    try
                    {
                        return await operation();
                    }
                    catch (Exception ex) when (!(ex is FlashPluginException))
                    {
                        // Wrap non-Flash exceptions to preserve context
                        throw new FlashPluginException(
                            $"Unexpected error in {operationName}",
                            "FLASH_UNEXPECTED_ERROR",
                            false,
                            ex);
                    }
                });
            }
            catch (BrokenCircuitException ex)
            {
                logger?.LogError(ex, "Circuit breaker is open for {OperationName}", operationName);
                throw new FlashApiException(
                    "Service temporarily unavailable due to repeated failures",
                    HttpStatusCode.ServiceUnavailable,
                    "CIRCUIT_BREAKER_OPEN",
                    false,
                    ex);
            }
        }

        /// <summary>
        /// Create a combined policy with retry and circuit breaker
        /// </summary>
        public static IAsyncPolicy<HttpResponseMessage> GetCombinedHttpPolicy(ILogger? logger = null)
        {
            return Policy.WrapAsync(
                GetHttpRetryPolicy(logger),
                GetHttpCircuitBreakerPolicy(logger));
        }
    }
}