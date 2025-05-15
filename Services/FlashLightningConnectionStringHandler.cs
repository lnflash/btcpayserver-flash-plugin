using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Flash.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace BTCPayServer.Plugins.Flash.Services;

public class FlashLightningConnectionStringHandler : ILightningConnectionStringHandler
{
    private readonly ILogger<FlashLightningConnectionStringHandler> _logger;
    private readonly FlashClientProvider _flashClientProvider;

    public FlashLightningConnectionStringHandler(
        ILogger<FlashLightningConnectionStringHandler> logger,
        FlashClientProvider flashClientProvider)
    {
        _logger = logger;
        _flashClientProvider = flashClientProvider;
    }

    // This is the identifier that will be used in the connection string format
    public string ConnectionStringName => "flash";

    // Method to parse connection string and initialize a Lightning client
    public ILightningClient Create(string connectionString, BTCPayNetwork network)
    {
        // Parse the connection string to extract Flash-specific parameters
        var dict = LightningConnectionStringHelper.ExtractValues(connectionString, out var type);
        
        if (type != "flash")
        {
            throw new FormatException("Invalid connection string type");
        }

        if (!dict.TryGetValue("server", out var server))
        {
            server = "https://api.flashapp.me/graphql";
        }

        if (!dict.TryGetValue("token", out var token))
        {
            throw new FormatException("Flash connection string needs a 'token' parameter");
        }

        // Create a Flash client with the provided token and server
        var settings = new FlashSettings
        {
            ApiUrl = server,
            BearerToken = token
        };

        var client = _flashClientProvider.GetClient(settings);
        
        if (client == null)
        {
            throw new InvalidOperationException("Failed to create Flash client");
        }

        return client;
    }

    // Validate if the connection string is supported by this handler
    public bool CanHandle(string connectionString)
    {
        try
        {
            LightningConnectionStringHelper.ExtractValues(connectionString, out var type);
            return type == "flash";
        }
        catch
        {
            return false;
        }
    }

    // Generate a sample connection string that can be used in the UI
    public Task<string> GetLightningNodeInfo(
        HostString host, 
        string connectionString, 
        BTCPayNetwork network, 
        CancellationToken cancellation)
    {
        if (!CanHandle(connectionString))
        {
            throw new FormatException("Invalid Flash connection string");
        }

        return Task.FromResult($"Flash Lightning node on {network.CryptoCode}");
    }

    // Provide a hint for the connection string format to display in the UI
    public string GetHint()
    {
        return "flash://api.flashapp.me?token=your-flash-token";
    }

    // Get link to relevant documentation/website
    public string GetExternalLink()
    {
        return "https://flashapp.me";
    }

    // Returns true if the host should be part of the connection string
    public bool RequiresHost() => false;
    
    // Unique display name for the Lightning implementation
    public string DisplayName => "Flash";

    // Should be true if we can open/close channels with this implementation
    public bool SupportsPayingInvoiceWithoutAmountAndExpiry => false;

    // Whether this provider is enabled for the network
    public bool IsSupported(BTCPayNetwork network) => network.CryptoCode == "BTC";
}