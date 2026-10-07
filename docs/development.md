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

The project targets **.NET 10** and references the BTCPay Server source through the git submodule at `submodules/btcpayserver`, pinned to **v2.4.5**. Any BTCPay 2.4.x tag works; 2.3 and earlier target .NET 8 and will not build this project (use the `v1.6.6` tag for those).

```sh
git clone --recurse-submodules https://github.com/lnflash/btcpayserver-flash-plugin.git
cd btcpayserver-flash-plugin
./build-package.sh        # → bin/Release/BTCPayServer.Plugins.Flash.btcpay
```

If you cloned without `--recurse-submodules`: `git submodule update --init --depth 1`.

`build-package.sh` runs the same steps as the BTCPay Plugin Builder: `dotnet restore`, `dotnet publish`, then BTCPay's `PluginPacker`, which reads `Identifier`, `Version` and `Dependencies` from the compiled `FlashPlugin` class and writes the `.btcpay` next to a `.btcpay.json` manifest. `manifest.json` in the repository root is informational; the packer does not read it.

Without a local .NET 10 SDK, build in Docker:

```sh
docker run --rm -v "$PWD:/build/repo" -w /build/repo \
  -e HOME=/build/home -e DOTNET_CLI_HOME=/build/home \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -c 'mkdir -p /build/home && ./build-package.sh'
```

### Smoke test on a disposable BTCPay

```sh
mkdir -p /tmp/flash-plugin/BTCPayServer.Plugins.Flash
unzip bin/Release/BTCPayServer.Plugins.Flash.btcpay -d /tmp/flash-plugin/BTCPayServer.Plugins.Flash
docker network create flash-smoke
docker run -d --name smoke-pg --network flash-smoke -e POSTGRES_HOST_AUTH_METHOD=trust postgres:16
docker run -d --name smoke-btcpay --network flash-smoke -p 127.0.0.1:14142:49392 \
  -v /tmp/flash-plugin:/root/.btcpayserver/Plugins \
  -e BTCPAY_NETWORK=regtest -e BTCPAY_BIND=0.0.0.0:49392 -e BTCPAY_CHAINS=btc \
  -e BTCPAY_POSTGRES="User ID=postgres;Host=smoke-pg;Port=5432;Database=btcpay" \
  -e BTCPAY_BTCEXPLORERURL=http://nbx:32838/ \
  btcpayserver/btcpayserver:2.4.5
docker logs smoke-btcpay 2>&1 | grep -i "plugin"   # expect "Running plugin BTCPayServer.Plugins.Flash - <version>"
```

Then open http://127.0.0.1:14142. NBXplorer is intentionally absent; the Lightning settings page is enough to check the plugin registered.

## Plugin Builder

To publish through [plugin-builder.btcpayserver.org](https://plugin-builder.btcpayserver.org), the plugin entry's settings are:

| Setting | Value |
|---|---|
| Git repository | `https://github.com/lnflash/btcpayserver-flash-plugin` |
| Git ref | the release tag, e.g. `v1.7.0` |
| Plugin directory | *(empty, the csproj is at the root)* |
| Build configuration | `Release` |

The builder does a shallow recursive clone and runs `dotnet publish` in a `dotnet/sdk:10.0` image with network access limited to nuget.org, which this layout satisfies. See the [BTCPay publishing guide](https://docs.btcpayserver.org/Developers/plugins/publishing/) for account verification and the listing request.

## Releasing

1. **Bump the version in all three places.** BTCPay reads the version from code, not only from the manifest:

   | File | Field |
   |---|---|
   | `BTCPayServer.Plugins.Flash.csproj` | `<Version>` |
   | `manifest.json` | `"version"` |
   | `FlashPlugin.cs` | `override Version` |

2. Add an entry to [CHANGELOG.md](../CHANGELOG.md).
3. Push the tag. CI ([`.github/workflows/build.yml`](../.github/workflows/build.yml)) builds the package in the same `dotnet/sdk:10.0` image the Plugin Builder uses, runs `release-check.sh` (the build fails if the tag does not match the compiled version or CHANGELOG has no entry for it), creates the GitHub release (notes from the matching CHANGELOG section) and attaches `BTCPayServer.Plugins.Flash.btcpay` plus its `.btcpay.json` manifest. The asset name must stay exactly `BTCPayServer.Plugins.Flash.btcpay` (see [Installation](installation.md#2-install-the-plugin)):

   ```sh
   git tag v1.7.0 && git push origin v1.7.0
   ```

   Do not upload a laptop-built package to the release; the CI artifact is the one users install, so what ships is reproducible from the tag.

Packages are not committed to the repository. `bin/` and `obj/` are ignored.

## Testing changes

Payment handling should be checked against the real Flash API with an API key, because the test environment and production can differ in wallet setup (for example the USDT cash wallet). For any change to receiving or sending, check at least:

- a new invoice reports **unpaid**, and stays unpaid if anything else claims it was paid;
- a paid invoice reports **paid**, with the amount from its payment request;
- a send reports **Complete**, **Failed** or **Pending** to match what Flash says, and an unknown payment stays **Pending**.

Then do a top-up and a spend with a real Boltcard against a BTCPay instance.
