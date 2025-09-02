#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BTCPayServer.Plugins.Flash.Exceptions;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Centralized error handling for Flash plugin operations
    /// </summary>
    public static class FlashErrorHandler
    {
        /// <summary>
        /// Handles Flash-specific exceptions and converts them to user-friendly messages
        /// </summary>
        public static string GetUserFriendlyMessage(Exception ex)
        {
            return ex switch
            {
                FlashApiException apiEx => apiEx.Message ?? "Flash API error occurred",
                UnauthorizedAccessException => "Flash API authentication failed. Please check your API token.",
                TimeoutException => "Request to Flash API timed out. Please try again.",
                InvalidOperationException ioe when ioe.Message.Contains("wallet") => "Flash wallet not found or not configured.",
                InvalidOperationException ioe when ioe.Message.Contains("minimum") => ioe.Message,
                ArgumentException ae => $"Invalid input: {ae.Message}",
                _ => "An unexpected error occurred while communicating with Flash."
            };
        }

        /// <summary>
        /// Executes an action with comprehensive error handling and logging
        /// </summary>
        public static async Task<T> ExecuteWithErrorHandlingAsync<T>(
            Func<Task<T>> action,
            ILogger logger,
            string operationName,
            Func<T>? fallbackValue = null)
        {
            try
            {
                logger.LogDebug("Starting operation: {OperationName}", operationName);
                var result = await action();
                logger.LogDebug("Completed operation: {OperationName}", operationName);
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in operation {OperationName}: {ErrorMessage}", 
                    operationName, ex.Message);

                if (fallbackValue != null)
                {
                    logger.LogWarning("Using fallback value for operation: {OperationName}", operationName);
                    return fallbackValue();
                }

                throw new FlashApiException(
                    $"Failed to {operationName}: {GetUserFriendlyMessage(ex)}",
                    null,
                    null,
                    false,
                    ex);
            }
        }

        /// <summary>
        /// Executes an action with retry logic for transient failures
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> action,
            ILogger logger,
            string operationName,
            int maxRetries = 3,
            int baseDelayMs = 1000)
        {
            Exception? lastException = null;

            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    if (i > 0)
                    {
                        var delay = baseDelayMs * Math.Pow(2, i - 1); // Exponential backoff
                        logger.LogInformation("Retrying operation {OperationName} (attempt {Attempt}/{MaxRetries}) after {Delay}ms", 
                            operationName, i + 1, maxRetries + 1, delay);
                        await Task.Delay(TimeSpan.FromMilliseconds(delay));
                    }

                    return await action();
                }
                catch (Exception ex) when (IsTransientError(ex) && i < maxRetries)
                {
                    lastException = ex;
                    logger.LogWarning(ex, "Transient error in operation {OperationName}, will retry", operationName);
                }
                catch (Exception ex)
                {
                    throw new FlashApiException(
                        $"Failed to {operationName} after {i + 1} attempts: {GetUserFriendlyMessage(ex)}",
                        null,
                        null,
                        false,
                        ex);
                }
            }

            throw new FlashApiException(
                $"Failed to {operationName} after {maxRetries + 1} attempts: {GetUserFriendlyMessage(lastException!)}",
                null,
                null,
                false,
                lastException!);
        }

        /// <summary>
        /// Determines if an error is transient and should be retried
        /// </summary>
        private static bool IsTransientError(Exception ex)
        {
            return ex switch
            {
                TimeoutException => true,
                System.Net.Http.HttpRequestException => true,
                FlashApiException apiEx when ((FlashPluginException)apiEx).IsRetryable => true,
                _ => false
            };
        }
    }


    /// <summary>
    /// Exception for Flash configuration errors
    /// </summary>
    public class FlashConfigurationException : Exception
    {
        public FlashConfigurationException(string message, Exception? innerException = null) 
            : base(message, innerException)
        {
        }
    }
}