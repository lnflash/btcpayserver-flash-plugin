#!/bin/bash
# Release gate run by CI on v* tags. Fails the build when the tag does not match
# the version compiled into the package, or when CHANGELOG.md has no entry for it.
# Usage: release-check.sh <tag> [package-dir] [changelog] [out-file]
set -euo pipefail

TAG="${1:?usage: release-check.sh <tag> [package-dir] [changelog] [out-file]}"
PACKAGE_DIR="${2:-bin/Release/package/BTCPayServer.Plugins.Flash}"
CHANGELOG="${3:-CHANGELOG.md}"
OUT="${4:-release-notes.md}"
VER="${TAG#v}"

# PluginPacker writes <name>/<Version>/<name>.btcpay, where <Version> is the compiled
# assembly version, so this one check covers both the csproj and FlashPlugin.cs.
if [ ! -d "$PACKAGE_DIR/$VER" ]; then
    echo "Tag ${TAG} does not match packaged version $(ls "$PACKAGE_DIR" 2>/dev/null || echo '<none>')" >&2
    exit 1
fi

sed -n "/^## \[${VER}\]/,/^## \[/p" "$CHANGELOG" | sed '$d' > "$OUT"
if [ ! -s "$OUT" ]; then
    echo "No CHANGELOG entry for ${VER}" >&2
    exit 1
fi
echo "Release notes for ${VER} written to ${OUT}"
