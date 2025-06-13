#nullable enable
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// HTTP message handler that logs requests and responses for debugging
    /// </summary>
    public class LoggingHttpMessageHandler : DelegatingHandler
    {
        private readonly ILogger<LoggingHttpMessageHandler> _logger;

        public LoggingHttpMessageHandler(ILogger<LoggingHttpMessageHandler> logger, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Log request
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _logger.LogInformation("[HTTP Request {RequestId}] {Method} {Uri}", requestId, request.Method, request.RequestUri);
            
            // Log headers (excluding sensitive authorization data)
            foreach (var header in request.Headers)
            {
                if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("[HTTP Request {RequestId}] Header: {Key}: Bearer [REDACTED]", requestId, header.Key);
                }
                else
                {
                    _logger.LogInformation("[HTTP Request {RequestId}] Header: {Key}: {Value}", requestId, header.Key, string.Join(", ", header.Value));
                }
            }

            // Log request body
            if (request.Content != null)
            {
                var requestBody = await request.Content.ReadAsStringAsync();
                _logger.LogInformation("[HTTP Request {RequestId}] Body: {Body}", requestId, requestBody);
            }

            // Send request
            var response = await base.SendAsync(request, cancellationToken);

            // Log response
            _logger.LogInformation("[HTTP Response {RequestId}] Status: {StatusCode} {ReasonPhrase}", 
                requestId, (int)response.StatusCode, response.ReasonPhrase);

            // Log response headers
            foreach (var header in response.Headers)
            {
                _logger.LogInformation("[HTTP Response {RequestId}] Header: {Key}: {Value}", 
                    requestId, header.Key, string.Join(", ", header.Value));
            }

            // Log response body
            if (response.Content != null)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("[HTTP Response {RequestId}] Body: {Body}", requestId, responseBody);
                
                // Recreate content since we consumed it
                response.Content = new StringContent(responseBody, Encoding.UTF8, response.Content.Headers.ContentType?.MediaType ?? "application/json");
            }

            return response;
        }
    }
}