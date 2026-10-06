# Changelog

All notable changes to the BTCPayServer Flash Plugin will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.6.6] - 2026-10-06

### Removed
- Controllers and views from the earlier dashboard UI (`Controllers.backup/`, `Views.backup/`). The controllers were still compiled into the plugin and answered requests: some pages returned HTTP 500 because their views were not packaged. The LNURL-pay endpoints in `FlashLNURLController` remain.
- A demo class (`Tests/LnurlMetadataTest.cs`) that was compiled into the plugin.

### Fixed
- The plugin icon had stray text after the closing `</svg>` tag.

### Changed
- Release packages are published as GitHub releases instead of being committed to `releases/`.
- Documentation rewritten: installation, how it works, troubleshooting, development.

## [1.6.5] - 2026-10-06

### Removed
- `FlashSimpleInvoiceService`. It called `lnInvoiceCreate` (a BTC-wallet invoice, which Flash does not support) without a `walletId`, so every invoice first failed and fell back to `FlashInvoiceService`. Invoices are now created only through `FlashInvoiceService`.

### Fixed
- The LNURL-pay callback in `FlashLNURLController` used the same service with no fallback and always returned an error.

## [1.6.4] - 2026-10-06

### Fixed
- Flash `PENDING` sends were reported to BTCPay as completed. They, and transport failures during a send, are now reported as unknown so the payout stays in progress. `ALREADY_PAID` is an error.
- `GetPayment` guessed from recently submitted payments. It now reports the recorded outcome of the send, or Flash's status for that exact invoice, or pending.

## [1.6.3] - 2026-10-06

### Fixed
- Sends failed with "insufficient balance. Current Balance: 0" on accounts moved to the USDT cash wallet. Requests now declare the `cash-wallet-usdt-v1` client capability, and the plugin uses the USDT wallet.

## [1.6.2] - 2026-10-06

### Fixed
- Invoices were marked paid when any payment had been received in the last 10 minutes, so one card's payment could credit another. An invoice is now reported paid only when Flash's `lnInvoicePaymentStatus` confirms that exact invoice.
- `settlementAmount` (cents) was read as dollars, so BTCPay recorded 100x the amount received. The amount received now comes from the invoice's payment request.
- Sats-to-cents conversion rounded to nearest, which could leave invoices underpaid and never settled. It now rounds up.
- `CreateInvoiceAsync(LightMoney, string, TimeSpan)` always threw (reflection with the wrong argument count).

## [1.6.1] - 2026-10-06

### Fixed
- Wallet lookup failed when Flash returned a null balance for the BTC wallet.

## [1.6.0] - 2026-10-06

### Added
- API keys from console.flashapp.me (`fk_...`), sent in the `X-API-KEY` header. Session tokens still use `Authorization: Bearer`.

### Changed
- With an API key the plugin polls for payments; Flash's WebSocket subscriptions accept session tokens only.
- `X-API-KEY` is redacted in request logs.

## [1.5.1] - 2025-06-16

### Added
- Connection state management system to prevent duplicate WebSocket connections
- WebSocket health metrics tracking (messages sent/received, errors, reconnect attempts)
- Configurable exponential backoff retry policy with jitter
- Active ping/pong keep-alive mechanism (30-second intervals)
- Connection state change events for monitoring
- Comprehensive connection cleanup on disposal

### Fixed
- Critical: "Remote party closed connection without completing handshake" errors
- WebSocket reconnection storms causing server overload
- Thread-safety issues with concurrent connection attempts
- Resource leaks from improper WebSocket disposal
- Graceful handling of abrupt disconnections

### Changed
- WebSocket service now uses state machine pattern for connection management
- Reconnection logic uses exponential backoff (1s → 2s → 4s → ... → max 2min)
- Added jitter to reconnection delays to prevent thundering herd
- Improved error logging with contextual information
- Enhanced resource management with proper disposal patterns

### Technical Improvements
- Created modular type system: WebSocketConnectionState, WebSocketRetryPolicy, WebSocketHealthMetrics
- Implemented thread-safe operations using SemaphoreSlim locks
- Added connection timeout handling (30-second default)
- Improved separation of concerns with dedicated reconnection logic
- Better error categorization and handling strategies

## [1.5.0] - 2025-06-16

### Changed
- Major UI cleanup: Temporarily removed all menu and dashboard elements for comprehensive rebuild
- Core Lightning functionality remains fully operational
- Preparing for fresh UI implementation

### Technical Notes
- Backend services and payment processing unaffected
- API and WebSocket functionality maintained
- Database operations continue normally

## [1.4.2] - 2025-06-13

### Fixed
- Critical: Removed hardcoded WebSocket endpoints - now dynamically derived from API endpoint
- External link generation now uses configured Flash instance instead of hardcoded URL
- Made plugin domain-agnostic to work on any BTCPay Server instance

### Changed
- WebSocket endpoint logic now intelligently derives from API configuration:
  - `api.domain.com` → `ws.domain.com`
  - Localhost and IP addresses maintain the same host
  - Automatic protocol selection (wss/ws) based on API scheme
- External links now derived from connection string configuration

### Technical Improvements
- Better support for custom Flash API deployments
- Improved domain detection and URL generation
- Enhanced compatibility with various hosting configurations

## [1.4.1] - 2025-06-13

### Added
- Complete Lightning interface implementation with PayInvoiceParams support
- ListPaymentsAsync method for payment history display
- Comprehensive error handling framework with retry logic
- Connection string validation service
- Payment caching system for immediate detection (5-minute cache)
- User-friendly error messages for common issues
- Production readiness documentation

### Fixed
- Critical: Boltcard payment detection using proper hex payment hash format
- Critical: Pull payment amount interpretation (satoshis vs USD conversion)
- Payment hash extraction from BOLT11 using NBitcoin library
- Race condition between payment completion and status queries

### Changed
- Improved minimum amount validation with clear error messages
- Enhanced logging for payment flows
- Better null reference handling throughout codebase

### Technical Improvements
- Centralized error handling with FlashErrorHandler service
- Exponential backoff for transient failures
- Removed code duplication in payment processing
- Improved service separation and dependency injection

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