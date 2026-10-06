<div align="center">

<img src="wwwroot/img/icons/flash.svg" width="72" alt="Flash">

# Flash for BTCPay Server

**Use a Flash account as your store's Lightning node.**
No node to run, no channels to manage. Payments settle in the account's dollar wallet.

![release](https://img.shields.io/github/v/release/lnflash/btcpayserver-flash-plugin?color=6B46C1)
![BTCPay Server](https://img.shields.io/badge/BTCPay_Server-2.x-51B13E)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![license](https://img.shields.io/badge/license-MIT-blue)

</div>

> [!NOTE]
> This plugin powers **Flashcard v1** (Boltcard NFC cards on BTCPay Server).
> **Flashcard v2** runs on a Cashu stack that works offline, with no BTCPay Server:
> [cashu-javacard](https://github.com/lnflash/cashu-javacard) · [cashu-client](https://github.com/lnflash/cashu-client) · [flash](https://github.com/lnflash/flash)

## How it fits

```mermaid
flowchart LR
    C["📱 Customer wallet<br/>or 💳 Boltcard"] -- Lightning --> F
    subgraph BTCPay Server
      S[Store] --> P[Flash plugin]
    end
    P -- "GraphQL + API key" --> F["⚡ Flash<br/>api.flashapp.me"]
    F --> W[("💵 Cash wallet<br/>USD / USDT")]
```

## Quick start

| | Step |
|---|---|
| 1 | Create an API key at **[console.flashapp.me](https://console.flashapp.me)** with scopes `read_user` `read_wallet` `write_wallet` `read_transactions` |
| 2 | Download `BTCPayServer.Plugins.Flash.btcpay` from **[Releases](https://github.com/lnflash/btcpayserver-flash-plugin/releases/latest)** and upload it in *Server Settings → Plugins*, then restart |
| 3 | In *Store → Lightning → Use custom node*, enter:<br/>`type=flash;server=https://api.flashapp.me/graphql;token=fk_...` |

Full walkthrough: **[docs/installation.md](docs/installation.md)**

## What it does

| | |
|---|---|
| ⚡ **Receive** | Lightning invoices from the account's dollar wallet, priced in cents |
| 📤 **Send** | Payouts and refunds paid from the same wallet |
| 💳 **Boltcards** | Card top-ups and tap-to-pay through BTCPay's Boltcards plugin |
| 🔗 **LNURL** | LNURL-pay, LNURL-withdraw and Lightning Address |
| ✅ **Exact confirmation** | An invoice is marked paid only when Flash confirms *that* invoice. Nothing is inferred from transaction history |

## Documentation

| Guide | |
|---|---|
| [Installation](docs/installation.md) | API key, install, connection string, updating |
| [How it works](docs/how-it-works.md) | Payment flows, how payments are confirmed, limitations |
| [Troubleshooting](docs/troubleshooting.md) | Common errors and what they mean |
| [Development](docs/development.md) | Repository layout, building, releasing |
| [Changelog](CHANGELOG.md) | Release history |

## License

MIT
