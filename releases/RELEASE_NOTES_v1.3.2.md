# Flash Plugin v1.3.2 Release Notes

## Boltcard Compatibility Improvements

- Fixed LNURL payments with Boltcards plugin
- Added minimum payment threshold (100 sats) for small LNURL payments
- Improved currency conversion precision for better compatibility
- Enhanced exchange rate handling with additional fallbacks
- Fixed amount mismatch issues with LNURL payments

## Changes

- Added robust error handling for different payment amounts
- Improved logging for payment tracking and diagnostics
- Enhanced rounding precision for USD/BTC conversions
- Fixed issue with very small payment amounts being rejected

## Documentation

- Updated LNURL testing guide with Boltcard-specific guidance
- Added troubleshooting steps for common integration issues
