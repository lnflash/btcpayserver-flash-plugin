#nullable enable
using System;
using System.Collections.Generic;
using System.Net;

namespace BTCPayServer.Plugins.Flash.Exceptions
{
    /// <summary>
    /// Base exception for all Flash plugin errors
    /// </summary>
    public class FlashPluginException : Exception
    {
        /// <summary>
        /// Unique error code for this exception type
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Indicates whether the operation can be safely retried
        /// </summary>
        public bool IsRetryable { get; }

        /// <summary>
        /// Additional context information about the error
        /// </summary>
        public Dictionary<string, object?> Context { get; } = new();

        /// <summary>
        /// Correlation ID for tracking errors across services
        /// </summary>
        public string CorrelationId { get; }

        public FlashPluginException(
            string message, 
            string errorCode = "FLASH_ERROR", 
            bool isRetryable = false,
            Exception? innerException = null) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            IsRetryable = isRetryable;
            CorrelationId = Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// Exception thrown when authentication with Flash API fails
    /// </summary>
    public class FlashAuthenticationException : FlashPluginException
    {
        public FlashAuthenticationException(string message, Exception? innerException = null)
            : base(message, "FLASH_AUTH_ERROR", false, innerException)
        {
        }
    }

    /// <summary>
    /// Exception thrown when Flash API calls fail
    /// </summary>
    public class FlashApiException : FlashPluginException
    {
        /// <summary>
        /// HTTP status code if available
        /// </summary>
        public HttpStatusCode? StatusCode { get; }

        /// <summary>
        /// Error code returned by the Flash API
        /// </summary>
        public string? ApiErrorCode { get; }

        /// <summary>
        /// GraphQL errors if present
        /// </summary>
        public GraphQL.GraphQLError[]? GraphQLErrors { get; }

        public FlashApiException(
            string message, 
            HttpStatusCode? statusCode = null,
            string? apiErrorCode = null,
            bool isRetryable = false,
            Exception? innerException = null)
            : base(message, "FLASH_API_ERROR", isRetryable, innerException)
        {
            StatusCode = statusCode;
            ApiErrorCode = apiErrorCode;
        }

        public FlashApiException(
            string message,
            GraphQL.GraphQLError[] graphQLErrors,
            bool isRetryable = false)
            : base(message, "FLASH_GRAPHQL_ERROR", isRetryable)
        {
            GraphQLErrors = graphQLErrors;
        }
    }

    /// <summary>
    /// Exception thrown when Flash API rate limits are exceeded
    /// </summary>
    public class FlashRateLimitException : FlashApiException
    {
        /// <summary>
        /// How long to wait before retrying
        /// </summary>
        public TimeSpan? RetryAfter { get; }

        public FlashRateLimitException(
            string message, 
            TimeSpan? retryAfter = null,
            Exception? innerException = null)
            : base(message, HttpStatusCode.TooManyRequests, "RATE_LIMIT_EXCEEDED", true, innerException)
        {
            RetryAfter = retryAfter ?? TimeSpan.FromSeconds(60);
        }
    }

    /// <summary>
    /// Exception thrown for invoice-related errors
    /// </summary>
    public class FlashInvoiceException : FlashPluginException
    {
        /// <summary>
        /// The invoice ID if available
        /// </summary>
        public string? InvoiceId { get; }

        /// <summary>
        /// The payment hash if available
        /// </summary>
        public string? PaymentHash { get; }

        /// <summary>
        /// The invoice status if known
        /// </summary>
        public string? Status { get; }

        public FlashInvoiceException(
            string message,
            string? invoiceId = null,
            string? paymentHash = null,
            string? status = null,
            bool isRetryable = false,
            Exception? innerException = null)
            : base(message, "FLASH_INVOICE_ERROR", isRetryable, innerException)
        {
            InvoiceId = invoiceId;
            PaymentHash = paymentHash;
            Status = status;
            
            if (invoiceId != null) Context["InvoiceId"] = invoiceId;
            if (paymentHash != null) Context["PaymentHash"] = paymentHash;
            if (status != null) Context["Status"] = status;
        }
    }

    /// <summary>
    /// Exception thrown for payment-related errors
    /// </summary>
    public class FlashPaymentException : FlashPluginException
    {
        /// <summary>
        /// The payment result if available
        /// </summary>
        public BTCPayServer.Lightning.PayResult? Result { get; }

