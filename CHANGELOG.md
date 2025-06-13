# Changelog

All notable changes to the BTCPayServer Flash Plugin will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.0] - 2025-01-13

### Added
- Real-time WebSocket payment detection using Flash's GraphQL subscription API
- Enhanced Boltcard payment tracking with correlation sequences
- Background service (BoltcardInvoicePoller) for monitoring Boltcard payments
- Proper payment execution through Flash's lnInvoicePaymentSend mutation
- Comprehensive payment notification system to ensure Boltcard balance updates

### Fixed
- Fixed critical issue where PayInvoiceAsync was returning dummy success without executing payments
- Fixed WebSocket subscription to use BOLT11 payment request instead of invoice ID
- Fixed Boltcard tap-to-pay flow to properly detect and notify payment completion
- Fixed invoice tracking for Boltcard/LNURL payments that bypass normal invoice creation
- Fixed dependency injection issues with background services

### Changed
- Improved logging with standardized messages (removed emojis and debug tags)
- Enhanced error handling and diagnostic information
- Optimized payment detection with 1-second polling intervals for Boltcard payments
- Updated all version references to 1.4.0

### Technical Improvements
- Added BOLT11 to invoice ID mapping for proper payment tracking
- Implemented aggressive invoice monitoring for unknown/Boltcard invoices
- Added proper service scope handling for accessing scoped services in background tasks
- Cleaned up console output and replaced with proper logging

## [1.3.6] - 2025-01-12

### Fixed
- Initial fixes for Boltcard balance update issues
- WebSocket connection improvements

## [1.3.5] - Previous Release

### Added
- Basic Flash Lightning integration
- LNURL support
- Boltcard top-up functionality