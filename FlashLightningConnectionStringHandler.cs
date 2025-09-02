#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Threading;

namespace BTCPayServer.Plugins.Flash
{
    public class FlashLightningConnectionStringHandler : ILightningConnectionStringHandler
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<string, ILightningClient> _clientCache = new();

        public FlashLightningConnectionStringHandler(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public string DisplayName => "Flash";

        public string PaymentLinkTemplate => "lightning:{BOLT11}";

        public ILightningClient Create(string connectionString, Network network, out string error)
        {
            if (connectionString == null)
            {
                error = "Connection string is null";
                return null;
            }

            if (!CanHandle(connectionString))
            {
                error = "Invalid Flash connection string";
                return null;
            }

            try
            {
                // Check cache first
                if (_clientCache.TryGetValue(connectionString, out var cachedClient))
                {
                    error = null;
                    _loggerFactory.CreateLogger<FlashLightningConnectionStringHandler>()
                        .LogDebug("Returning cached Flash Lightning client for connection string");
                    return cachedClient;
                }

                var flashConnectionString = Parse(connectionString);
                error = null;
                var client = new FlashLightningClient(
                    flashConnectionString.BearerToken,
                    new Uri(flashConnectionString.Endpoint),
                    _loggerFactory.CreateLogger<FlashLightningClient>(),
                    httpClient: null,
                    loggerFactory: _loggerFactory);
                
                // Cache the client
                _clientCache.TryAdd(connectionString, client);
                _loggerFactory.CreateLogger<FlashLightningConnectionStringHandler>()
                    .LogInformation("Created and cached new Flash Lightning client for connection string");
                
                return client;
            }
            catch (Exception ex)
            {
                error = $"Error creating Flash Lightning client: {ex.Message}";
                return null;
            }
        }

        [Obsolete("Use the Create method with error out parameter instead")]
        public ILightningClient Create(string connectionString, BTCPayNetwork network)
        {
            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));

            if (!CanHandle(connectionString))
                throw new ArgumentException("Invalid connection string", nameof(connectionString));

            // Check cache first
            if (_clientCache.TryGetValue(connectionString, out var cachedClient))
            {
                _loggerFactory.CreateLogger<FlashLightningConnectionStringHandler>()
                    .LogDebug("Returning cached Flash Lightning client for connection string (obsolete method)");
                return cachedClient;
            }

            var flashConnectionString = Parse(connectionString);
            var client = new FlashLightningClient(
                flashConnectionString.BearerToken,
                new Uri(flashConnectionString.Endpoint),
                _loggerFactory.CreateLogger<FlashLightningClient>(),
                httpClient: null,
                loggerFactory: _loggerFactory);
            
            // Cache the client
            _clientCache.TryAdd(connectionString, client);
            _loggerFactory.CreateLogger<FlashLightningConnectionStringHandler>()
                .LogInformation("Created and cached new Flash Lightning client for connection string (obsolete method)");
            
            return client;
        }

        public async Task<object> GetLightningClient(string connectionString, BTCPayNetwork network, CancellationToken cancellation)
        {
            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));

            if (!CanHandle(connectionString))
                throw new ArgumentException("Invalid connection string", nameof(connectionString));

            try
            {
                // Check cache first
                if (_clientCache.TryGetValue(connectionString, out var cachedClient))
                {
                    _loggerFactory.CreateLogger<FlashLightningConnectionStringHandler>()
                        .LogDebug("Returning cached Flash Lightning client for connection string (async method)");
                    // Still verify it works
                    await cachedClient.GetInfo(cancellation);
                    return cachedClient;
                }

                var flashConnectionString = Parse(connectionString);
                var client = new FlashLightningClient(
                    flashConnectionString.BearerToken,
                    new Uri(flashConnectionString.Endpoint),
                    _loggerFactory.CreateLogger<FlashLightningClient>(),
                    httpClient: null,
                    loggerFactory: _loggerFactory);

                // Verify the connection works by calling GetInfo
                await client.GetInfo(cancellation);

                // Cache the client after successful verification
                _clientCache.TryAdd(connectionString, client);
                _loggerFactory.CreateLogger<FlashLightningConnectionStringHandler>()
                    .LogInformation("Created and cached new Flash Lightning client for connection string (async method)");

                // Return the client instance directly
                return client;
            }
            catch (Exception ex)
            {
                _loggerFactory.CreateLogger<FlashLightningConnectionStringHandler>()
                    .LogError(ex, "Error creating Flash Lightning client");
                throw;
            }
        }

        public Task<object> GetLightningNodeDashboardInfo(string connectionString, BTCPayNetwork network)
        {
            return Task.FromResult<object>(null);
        }

        public bool CanHandle(string connectionString)
        {
            try
            {
                if (connectionString == null)
                    return false;

                Parse(connectionString);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public (string Endpoint, string BearerToken) Parse(string connectionString)
        {
            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));

            var parameters = new Dictionary<string, string>();

            foreach (var param in connectionString.Split(';'))
            {
                if (string.IsNullOrEmpty(param))
                    continue;

                var parts = param.Split('=', 2);
                if (parts.Length != 2)
                    throw new FormatException("Invalid connection string parameter format");

                parameters[parts[0].Trim()] = parts[1].Trim();
            }

            if (!parameters.TryGetValue("type", out var type) || type != "flash")
                throw new FormatException("Missing or invalid 'type' parameter");

            if (!parameters.TryGetValue("server", out var server))
                throw new FormatException("Missing 'server' parameter");

            if (!parameters.TryGetValue("token", out var token))
                throw new FormatException("Missing 'token' parameter");

            // Validate server URL
            if (!Uri.TryCreate(server, UriKind.Absolute, out _))
                throw new FormatException("Invalid server URL");

            return (server, token);
        }

        public string GetExternalLink(string connectionString, BTCPayNetwork network)
        {
            try
            {
                var parsed = Parse(connectionString);
                if (parsed.Endpoint != null && !string.IsNullOrEmpty(parsed.Endpoint))
                {
                    var uri = new Uri(parsed.Endpoint);
                    // Transform API URL to main website URL
                    var host = uri.Host;
                    if (host.StartsWith("api.", StringComparison.OrdinalIgnoreCase))
                    {
                        host = host.Substring(4); // Remove "api." prefix
                    }
                    return $"{uri.Scheme}://{host}";
                }
            }
            catch
            {
                // Fallback to default if parsing fails
            }
            
            return "https://flashapp.me";
        }

        public string GenerateConnectionString(object options)
        {
            if (options is not FlashOptions flashOptions)
                throw new ArgumentException("Options must be of type FlashOptions", nameof(options));

            if (string.IsNullOrEmpty(flashOptions.Server))
                throw new ArgumentException("Server URL is required", nameof(options));

            if (string.IsNullOrEmpty(flashOptions.Token))
                throw new ArgumentException("Bearer token is required", nameof(options));

            return $"type=flash;server={flashOptions.Server};token={flashOptions.Token}";
        }
    }

    public class FlashOptions
    {
        public string Server { get; set; } = "https://api.flashapp.me/graphql";
        public string Token { get; set; } = string.Empty;
    }
}