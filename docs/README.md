# BTCPayServer Flash Plugin

A Lightning Network plugin for BTCPayServer that integrates with Flash wallets, enabling USD-denominated Lightning payments with enhanced reliability and real-time updates.

## Overview

The BTCPayServer Flash Plugin integrates Flash wallet's Lightning Network capabilities into BTCPayServer, providing merchants with USD-stable Lightning payments, Boltcard NFC support, and seamless payment processing.

## Table of Contents

1. [Installation Guide](#installation-guide)
2. [Configuration](#configuration)
3. [Features](#features)
4. [Troubleshooting](#troubleshooting)
5. [API Reference](#api-reference)
6. [Development](#development)

## Installation Guide

### Prerequisites
- BTCPayServer v2.0.0 or higher
- Flash account with API access
- USD wallet in your Flash account (required for Lightning payments)

### Installation Methods

#### Method 1: Plugin Manager (Recommended)
1. Navigate to Server Settings > Plugins
2. Search for "Flash"
3. Click Install
4. Restart BTCPayServer

#### Method 2: Manual Upload
1. Download the `.btcpay` file from releases
2. Go to Server Settings > Plugins
3. Click "Upload Plugin"
4. Select the downloaded file
5. Restart BTCPayServer

## Configuration

### Basic Setup

1. **Get Your API Token**
   - Open Flash mobile app
   - Settings > Developer > API Access
   - Generate new token
   - Copy the token

2. **Configure in BTCPayServer**
   - Go to your store's Lightning settings
   - Click "Setup Lightning Node"
   - Select "Use custom node"
   - Enter connection string:
     ```
     type=flash;api=https://api.flashapp.me/graphql;api-token=YOUR_TOKEN
     ```
   - Test connection
   - Save

### Advanced Configuration

#### Connection String Parameters
- `type=flash` (required)
- `api=https://api.flashapp.me/graphql` (required - Flash API endpoint)
- `api-token=YOUR_TOKEN` (required - Your Flash API token)

For test environment:
```
type=flash;api=https://api.test.flashapp.me/graphql;api-token=YOUR_TOKEN
```

## Features

### Lightning Payments
- Zero-configuration Lightning node
- Instant payment settlement
- Low fees
- High reliability

### LNURL Support
- **LNURL-pay**: Accept payments via LNURL QR codes
- **LNURL-withdraw**: Enable withdrawals from your store
- **Lightning Address**: yourstore@domain.com support

### Boltcard (NFC Payments)
- Tap-to-pay with NFC cards
- Balance top-up functionality
- Real-time balance checking
- Transaction history

### USD Wallet Support
- Accept Lightning payments in USD
- Automatic BTC/USD conversion at current exchange rates
- Stable value for accounting
- Minimum transaction: 1 cent USD

### WebSocket Updates
- Real-time payment notifications
- Reduced server load
- Instant UI updates
- Automatic reconnection with exponential backoff
- Connection health monitoring

## Troubleshooting

### Common Issues

#### Connection Failed
**Error**: "Failed to connect to Flash API"

**Solutions**:
1. Verify API token is correct
2. Check internet connectivity
3. Ensure Flash API is accessible
4. Try regenerating API token

#### Payment Not Detected
**Error**: "Invoice shows unpaid after payment"

**Solutions**:
1. Check WebSocket connection status
2. Verify wallet has sufficient balance
3. Ensure invoice hasn't expired
4. Check Flash app for payment status

#### Boltcard Not Working
**Error**: "Card tap not recognized"

**Solutions**:
1. Ensure NFC plugin is installed
2. Verify card is properly programmed
3. Check NFC reader compatibility
4. Test with Flash mobile app

### Debug Mode

Enable debug logging:
1. Add to appsettings.json:
```json
{
  "Logging": {
    "LogLevel": {
      "BTCPayServer.Plugins.Flash": "Debug"
    }
  }
}
```

### Log Locations
- Linux: `/var/log/btcpayserver/`
- Docker: `docker logs btcpayserver`
- Windows: `%APPDATA%\BTCPayServer\Logs`

## API Reference

### Lightning Client Methods

#### Create Invoice
```csharp
CreateInvoice(amount, description, expiry)
```

#### Pay Invoice
```csharp
Pay(bolt11, amount)
```

#### Get Balance
```csharp
GetBalance()
```

### Service Interfaces

#### IFlashInvoiceService
- CreateInvoiceAsync()
- GetInvoiceAsync()
- CancelInvoiceAsync()

#### IFlashPaymentService
- PayInvoiceAsync()
- GetPaymentStatusAsync()

#### IFlashWalletService
- GetWalletInfoAsync()
- GetBalanceAsync()

## Development

### Building from Source

1. **Clone Repository**
```bash
git clone https://github.com/Island-Bitcoin/BTCPayServer.Plugins.Flash.git
cd BTCPayServer.Plugins.Flash
```

2. **Build Plugin**
```bash
dotnet build
```

3. **Run Tests**
```bash
dotnet test
```

### Development Setup

1. **Local BTCPayServer**
```bash
git clone https://github.com/btcpayserver/btcpayserver.git
cd btcpayserver
docker-compose up
```

2. **Plugin Development**
- Place plugin in `btcpayserver/BTCPayServer.Plugins/`
- Run with: `dotnet run --launch-profile Docker`

### Contributing

1. Fork the repository
2. Create feature branch
3. Commit changes
4. Push to branch
5. Create Pull Request

### Code Style
- Follow C# conventions
- Use async/await for I/O
- Add XML documentation
- Include unit tests

## Version History

### v1.5.1 (Latest)
- Enhanced WebSocket stability with exponential backoff reconnection
- Added connection state management to prevent duplicate connections
- Implemented WebSocket health metrics and monitoring
- Added ping/pong keep-alive mechanism
- Improved error handling for abrupt disconnections
- Created modular retry policy system
- Fixed "remote party closed connection" errors

### v1.5.0
- Major UI cleanup for fresh rebuild
- Temporarily removed UI elements for comprehensive redesign
- Core Lightning functionality remains fully operational

### v1.4.2
- Critical: Made plugin domain-agnostic - now works on any BTCPay Server instance
- Fixed hardcoded WebSocket endpoints - dynamically derived from API configuration
- Fixed external link generation to use configured Flash instance
- Improved support for custom Flash API deployments

### v1.4.1
- Fixed Boltcard payment detection with proper payment hash extraction
- Fixed pull payment amount interpretation (satoshis vs USD)
- Added payment caching for immediate detection
- Implemented PayInvoiceParams and ListPaymentsAsync methods
- Added comprehensive error handling and connection validation
- Improved minimum amount validation with clear error messages

### v1.3.6
- Fixed LNURL amount handling
- Improved transaction monitoring
- Better error messages

### v1.3.5
- Initial stable release
- Basic Lightning support
- LNURL implementation

## Support

- **GitHub Issues**: [Report bugs](https://github.com/Island-Bitcoin/BTCPayServer.Plugins.Flash/issues)
- **Discussions**: [Ask questions](https://github.com/Island-Bitcoin/BTCPayServer.Plugins.Flash/discussions)
- **Chat**: [BTCPay Mattermost](https://chat.btcpayserver.org)

## License

MIT License - see LICENSE file for details.