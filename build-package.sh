#!/bin/bash
# Builds BTCPayServer.Plugins.Flash.btcpay the same way the BTCPay Plugin Builder does:
# dotnet publish, then BTCPay's PluginPacker. Requires the submodule to be checked out:
#   git submodule update --init --depth 1
set -euo pipefail

cd "$(dirname "$0")"

CONFIG="${BUILD_CONFIG:-Release}"
PROJECT=BTCPayServer.Plugins.Flash
PACKER_SRC=submodules/btcpayserver/BTCPayServer.PluginPacker
OUT=bin/$CONFIG
PUBLISH_DIR=$OUT/publish
PACKER_DIR=$OUT/packer
PACKAGE_DIR=$OUT/package

[ -f "$PACKER_SRC/BTCPayServer.PluginPacker.csproj" ] || {
    echo "submodules/btcpayserver is empty. Run: git submodule update --init --depth 1" >&2
    exit 1
}

rm -rf "$PUBLISH_DIR" "$PACKER_DIR" "$PACKAGE_DIR"

dotnet restore "$PROJECT.csproj" --property:Configuration="$CONFIG"
dotnet publish "$PROJECT.csproj" --configuration "$CONFIG" --no-restore --output "$PUBLISH_DIR"

# Always rebuild the packer from the pinned submodule so a submodule bump can never
# leave a stale packer behind (the dir is wiped above). Incremental build is cheap.
dotnet build "$PACKER_SRC/BTCPayServer.PluginPacker.csproj" -c Release -o "$PACKER_DIR"

"$PACKER_DIR/BTCPayServer.PluginPacker" "$PUBLISH_DIR" "$PROJECT" "$PACKAGE_DIR"

# PluginPacker writes <version>/<name>.btcpay; expose it at a stable path for releases.
PKG=$(find "$PACKAGE_DIR" -name "$PROJECT.btcpay" -print -quit)
[ -n "$PKG" ] || { echo "PluginPacker produced no $PROJECT.btcpay under $PACKAGE_DIR" >&2; exit 1; }
cp "$PKG" "$OUT/$PROJECT.btcpay"
echo "Plugin package created at $OUT/$PROJECT.btcpay"
