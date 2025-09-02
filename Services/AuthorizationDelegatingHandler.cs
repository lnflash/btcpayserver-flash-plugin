using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// A delegating handler that ensures the Authorization header is set on every HTTP request
    /// </summary>
    public class AuthorizationDelegatingHandler : DelegatingHandler
    {
        private readonly string _bearerToken;
        private readonly ILogger _logger;

        public AuthorizationDelegatingHandler(string bearerToken, ILogger logger)
        {
            _bearerToken = bearerToken ?? throw new ArgumentNullException(nameof(bearerToken));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Always ensure the authorization header is set
            if (request.Headers.Authorization == null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
                _logger.LogDebug("[AuthHandler] Added authorization header to request: {Method} {Uri}", 
                    request.Method, request.RequestUri);
            }
            else
            {
                _logger.LogDebug("[AuthHandler] Authorization header already present: {Method} {Uri}", 
                    request.Method, request.RequestUri);
            }

            // Log the request details for debugging
            _logger.LogDebug("[AuthHandler] Sending request with {HeaderCount} headers to {Uri}", 
                request.Headers.Count(), request.RequestUri);

            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                
                // Log the response status
                _logger.LogDebug("[AuthHandler] Received response: {StatusCode} from {Uri}", 
                    response.StatusCode, request.RequestUri);
                
                // If we got Unauthorized, log more details
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("[AuthHandler] Received Unauthorized response. Token: {TokenPrefix}..., URI: {Uri}", 
                        _bearerToken.Length > 10 ? _bearerToken.Substring(0, 10) : "INVALID",
                        request.RequestUri);
                    
                    // Log response headers for debugging
                    foreach (var header in response.Headers)
                    {
                        _logger.LogDebug("[AuthHandler] Response header: {Key}: {Value}", 
                            header.Key, string.Join(", ", header.Value));
                    }
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthHandler] Error sending request to {Uri}", request.RequestUri);
                throw;
            }
        }
    }
}