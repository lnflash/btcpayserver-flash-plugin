# BTCPayServer Flash Plugin v1.4.0 Release Notes

## Overview
This release resolves critical issues with Boltcard payment detection and balance updates. Both tap-to-pay and top-up flows now work reliably with real-time payment notifications.

## Key Fixes

### ✅ Fixed Boltcard Payment Execution
- **Issue**: PayInvoiceAsync was returning dummy success without actually executing Lightning payments
- **Solution**: Implemented proper payment execution through Flash's `lnInvoicePaymentSend` GraphQL mutation
- **Impact**: Boltcard payments now execute properly and receiving wallets update correctly

### ✅ Fixed Payment Detection
- **Issue**: WebSocket subscriptions were using invoice IDs instead of BOLT11 payment requests
- **Solution**: Updated subscription logic to use BOLT11 payment requests for proper payment tracking
- **Impact**: Real-time payment detection now works for both Boltcard top-up and tap-to-pay flows

### ✅ Fixed Balance Updates
- **Issue**: Boltcard balances were not updating after successful payments
- **Solution**: Implemented comprehensive invoice tracking and notification system
- **Impact**: Boltcard balances now update immediately after payment completion

## Technical Improvements

### Enhanced Boltcard Tracking
- Added `BoltcardInvoicePoller` background service for aggressive payment monitoring
- Implemented BOLT11 to invoice ID mapping for proper payment correlation
- Added invoice tracking for LNURL/Boltcard payments that bypass normal invoice creation

### Improved Error Handling
- Better dependency injection with proper service scope handling
- Enhanced error messages and diagnostic logging
- Removed debug artifacts (emojis, console output) from production code

### Code Quality
- Cleaned up logging messages (removed emojis and debug tags)
- Replaced Console.WriteLine with proper ILogger usage
- Updated all version references to follow semantic versioning

## Installation

1. Download `BTCPayServer.Plugins.Flash-v1.4.0.btcpay`
2. Upload to your BTCPayServer instance via Server Settings > Plugins
3. Restart BTCPayServer
4. Verify plugin version 1.4.0 is loaded

## Testing Checklist

- [ ] Boltcard tap-to-pay shows payment confirmation on receiving device
- [ ] Boltcard balance updates after tap-to-pay
- [ ] Boltcard top-up flow completes successfully
- [ ] Boltcard balance updates after top-up
- [ ] WebSocket connections remain stable
- [ ] No errors in BTCPayServer logs

## Compatibility
- BTCPayServer: v2.0.0 or higher
- Flash API: Compatible with test and production endpoints
- Dependencies: BTCPayServer.Plugins.NFC

## Support
For issues or questions, please visit: https://github.com/lnflash/btcpayserver-flash-plugin