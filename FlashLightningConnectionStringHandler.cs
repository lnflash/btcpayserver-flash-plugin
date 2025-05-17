#nullable enable
using System;
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
                var flashConnectionString = Parse(connectionString);
                error = null;
                return new FlashLightningClient(
                    flashConnectionString.BearerToken,
                    new Uri(flashConnectionString.Endpoint),
                    _loggerFactory.CreateLogger<FlashLightningClient>());
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

            var flashConnectionString = Parse(connectionString);
            return new FlashLightningClient(
                flashConnectionString.BearerToken,
                new Uri(flashConnectionString.Endpoint),
                _loggerFactory.CreateLogger<FlashLightningClient>());
        }

        public async Task<object> GetLightningClient(string connectionString, BTCPayNetwork network, CancellationToken cancellation)
        {
            if (connectionString == null)
                throw new ArgumentNullException(nameof(connectionString));

            if (!CanHandle(connectionString))
                throw new ArgumentException("Invalid connection string", nameof(connectionString));

            try
            {
                var flashConnectionString = Parse(connectionString);
                var client = new FlashLightningClient(
                    flashConnectionString.BearerToken,
                    new Uri(flashConnectionString.Endpoint),
                    _loggerFactory.CreateLogger<FlashLightningClient>());

                // Verify the connection works by calling GetInfo
                await client.GetInfo(cancellation);

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