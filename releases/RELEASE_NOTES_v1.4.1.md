# Release Notes - v1.4.1

## Release Date: June 13, 2025

## Overview
This release focuses on production readiness with critical bug fixes, complete Lightning interface implementation, and enhanced error handling.

## New Features

### ✅ Complete Lightning Interface Implementation
- **PayInvoiceParams Support**: Implemented the missing `PayInvoiceAsync(PayInvoiceParams)` overload for full Lightning API compatibility
- **Payment History**: Implemented `ListPaymentsAsync` to display outgoing Lightning payments with proper filtering and pagination

### 🛡️ Enhanced Error Handling
- **Centralized Error Handler**: New `FlashErrorHandler` service for consistent error handling across the plugin
- **Retry Logic**: Automatic retry for transient failures with exponential backoff
- **User-Friendly Messages**: Clear, actionable error messages for common issues

### 🔌 Connection Validation
- **Connection String Validator**: Validates Flash connection strings before use
- **Connection Testing**: Test Flash API connectivity with detailed diagnostics
- **Configuration Validation**: Ensures all required parameters are present and valid

## Bug Fixes

### 🎯 Boltcard Payment Detection (Critical)
- **Issue**: Boltcard payments were successful but not detected by BTCPay Server
- **Root Cause**: Payment hash extraction was using bech32 format instead of hex
- **Fix**: Proper BOLT11 parsing using NBitcoin library to extract hex-encoded payment hash
- **Impact**: Boltcard tap-to-pay now works reliably

### 💰 Pull Payment Amount Handling (Critical)
- **Issue**: Pull payment amounts in satoshis were interpreted as USD dollars
- **Root Cause**: Missing satoshi-to-USD conversion for pull payment payouts
- **Fix**: Added proper amount conversion based on current exchange rates
- **Impact**: Pull payments now process correct amounts with minimum validation

### ⚡ Payment Synchronization
- **Issue**: Race condition between payment completion and status queries
- **Fix**: Added 5-minute payment cache for immediate detection
- **Impact**: Payments are instantly detected even before API indexing

## Improvements

### 📚 Documentation
- Comprehensive README with installation and troubleshooting guides
- Production readiness checklist
- Clear minimum amount requirements (1 cent USD)

### 🔍 Logging
- Enhanced debug logging for payment flows
- Sanitized request/response logging
- Better error context in logs

### 🏗️ Code Quality
- Removed code duplication
- Improved service separation
- Better null handling

## Known Issues

### WebSocket Connection
- May fail with 503 error in some environments
- **Impact**: Non-critical - falls back to polling
- **Workaround**: Automatic fallback ensures payments still work

### Flash API Limitations
- Requires USD wallet for Lightning operations
- Minimum transaction amount of 1 cent USD
- Transaction indexing may have slight delays

## Upgrade Instructions

1. Download `BTCPayServer.Plugins.Flash.btcpay` v1.4.1
2. Upload through BTCPay Server plugin manager
3. Restart BTCPay Server
4. No configuration changes required

## Breaking Changes
None - This release maintains full backward compatibility.

## Minimum Requirements
- BTCPayServer v2.0.0+
- Flash account with USD wallet
- Valid Flash API token

## Next Release Preview
- LNURL-withdraw implementation
- No-amount invoice support
- Performance optimizations
- Additional unit tests

## Contributors
- Flash Plugin Development Team
- BTCPayServer Community

## Support
Report issues at: https://github.com/Island-Bitcoin/BTCPayServer.Plugins.Flash/issues