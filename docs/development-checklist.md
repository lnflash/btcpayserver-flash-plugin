# Flash Plugin Development Checklist

This checklist helps ensure consistent and correct development of the Flash plugin for BTCPayServer. It covers all essential steps for making changes to the plugin and preparing releases.

## Version Management (CRITICAL)

- [ ] Update version number in **all three** required locations:
  - [ ] `FlashPlugin.cs`: `public override Version Version => new Version(x, y, z);`
  - [ ] `manifest.json`: `"version": "x.y.z"`
  - [ ] `BTCPayServer.Plugins.Flash.csproj`: `<Version>x.y.z</Version>`

⚠️ **WARNING**: Failing to update all three version locations will result in incorrect version reporting in BTCPayServer.

## Code Development

- [ ] Ensure all code changes are consistent with the existing code style and patterns
- [ ] Add appropriate logging (with proper prefixes like `[PAYMENT DEBUG]`)
- [ ] Include error handling with detailed diagnostic information
- [ ] Ensure fallback mechanisms for API limitations
- [ ] Add XML documentation to public methods
- [ ] Update or add tests if applicable

## Build and Package

- [ ] Run the build script: `./build-package.sh`
- [ ] Verify build completes without errors (warnings are acceptable)
- [ ] Check the output package: `bin/Release/BTCPayServer.Plugins.Flash.btcpay`
- [ ] Copy the package to the releases folder with version name:
  - [ ] `cp bin/Release/BTCPayServer.Plugins.Flash.btcpay releases/BTCPayServer.Plugins.Flash-vX.Y.Z.btcpay`

## Documentation Updates

- [ ] Create release notes: `releases/RELEASE_NOTES_vX.Y.Z.md`
- [ ] Update `technical-implementation.md` with new features or changes
- [ ] Add new endpoints or features to user documentation
- [ ] Update installation guide if necessary

## Testing

- [ ] Install the new package in a development BTCPayServer instance
- [ ] Verify the correct version appears in logs:
  ```
  info: BTCPayServer.Plugins.PluginManager: Adding and executing plugin BTCPayServer.Plugins.Flash - X.Y.Z
  ```
- [ ] Test all affected functionality:
  - [ ] LNURL and Lightning Address support
  - [ ] Invoice creation and payment
  - [ ] Pull payment processing
  - [ ] Boltcard topup (if applicable)
- [ ] Test error scenarios and fallback mechanisms
- [ ] Test mobile wallet compatibility

## Release Preparation

- [ ] Update `CHANGELOG.md` with release notes
- [ ] Create a Git tag for the release: `git tag vX.Y.Z`
- [ ] Push tag to remote: `git push origin vX.Y.Z`
- [ ] Create a GitHub release with:
  - [ ] Release notes
  - [ ] Package attached as asset

## Installation Verification

After release, verify installation on a production server:

- [ ] Upload the package through BTCPayServer admin UI
- [ ] Restart the server
- [ ] Check logs for correct version loading
- [ ] Test basic functionality to ensure it works as expected

## Troubleshooting Steps

If the plugin doesn't load or shows the wrong version:

1. Check all three version locations to ensure they match
2. Try manual installation:
   ```bash
   ./direct-install.sh
   ```
3. Check BTCPayServer logs for any error messages
4. Verify all required files are in the plugin directory