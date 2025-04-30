# Flash BTCPayServer Plugin Documentation

This directory contains technical documentation for the Flash BTCPayServer Plugin.

## Available Documents

### Implementation and Architecture
- [**Implementation Options**](implementation-options.md) - Detailed overview of authentication approaches for connecting BTCPayServer to Flash
- [**Security Analysis: JWT vs API Keys**](security-analysis-jwt-vs-apikeys.md) - In-depth security and architectural analysis of authentication options

## Integration Instructions

### Setting Up Flash with BTCPayServer

1. **Installation**:
   - In BTCPayServer, go to Server Settings > Plugins
   - Click "Upload Plugin" and select the Flash plugin file
   - Install and restart BTCPayServer

2. **Configuration**:
   - Go to your Store Settings > Lightning
   - Click "Add Lightning Node"
   - Select "Flash" from the connection type dropdown
   - Enter your Flash credentials as specified in the form
   - Test connection and save

3. **Usage**:
   - Flash will now be available as a Lightning payment option
   - Flash Cards functionality can be accessed via the Flash Cards menu in your store

### Flash Card Management

Flash cards integration allows your customers to use NFC cards for payments:

1. **Registration**:
   - Go to Flash Cards > Register New Card
   - Scan an NFC card or enter its UID
   - Create a pull payment to fund the card
   - Complete the registration process

2. **Monitoring**:
   - View card usage and transaction history
   - Block/unblock cards as needed
   - Manage card balances through the pull payment system

## Development Notes

### Building from Source

To build the plugin from source:

```bash
dotnet build -c Release
dotnet run --project [Path to BTCPayServer.PluginPacker] -- [Path to compiled plugin] BTCPayServer.Plugins.Flash [Output directory]
```

### Running Tests

The plugin includes both unit and integration tests:

```bash
dotnet test
```

## Support

For issues related to the plugin, please submit a GitHub issue with:
1. Detailed description of the problem
2. Steps to reproduce
3. BTCPayServer and plugin versions
4. Any relevant log output (redacted of sensitive information)

## Security Considerations

See [security-analysis-jwt-vs-apikeys.md](security-analysis-jwt-vs-apikeys.md) for important security information related to this plugin.