#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Validates Flash Lightning connection strings and connections
    /// </summary>
    public class FlashConnectionValidator
    {
        private readonly ILogger<FlashConnectionValidator> _logger;
        private readonly HttpClient _httpClient;

        public FlashConnectionValidator(
            ILogger<FlashConnectionValidator> logger,
            HttpClient httpClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Validates a Flash connection string
        /// </summary>
        public ValidationResult ValidateConnectionString(string connectionString)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                errors.Add("Connection string cannot be empty");
                return new ValidationResult(false, errors);
            }

            // Parse connection string
            var parts = connectionString.Split(';')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            string? apiEndpoint = null;
            string? bearerToken = null;

            foreach (var part in parts)
            {
                var keyValue = part.Split('=', 2);
                if (keyValue.Length != 2)
                {
                    errors.Add($"Invalid connection string format: '{part}'");
                    continue;
                }

                var key = keyValue[0].Trim().ToLowerInvariant();
                var value = keyValue[1].Trim();

                switch (key)
                {
                    case "api":
                    case "endpoint":
                        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                        {
                            errors.Add($"Invalid API endpoint URL: '{value}'");
                        }
                        else if (uri.Scheme != "https" && uri.Scheme != "http")
                        {
                            errors.Add($"API endpoint must use HTTP or HTTPS: '{value}'");
                        }
                        else
                        {
                            apiEndpoint = value;
                        }
                        break;

                    case "api-token":
                    case "bearer":
                    case "token":
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            errors.Add("API token cannot be empty");
                        }
                        else if (value.Length < 20)
                        {
                            errors.Add("API token appears to be invalid (too short)");
                        }
                        else
                        {
                            bearerToken = value;
                        }
                        break;

                    default:
                        errors.Add($"Unknown connection string parameter: '{key}'");
                        break;
                }
            }

            // Validate required parameters
            if (string.IsNullOrEmpty(apiEndpoint))
            {
                errors.Add("API endpoint is required (api=https://...)");
            }

            if (string.IsNullOrEmpty(bearerToken))
            {
                errors.Add("API token is required (api-token=...)");
            }

            return new ValidationResult(errors.Count == 0, errors);
        }

        /// <summary>
        /// Tests a Flash connection
        /// </summary>
        public async Task<ConnectionTestResult> TestConnectionAsync(
            string apiEndpoint,
            string bearerToken,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Testing Flash connection to {Endpoint}", apiEndpoint);

                // Create a simple GraphQL query to test the connection
                var testQuery = @"
                {
                    me {
                        defaultAccount {
                            id
                        }
                    }
                }";

                var request = new HttpRequestMessage(HttpMethod.Post, apiEndpoint)
                {
                    Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(new { query = testQuery }),
                        System.Text.Encoding.UTF8,
                        "application/json")
                };

                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (content.Contains("\"data\"") && !content.Contains("\"errors\""))
                    {
                        _logger.LogInformation("Flash connection test successful");
                        return new ConnectionTestResult(true, "Connection successful");
                    }
                    else if (content.Contains("\"errors\""))
                    {
                        _logger.LogWarning("Flash connection test failed with errors in response");
                        return new ConnectionTestResult(false, "API returned errors - check your credentials");
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Flash connection test failed with unauthorized status");
                    return new ConnectionTestResult(false, "Invalid API token");
                }
                else
                {
                    _logger.LogWarning("Flash connection test failed with status {StatusCode}", response.StatusCode);
                    return new ConnectionTestResult(false, $"Connection failed with status: {response.StatusCode}");
                }

                return new ConnectionTestResult(false, "Unexpected response from Flash API");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error testing Flash connection");
                return new ConnectionTestResult(false, $"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Flash connection test timed out");
                return new ConnectionTestResult(false, "Connection timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error testing Flash connection");
                return new ConnectionTestResult(false, $"Unexpected error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Result of connection string validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<string> Errors { get; }

        public ValidationResult(bool isValid, IEnumerable<string> errors)
        {
            IsValid = isValid;
            Errors = errors.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Result of connection test
    /// </summary>
    public class ConnectionTestResult
    {
        public bool Success { get; }
        public string Message { get; }

        public ConnectionTestResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }
}