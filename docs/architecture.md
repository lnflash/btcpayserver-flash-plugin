# Flash BTCPayServer Plugin Architecture

This document provides a high-level overview of the Flash BTCPayServer plugin architecture, explaining how different components interact and the overall system design.

## System Architecture Overview

The Flash BTCPayServer plugin follows a modular architecture designed to integrate Flash Lightning wallet functionality with BTCPayServer, while adding NFC card support.

![Architecture Diagram](https://mermaid.ink/img/pako:eNqFU9tu2zAM_RXCTx2w7O5bU3RtHzZgwwL0IQnyYMt0ROjiyZYzL1j-fZRT13W34QkQTx0eHpLuQFUKKAbXq2UE02_xKRrlXkznUHcpGZ-YrzPcKFdbjwknmL1UamdsZ4OJ2qPriEKxOtdsMu09yt00mglKaO_2cz9GXrMahk62YYo8q3b7F2XJcg98AqFOXw5BtauOVctMJzl3v9ZwKzW-f-QwSLpj2N_vvzGGv-X9B2OU5sK_QCqFONH4B2m5zfPVJBrXRZMwXSr6xGfBw0ZhZdOx84-kTR9NOqHkAw8p2sZzqJJJ1OG8jdimuD53kJQc0Dn_TnCUylXDFQZPgQf4lH06dn7tQRe8_ZgxkH08wOMWqQxCGIYjf4D1FQy5Kxq9e-OT8W-bR2tPQwZB4E8jGk93uSCcX1wAFjhgZw-wRtVjfB-kUfr8r-MNGtFzEBNSNcG90GizUl_VVNLxQb9cYpd-VgwJQ5R2ZWTjF2KDYB5n7Y6T09tTcpqbkw-7mzORdxC0Wxp_j0hZpTQdbw9p8oxhLVumzulUJl3tnkltwOzZU0L5KHBk0LZkPsXIGh4TbhO3mbfKqD1SB6Gxu16hnPwTfcbKGW5yCnj-Vz-aRXc-JvWXsLcQ9G9OGWUYrZlADZy9yR0aPnPiLN4RpYKmdA3NzwmZe_LpxZ9XJV8wGNMEbRjk2lqcJq8RskLU2Ld4bnbofKeCvgL3HqtS6hVcaAxXi-uV4SsKnv38CUmPw3s)

### Core Components

The plugin is divided into several core components, each with specific responsibilities:

1. **Flash Lightning Integration**:
   - `FlashPlugin.cs`: Main plugin entry point
   - `FlashLightningClient.cs`: Implementation of ILightningClient for Flash
   - `FlashLightningConnectionStringHandler.cs`: Handles connection strings for Flash

2. **NFC Card System**:
   - `FlashCardRegistrationService.cs`: Manages card registrations and mappings
   - `FlashPaymentHostedService.cs`: Processes card payments and events
   - Data models for card registrations and transactions

3. **UI and API Layer**:
   - `UIFlashCardController.cs`: User interface for card management
   - `APIFlashCardController.cs`: API endpoints for card operations
   - View components for card management and settings

4. **Database Layer**:
   - `FlashCardDbContext.cs`: Entity Framework context for card data
   - `FlashCardDbContextFactory.cs`: Factory for creating DB contexts
   - Migration system for database schema management

### Component Interactions

The components interact in the following ways:

1. **Lightning Payment Flow**:
   - User configures Flash Lightning connection in BTCPayServer
   - BTCPayServer creates invoices via `FlashLightningClient`
   - Flash API processes payments and sends notifications
   - BTCPayServer updates invoice status via webhook or subscription

2. **Card Registration Flow**:
   - Merchant registers card via UI
   - `UIFlashCardController` processes request
   - `FlashCardRegistrationService` creates card registration
   - Pull Payment is created for card balance
   - Card is programmed with derived keys

3. **Card Payment Flow**:
   - Card is tapped on NFC reader
   - `APIFlashCardController` receives tap event
   - `FlashPaymentHostedService` processes the payment
   - Card balance is updated via Pull Payment system
   - Transaction is recorded in database

## Integration with BTCPayServer

The plugin integrates with BTCPayServer through several extension points:

### Lightning Network Integration

BTCPayServer provides a standardized interface for Lightning Network integration via the `ILightningClient` interface. The Flash plugin implements this interface to allow BTCPayServer to:

- Create and manage Lightning invoices
- Process Lightning payments
- Check payment status
- Listen for payment updates

The `FlashLightningConnectionStringHandler` registers with BTCPayServer to handle the `flash://` connection scheme, allowing users to configure Flash as their Lightning provider.

### UI Extensions

The plugin extends the BTCPayServer UI in several places:

1. **Lightning Setup Tab**: Adds a "Flash" option in the Lightning configuration page
2. **Navigation Menu**: Adds a "Flash Cards" entry in the main navigation
3. **Custom Pages**: Adds card management pages under the Flash Cards section

These extensions use BTCPayServer's UI extension system, which allows plugins to inject HTML into specific locations in the UI.

### Event System

The plugin integrates with BTCPayServer's event system to:

- Listen for invoice payment events
- Process card top-ups
- Handle card tap events
- Update transaction status

The `FlashPaymentHostedService` subscribes to these events and processes them accordingly.

### Pull Payment System

The plugin uses BTCPayServer's Pull Payment system to manage card balances:

- Each card is linked to a Pull Payment
- Card balance is tied to Pull Payment remaining amount
- Card transactions create payment requests against the Pull Payment
- Top-ups add funds to the Pull Payment

## Database Schema

The plugin defines its own database schema, which includes:

1. **CardRegistration Table**:
   - Primary key: Id (string)
   - CardUID (string): Unique identifier for the NFC card
   - PullPaymentId (string): Reference to BTCPayServer Pull Payment
   - StoreId (string): The store the card belongs to
   - UserId (string, nullable): The user who owns the card
   - CardName (string): A friendly name for the card
   - Version (int): Card version number
   - CreatedAt (DateTimeOffset): When the card was registered
   - LastUsedAt (DateTimeOffset, nullable): When the card was last used
   - IsBlocked (bool): Whether the card is blocked
   - FlashWalletId (string, nullable): Associated Flash wallet ID
   - SpendingLimitPerTransaction (decimal, nullable): Per-transaction limit

2. **CardTransaction Table**:
   - Primary key: Id (string)
   - CardRegistrationId (string): Foreign key to CardRegistration
   - PayoutId (string, nullable): Reference to BTCPayServer Payout
   - Amount (decimal): Transaction amount
   - Currency (string): Transaction currency
   - Type (enum): Transaction type (Payment, TopUp, Refund)
   - Status (enum): Transaction status
   - InvoiceId (string, nullable): Associated invoice ID
   - PaymentHash (string, nullable): Lightning payment hash
   - CreatedAt (DateTimeOffset): When the transaction was created
   - CompletedAt (DateTimeOffset, nullable): When the transaction was completed
   - MerchantId (string, nullable): Merchant identifier
   - LocationId (string, nullable): Location identifier
   - Description (string, nullable): Transaction description

## API Endpoints

The plugin exposes the following API endpoints:

1. **Card Registration**:
   - `POST /api/v1/flash-cards/register`
   - Registers a new Flash card
   - Requires store authentication

2. **Card Tap Processing**:
   - `POST /api/v1/flash-cards/tap`
   - Processes a card tap payment
   - Anonymous endpoint for POS systems

3. **Balance Checking**:
   - `GET /api/v1/flash-cards/{cardUid}/balance`
   - Gets the balance for a specific card
   - Anonymous endpoint for balance checking

See [API Reference](api-reference.md) for detailed API documentation.

## Security Architecture

The plugin implements several security measures:

1. **API Authentication**:
   - API endpoints are protected by BTCPayServer's authentication system
   - Card registration requires store authentication
   - Card tap processing uses secure validation

2. **Card Security**:
   - Secure key derivation for NFC cards
   - Cards can be blocked if lost or stolen
   - Transaction limits prevent unauthorized spending

3. **Data Protection**:
   - Sensitive data is not stored in the database
   - Card UIDs are stored but not directly usable without proper keys
   - All database access is through authenticated services

## Conclusion

The Flash BTCPayServer plugin architecture is designed to be modular, secure, and easily maintainable. By leveraging BTCPayServer's extension points and following its design patterns, the plugin seamlessly integrates Flash Lightning wallet and NFC card functionality while maintaining compatibility with the core BTCPayServer system.