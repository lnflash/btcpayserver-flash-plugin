# Flash BTCPayServer Plugin

A BTCPayServer plugin for integration with Flash Lightning wallet, including NFC card support for tap-to-pay functionality.

## Features

- **Flash Lightning Integration**: Connect BTCPayServer to your Flash Lightning wallet
- **NFC Card Support**: Register and manage Flash NFC cards for tap-to-pay
- **Card Management**: View card balances, transaction history, and manage permissions
- **Card Top-ups**: Add funds to Flash cards through BTCPayServer

## Installation

1. Download the latest release from the [Releases](https://github.com/lnflash/btcpayserver-plugin-flash/releases) page
2. Copy the plugin files to your BTCPayServer plugins directory
3. Restart BTCPayServer

## Configuration

### 1. Set up Flash Lightning Connection

1. Go to your BTCPayServer store settings
2. Navigate to "Lightning" section
3. Select "Flash" from the connection options
4. Enter your Flash API key and wallet ID

### 2. Register Flash Cards

1. Go to the "Flash Cards" section in the navigation menu
2. Click "Register New Card"
3. Enter card details and scan the NFC card
4. Follow the instructions to program the card

### 3. Configure Card Settings

1. Set spending limits per card
2. Configure automatic top-ups
3. View transaction history

## API Endpoints

The plugin provides the following API endpoints:

- `POST /api/v1/flash-cards/register` - Register a new Flash card
- `POST /api/v1/flash-cards/tap` - Process a card tap payment
- `GET /api/v1/flash-cards/{cardUid}/balance` - Get card balance

## Development

### Building from Source

```bash
cd BTCPayServer.Plugins.Flash
dotnet build
```

### Running Tests

```bash
cd BTCPayServer.Plugins.Flash.Tests
dotnet test
```

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- [BTCPayServer](https://github.com/btcpayserver/btcpayserver) for the core payment server
- [Flash](https://github.com/lnflash/flash) for the Lightning wallet
- [Boltcards](https://github.com/boltcard/boltcard) for NFC card technology inspiration
