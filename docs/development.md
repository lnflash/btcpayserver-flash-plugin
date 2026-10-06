# Development

## Repository layout

```text
.
├── FlashPlugin.cs              Plugin entry point: registers services and the connection string handler
├── FlashPlugin*.cs             Initializer, extensions, logger
├── Lightning/                  BTCPay's ILightningClient for Flash, and the connection string parser
├── Services/                   Flash API client, invoices, payments, auth headers, polling
├── Models/                     LNURL, pull payment and payout models
├── Controllers/                LNURL-pay endpoints for flashcards
├── Data/                       Payout tracking database (EF Core)
├── Exceptions/
├── Views/  wwwroot/            Embedded views and the plugin icon
├── docs/
├── build-package.sh            Builds the .btcpay package
├── manifest.json               Plugin identity and version
└── BTCPayServer.Plugins.Flash.csproj
```

Start with `Lightning/FlashLightningClient.cs`, which BTCPay calls. It delegates to `Services/FlashInvoiceService.cs` (receiving) and `Services/FlashPaymentService.cs` (sending). Every Flash request gets its headers from `Services/FlashAuth.cs`.

## Building

The project targets **.NET 8** and references the BTCPay Server source at `../btcpayserver`. Use BTCPay Server **v2.1.1**; later versions target .NET 10 and won't build this project. A plugin built against 2.1.1 runs on current BTCPay 2.x.

```sh
git clone --depth 1 --branch v2.1.1 https://github.com/btcpayserver/btcpayserver.git ../btcpayserver
./build-package.sh        # → bin/Release/BTCPayServer.Plugins.Flash.btcpay
```

Without a local .NET 8 SDK, build in Docker from the parent directory:

```sh
docker run --rm \
  -v "$PWD/btcpayserver:/src/btcpayserver" \
  -v "$PWD/btcpayserver-flash-plugin:/src/plugin" \
  -w /src/plugin mcr.microsoft.com/dotnet/sdk:8.0 \
  bash -c 'apt-get -qq update && apt-get -qq install -y zip && ./build-package.sh'
```

## Releasing

1. **Bump the version in all three places.** BTCPay reads the version from code, not only from the manifest:

   | File | Field |
   |---|---|
   | `BTCPayServer.Plugins.Flash.csproj` | `<Version>` |
   | `manifest.json` | `"version"` |
   | `FlashPlugin.cs` | `override Version` |

2. Add an entry to [CHANGELOG.md](../CHANGELOG.md).
3. Build the package, then publish it as a GitHub release. Keep the asset name exactly `BTCPayServer.Plugins.Flash.btcpay` (see [Installation](installation.md#2-install-the-plugin)):

   ```sh
   gh release create v1.6.6 bin/Release/BTCPayServer.Plugins.Flash.btcpay \
     --title "v1.6.6" --notes-file <(sed -n '/## \[1.6.6\]/,/## \[/p' CHANGELOG.md | sed '$d')
   ```

Packages are not committed to the repository. `bin/` and `obj/` are ignored.

## Testing changes

Payment handling should be checked against the real Flash API with an API key, because the test environment and production can differ in wallet setup (for example the USDT cash wallet). For any change to receiving or sending, check at least:

- a new invoice reports **unpaid**, and stays unpaid if anything else claims it was paid;
- a paid invoice reports **paid**, with the amount from its payment request;
- a send reports **Complete**, **Failed** or **Pending** to match what Flash says, and an unknown payment stays **Pending**.

Then do a top-up and a spend with a real Boltcard against a BTCPay instance.
