> [!WARNING]
> **This repository is deprecated and no longer maintained.**
>
> Flash NFC card payments have moved to a Cashu-native architecture that works offline — no BTCPay Server required.
>
> **Replacement stack:**
> - [lnflash/cashu-javacard](https://github.com/lnflash/cashu-javacard) — JavaCard applet + NUT-XX spec
> - [lnflash/cashu-client](https://github.com/lnflash/cashu-client) — Cashu crypto + mint HTTP client
> - [lnflash/flash](https://github.com/lnflash/flash) — Backend provisioning API (`cashuCardProvision` mutation)
>
> This repository is archived for historical reference. History is preserved.

---

# BTCPayServer Flash Plugin

Lightning Network plugin for BTCPayServer that integrates Flash wallet capabilities.

## Quick Start

1. **Install**: Server Settings → Plugins → Search "Flash" → Install
2. **Configure**: Store → Lightning → Setup Lightning Node → Use custom node
3. **Connection string**: `type=flash;api=https://api.flashapp.me/graphql;api-token=YOUR_TOKEN`

Get your API token from Flash mobile app: Settings → Developer → API Access


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
- [Changelog](docs/changelog.md)

## Support

- [Report Issues](https://github.com/Island-Bitcoin/BTCPayServer.Plugins.Flash/issues)
- [Discussions](https://github.com/Island-Bitcoin/BTCPayServer.Plugins.Flash/discussions)

## License

MIT License
