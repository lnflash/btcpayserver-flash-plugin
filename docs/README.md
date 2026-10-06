# BTCPayServer Flash Plugin

Lightning Network plugin for BTCPayServer that integrates Flash wallet capabilities.

## Quick Start

1. **Install**: Server Settings → Plugins → upload `BTCPayServer.Plugins.Flash.btcpay` from `releases/` → restart
2. **Configure**: Store → Lightning → Setup Lightning Node → Use custom node
3. **Connection string**: `type=flash;server=https://api.flashapp.me/graphql;token=fk_...`

Get an API key at [console.flashapp.me](https://console.flashapp.me) with the scopes `read_user`, `read_wallet`, `write_wallet` and `read_transactions`.

## Features

- ⚡ **Lightning Payments** - Zero-configuration Lightning node
- 💵 **USD Wallet** - Accept payments in USD with automatic BTC conversion  
- 💳 **Boltcard NFC** - Tap-to-pay with NFC cards
- 🔗 **LNURL Support** - Full LNURL-pay, withdraw, and Lightning Address
- 🔄 **Real-time Updates** - WebSocket notifications for instant payment detection

## Documentation

- [Installation Guide](docs/installation-guide.md)
- [Configuration](docs/configuration.md)
- [API Reference](docs/api-reference.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Development](docs/development.md)
- [Changelog](CHANGELOG.md)

## Support

- [Report Issues](https://github.com/lnflash/btcpayserver-flash-plugin/issues)

## License

MIT License