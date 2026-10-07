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

rm -rf "$PUBLISH_DIR" "$PACKAGE_DIR"

dotnet restore "$PROJECT.csproj" --property:Configuration="$CONFIG"
dotnet publish "$PROJECT.csproj" --configuration "$CONFIG" --no-restore --output "$PUBLISH_DIR"

if [ ! -x "$PACKER_DIR/BTCPayServer.PluginPacker" ]; then
    dotnet build "$PACKER_SRC/BTCPayServer.PluginPacker.csproj" -c Release -o "$PACKER_DIR"
fi

# The packer targets .NET 8; let it run on whatever newer runtime the SDK image ships.
DOTNET_ROLL_FORWARD=Major "$PACKER_DIR/BTCPayServer.PluginPacker" "$PUBLISH_DIR" "$PROJECT" "$PACKAGE_DIR"

# PluginPacker writes <version>/<name>.btcpay; expose it at a stable path for releases.
PKG=$(find "$PACKAGE_DIR" -name "$PROJECT.btcpay" | head -1)
cp "$PKG" "$OUT/$PROJECT.btcpay"
echo "Plugin package created at $OUT/$PROJECT.btcpay"
