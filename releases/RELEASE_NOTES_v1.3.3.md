# Flash Plugin v1.3.3 Release Notes

## API and Payment Improvements

- Added GraphQL schema feature detection for better compatibility
- Fixed issues with decodeInvoice API by implementing automatic fallback parser
- Enhanced payment hash tracking with deterministic ID generation
- Improved transaction ID to payment hash mapping
- Optimized wallet initialization and authentication flow
- Prevented redundant plugin initialization
- Fixed "No transaction found for payment hash" errors with better tracking
- Enhanced Boltcard payment reliability

## Technical Improvements

- Added semaphore to prevent concurrent initialization
- Implemented thread-safe tracking for payments
- Added bidirectional mapping between transaction IDs and payment hashes
- Improved error handling and logging during initialization
- Enhanced schema introspection for API feature support

## Documentation

- Added detailed logging for payment tracking and debugging
- Improved error messages for API incompatibilities