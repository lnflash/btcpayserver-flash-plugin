#!/bin/bash
# Tests for release-check.sh. Run: ./release-check.test.sh
set -uo pipefail
cd "$(dirname "$0")" || exit 1

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT
PKG="$TMP/package/BTCPayServer.Plugins.Flash"
mkdir -p "$PKG/1.7.0"
touch "$PKG/1.7.0/BTCPayServer.Plugins.Flash.btcpay"
cat > "$TMP/CHANGELOG.md" <<'MD'
# Changelog

## [1.7.0] - 2026-10-07

### Changed
- Something.

## [1.6.6] - 2026-10-06

### Fixed
- Older thing.
MD

# release-check.sh also reads the csproj and manifest.json; point it at fixtures.
export CSPROJ="$TMP/plugin.csproj" MANIFEST="$TMP/manifest.json"
printf '<Project><PropertyGroup><Version>1.7.0</Version></PropertyGroup></Project>\n' > "$CSPROJ"
printf '{ "version": "1.7.0" }\n' > "$MANIFEST"

fail=0
check() { if [ "$1" -eq 0 ]; then echo "ok   - $2"; else echo "FAIL - $2"; fail=1; fi; }

# 1. Matching tag + CHANGELOG entry: passes and extracts only that section.
./release-check.sh v1.7.0 "$PKG" "$TMP/CHANGELOG.md" "$TMP/notes.md" >/dev/null
check $? "matching tag passes"
grep -q "Something." "$TMP/notes.md"; check $? "notes contain 1.7.0 body"
if grep -q "Older thing" "$TMP/notes.md"; then r=1; else r=0; fi; check $r "notes exclude 1.6.6 body"
if grep -q "## \[1.6.6\]" "$TMP/notes.md"; then r=1; else r=0; fi; check $r "notes exclude next heading"

# 2. Tag does not match packaged version: fails.
if ./release-check.sh v1.7.1 "$PKG" "$TMP/CHANGELOG.md" "$TMP/notes2.md" 2>"$TMP/err2"; then r=1; else r=0; fi
check $r "mismatched tag fails"
grep -q "does not match packaged version 1.7.0" "$TMP/err2"; check $? "mismatch error names packaged version"

# 3. Tag matches package, csproj and manifest, but CHANGELOG has no entry: fails.
mkdir -p "$PKG/1.8.0"
printf '<Project><PropertyGroup><Version>1.8.0</Version></PropertyGroup></Project>\n' > "$CSPROJ"
printf '{ "version": "1.8.0" }\n' > "$MANIFEST"
if ./release-check.sh v1.8.0 "$PKG" "$TMP/CHANGELOG.md" "$TMP/notes3.md" 2>"$TMP/err3"; then r=1; else r=0; fi
check $r "missing CHANGELOG entry fails"
grep -q "No CHANGELOG entry for 1.8.0" "$TMP/err3"; check $? "missing-entry error names version"
printf '<Project><PropertyGroup><Version>1.7.0</Version></PropertyGroup></Project>\n' > "$CSPROJ"
printf '{ "version": "1.7.0" }\n' > "$MANIFEST"

# 4. Package and CHANGELOG agree but the csproj was not bumped: fails.
printf '<Project><PropertyGroup><Version>1.6.6</Version></PropertyGroup></Project>\n' > "$CSPROJ"
if ./release-check.sh v1.7.0 "$PKG" "$TMP/CHANGELOG.md" "$TMP/notes4.md" 2>"$TMP/err4"; then r=1; else r=0; fi
check $r "stale csproj version fails"
grep -q "<Version> does not match" "$TMP/err4"; check $? "csproj error names the file"
printf '<Project><PropertyGroup><Version>1.7.0</Version></PropertyGroup></Project>\n' > "$CSPROJ"

# 5. Same for manifest.json.
printf '{ "version": "1.6.6" }\n' > "$MANIFEST"
if ./release-check.sh v1.7.0 "$PKG" "$TMP/CHANGELOG.md" "$TMP/notes5.md" 2>"$TMP/err5"; then r=1; else r=0; fi
check $r "stale manifest.json version fails"
printf '{ "version": "1.7.0" }\n' > "$MANIFEST"

exit $fail
