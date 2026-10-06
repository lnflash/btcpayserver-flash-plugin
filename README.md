> [!NOTE]
> This plugin serves **Flashcard v1** (Boltcard NFC cards on BTCPay Server).
>
> **Flashcard v2** uses a Cashu-native stack that works offline, with no BTCPay Server:
> - [lnflash/cashu-javacard](https://github.com/lnflash/cashu-javacard): JavaCard applet + NUT-XX spec
> - [lnflash/cashu-client](https://github.com/lnflash/cashu-client): Cashu crypto + mint HTTP client
> - [lnflash/flash](https://github.com/lnflash/flash): backend provisioning API (`cashuCardProvision` mutation)

# BTCPayServer Flash Plugin

Lightning Network plugin for BTCPayServer that uses a Flash account as the store's Lightning node.

## Quick Start

1. **Get an API key** at [console.flashapp.me](https://console.flashapp.me) with the scopes `read_user`, `read_wallet`, `write_wallet` and `read_transactions`. Keys look like `fk_<keyId>_<secret>` and are shown once.
2. **Install**: Server Settings → Plugins → upload `releases/v1.6.5/BTCPayServer.Plugins.Flash.btcpay` (the file must keep that exact name) → restart.
3. **Configure**: Store → Lightning → Setup Lightning Node → Use custom node, with the connection string:

   ```
   type=flash;server=https://api.flashapp.me/graphql;token=fk_...
   ```

Legacy Flash session tokens (`ory_st_...`) still work in `token=`, but they expire; use an API key.

## Features

- ⚡ **Lightning payments**: receive and send through the Flash account, no node to run
- 💵 **Dollar wallet**: invoices are created in the account's USD/USDT cash wallet
- 💳 **Boltcard NFC**: card top-ups and tap-to-pay through BTCPay's Boltcards plugin
- 🔗 **LNURL**: LNURL-pay, LNURL-withdraw and Lightning Address

## How payments are confirmed

Flash transactions carry no payment hash, so the plugin never infers a payment from transaction history:

- **Receiving**: an invoice is reported paid only when Flash's `lnInvoicePaymentStatus` confirms that exact invoice. The amount received comes from the invoice's payment request.
- **Sending**: each payment is recorded when sent, and its status comes from Flash's response or from `lnInvoicePaymentStatus` for that invoice. A payment that stays pending at Flash stays in progress in BTCPay rather than being guessed.

Payment status is polled. API keys cannot authenticate Flash's WebSocket subscriptions.

## Known limitations

- Invoices are tracked in memory. An invoice still waiting for payment when BTCPay restarts is reported unpaid even if it is later paid, and needs manual reconciliation.
- Flash cannot report the status of invoices issued by other Lightning nodes. A payment to one that Flash answers as pending stays in progress until checked by hand.
- Amounts round up to whole cents, so very small invoices can cost the payer up to one cent more than requested.

## Building

The plugin targets .NET 8 and references the BTCPay Server source as `../btcpayserver`. Check out BTCPay Server **v2.1.1** there (later versions target .NET 10), then run `./build-package.sh`. Version numbers live in three places: `BTCPayServer.Plugins.Flash.csproj`, `manifest.json` and `FlashPlugin.cs`.

## Documentation

- [Installation Guide](docs/installation-guide.md)
- [Configuration](docs/configuration.md)
- [API Reference](docs/api-reference.md)
- [Troubleshooting](docs/troubleshooting.md)
- [Development](docs/development.md)
- [Changelog](docs/changelog.md)

## Support

- [Report Issues](https://github.com/lnflash/btcpayserver-flash-plugin/issues)

## License

MIT License