        /// <summary>
        /// The transaction ID if available
        /// </summary>
        public string? TransactionId { get; }

        /// <summary>
        /// The payment request (BOLT11) if available
        /// </summary>
        public string? PaymentRequest { get; }

        public FlashPaymentException(
            string message,
            BTCPayServer.Lightning.PayResult? result = null,
            string? transactionId = null,
            string? paymentRequest = null,
            bool isRetryable = false,
            Exception? innerException = null)
            : base(message, "FLASH_PAYMENT_ERROR", isRetryable, innerException)
        {
            Result = result;
            TransactionId = transactionId;
            PaymentRequest = paymentRequest;
            
            if (result != null) Context["PayResult"] = result.ToString();
            if (transactionId != null) Context["TransactionId"] = transactionId;
        }
    }

    /// <summary>
    /// Exception thrown for WebSocket connection errors
    /// </summary>
    public class FlashWebSocketException : FlashPluginException
    {
        /// <summary>
        /// Current WebSocket connection state
        /// </summary>
        public System.Net.WebSockets.WebSocketState? ConnectionState { get; }

        /// <summary>
        /// Close status code if connection was closed
        /// </summary>
        public System.Net.WebSockets.WebSocketCloseStatus? CloseStatus { get; }

        public FlashWebSocketException(
            string message,
            System.Net.WebSockets.WebSocketState? connectionState = null,
            System.Net.WebSockets.WebSocketCloseStatus? closeStatus = null,
            bool isRetryable = true,
            Exception? innerException = null)
            : base(message, "FLASH_WEBSOCKET_ERROR", isRetryable, innerException)
        {
            ConnectionState = connectionState;
            CloseStatus = closeStatus;
            
            if (connectionState != null) Context["ConnectionState"] = connectionState.ToString();
            if (closeStatus != null) Context["CloseStatus"] = closeStatus.ToString();
        }
    }

    /// <summary>
    /// Exception thrown for exchange rate errors
    /// </summary>
    public class FlashExchangeRateException : FlashPluginException
    {
        /// <summary>
        /// Last known good exchange rate if available
        /// </summary>
        public decimal? LastKnownRate { get; }

        /// <summary>
        /// Currency pair for the rate
        /// </summary>
        public string CurrencyPair { get; }

        public FlashExchangeRateException(
            string message,
            string currencyPair,
            decimal? lastKnownRate = null,
            bool isRetryable = true,
            Exception? innerException = null)
            : base(message, "FLASH_EXCHANGE_RATE_ERROR", isRetryable, innerException)
        {
            CurrencyPair = currencyPair;
            LastKnownRate = lastKnownRate;
            
            Context["CurrencyPair"] = currencyPair;
            if (lastKnownRate != null) Context["LastKnownRate"] = lastKnownRate;
        }
    }

    /// <summary>
    /// Exception thrown for wallet-related errors
    /// </summary>
    public class FlashWalletException : FlashPluginException
    {
        /// <summary>
        /// The wallet ID if available
        /// </summary>
        public string? WalletId { get; }

        /// <summary>
        /// The wallet currency if known
        /// </summary>
        public string? Currency { get; }

        public FlashWalletException(
            string message,
            string? walletId = null,
            string? currency = null,
            bool isRetryable = false,
            Exception? innerException = null)
            : base(message, "FLASH_WALLET_ERROR", isRetryable, innerException)
        {
            WalletId = walletId;
            Currency = currency;
            
            if (walletId != null) Context["WalletId"] = walletId;
            if (currency != null) Context["Currency"] = currency;
        }
    }

    /// <summary>
    /// Exception thrown for transaction-related errors
    /// </summary>
    public class FlashTransactionException : FlashPluginException
    {
        /// <summary>
        /// The transaction ID if available
        /// </summary>
        public string? TransactionId { get; }

        /// <summary>
        /// The transaction type if known
        /// </summary>
        public string? TransactionType { get; }

        public FlashTransactionException(
            string message,
            string? transactionId = null,
            string? transactionType = null,
            bool isRetryable = false,
            Exception? innerException = null)
            : base(message, "FLASH_TRANSACTION_ERROR", isRetryable, innerException)
        {
            TransactionId = transactionId;
            TransactionType = transactionType;
            
            if (transactionId != null) Context["TransactionId"] = transactionId;
            if (transactionType != null) Context["TransactionType"] = transactionType;
        }
    }
}