# Flash Plugin v1.3.4 Release Notes

## Boltcard Compatibility Fix

- Fixed "incorrect amount" errors when using Boltcards with Flash
- Added special handling for Boltcard topups 
- Relaxed amount verification for Boltcard transactions
- Enhanced LNURL detection and processing

## Technical Changes

- Added a dedicated BoltcardPatch hook to handle LNURL amount verification
- Implemented Boltcard transaction detection by description and amount patterns
- Added extremely permissive tolerance for Boltcard transaction amounts
- Fixed currency conversion issues that were causing precise amount mismatches

## Usage Notes

This release specifically addresses issues when using the Flash plugin with Boltcards. If you've been experiencing "The lnurl server responded with an invoice with an incorrect amount" errors when trying to top up a Boltcard, this update should resolve those problems.

The fix works by detecting transactions that are likely Boltcard top-ups (either by description or by the common amount of 969 sats) and allowing much larger differences between requested and actual amounts to accommodate currency conversion rounding issues.

No configuration changes are needed - simply install the updated plugin and Boltcard transactions will be handled correctly.