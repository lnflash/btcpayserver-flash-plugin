# NFC Card System

This document outlines the NFC card system implementation for the Flash BTCPayServer plugin, detailing how NFC cards are registered, programmed, and used for payments.

## Overview

The NFC card system enables merchants to issue and manage NFC cards for Lightning payments through the Flash wallet. The system comprises several components:

1. **Card Registration**: Process of registering NFC cards in the system
2. **Card Programming**: Generating keys and configuring NFC cards
3. **Card Management**: Managing registered cards and their status
4. **Payment Processing**: Handling payments initiated by card taps

## Card Technology

The plugin supports standard NFC cards compatible with the NTAG424 specification, which provides secure storage and cryptographic capabilities. These cards:

- Store cryptographic keys securely
- Support authentication protocols
- Provide tamper-resistant operation
- Work with standard NFC readers

## System Architecture

### Card Registration System

The card registration system is responsible for registering NFC cards and associating them with Flash wallets:

1. **Registration Flow**:
   - Merchant scans card UID using NFC reader
   - System creates a Pull Payment for the card's balance
   - Card is registered in the database with status and metadata
   - System generates cryptographic keys for the card

2. **Components**:
   - `FlashCardRegistrationService`: Core service for registration logic
   - `UIFlashCardController`: User interface for registration
   - `APIFlashCardController`: API endpoints for registration
   - Database models for storing registration data

### Card Programming System

The card programming system generates the necessary keys and configuration for NFC cards:

1. **Key Derivation**:
   - System derives application keys from card UID and Pull Payment ID
   - Keys are generated deterministically for reproducibility
   - Secure key derivation functions prevent key compromise

2. **Programming Flow**:
   - System generates card configuration data
   - Merchant programs the card using NFC writer
   - Card is activated and ready for use

### Payment Processing System

The payment processing system handles payments initiated by card taps:

1. **Tap Processing Flow**:
   - Card is tapped on NFC reader
   - Reader extracts card UID and sends to API
   - System validates card and checks balance
   - Payment is processed through the Flash wallet
   - Transaction is recorded in the database

2. **Components**:
   - `FlashPaymentHostedService`: Processes card payment events
   - `APIFlashCardController`: Receives tap events
   - `CardTransaction` model: Records transaction details

## Integration with Pull Payment System

The NFC card system integrates with BTCPayServer's Pull Payment system:

1. **Balance Management**:
   - Each card is linked to a Pull Payment
   - Card balance is the remaining amount in the Pull Payment
   - Payments reduce the available balance

2. **Top-up Process**:
   - Merchants can add funds to cards
   - Top-ups increase the Pull Payment limit
   - System tracks top-up history

## Implementation Details

### Card Registration

The card registration process is implemented as follows:

```csharp
// Register a new card
public async Task<CardRegistration> RegisterCard(
    string cardUid, 
    string pullPaymentId, 
    string storeId, 
    string? userId = null, 
    string? cardName = null)
{
    // Check if card already registered
    var existingCard = await GetCardRegistration(cardUid);
    if (existingCard != null)
    {
        // Update existing card
        // ...
    }
    
    // Create new registration
    var cardRegistration = new CardRegistration
    {
        CardUID = cardUid,
        PullPaymentId = pullPaymentId,
        StoreId = storeId,
        UserId = userId,
        CardName = cardName ?? "Flash Card",
        Version = 1,
        CreatedAt = DateTimeOffset.UtcNow
    };
    
    // Save to database
    // ...
    
    return cardRegistration;
}
```

### Key Derivation

The key derivation process is crucial for card security:

```csharp
// Derive card keys
public CardKeys DeriveCardKeys(string cardUid, int version, string pullPaymentId)
{
    // Create a unique seed from card UID, version, and pullPaymentId
    var seed = CombineAndHash(cardUid, version.ToString(), pullPaymentId);
    
    // Derive application master key
    var appMasterKey = DeriveKey(seed, "APP_MASTER");
    
    // Derive encryption key
    var encryptionKey = DeriveKey(seed, "ENCRYPTION");
    
    // Derive authentication key
    var authKey = DeriveKey(seed, "AUTH");
    
    // Additional keys
    var k3 = DeriveKey(seed, "K3");
    var k4 = DeriveKey(seed, "K4");
    
    return new CardKeys
    {
        AppMasterKey = appMasterKey,
        EncryptionKey = encryptionKey,
        AuthenticationKey = authKey,
        K3 = k3,
        K4 = k4
    };
}
```

### Payment Processing

The payment processing flow is implemented in the `FlashPaymentHostedService`:

```csharp
// Handle card tap event
private async Task HandleCardTapEvent(CardTapEvent evt)
{
    // Look up card registration
    var cardRegistration = await _cardService.GetCardRegistration(evt.CardUid);
    if (cardRegistration == null)
        return; // Card not registered
        
    // Check if card is blocked
    if (cardRegistration.IsBlocked)
        return; // Card is blocked
        
    // Check available funds
    var hasAvailableFunds = await _cardService.CardHasAvailableFunds(cardRegistration.Id);
    if (!hasAvailableFunds)
        return; // Insufficient funds
        
    // Log transaction
    var transaction = await _cardService.LogCardTransaction(
        cardRegistration.Id,
        evt.Amount,
        CardTransactionType.Payment);
        
    // Process payment through pull payment system
    // ...
    
    // Update transaction status
    await _cardService.UpdateCardTransactionStatus(
        transaction.Id,
        CardTransactionStatus.Completed);
}
```

## Security Considerations

The NFC card system implements several security measures:

### 1. Card Security

- **Key Derivation**: Secure key derivation prevents key compromise
- **Authentication**: Cards authenticate with the system before processing payments
- **Tamper Resistance**: NTAG424 cards provide tamper-resistant storage

### 2. Transaction Security

- **Validation**: All tap events are validated before processing
- **Authorization**: Cards must be authorized and active
- **Spending Limits**: Cards can have per-transaction spending limits

### 3. System Security

- **Data Protection**: Sensitive data is not stored in the database
- **API Security**: API endpoints use appropriate authentication
- **Logging**: All operations are logged for audit purposes

## User Interface

The NFC card system provides several user interfaces:

### 1. Card Registration UI

- Form for entering card details
- NFC scanning integration
- Initial balance configuration

### 2. Card Management UI

- List of registered cards
- Card status and balance information
- Transaction history
- Block/unblock functionality

### 3. Card Programming UI

- Instructions for programming cards
- Key display for manual programming
- Verification interface

## API Endpoints

The NFC card system exposes the following API endpoints:

### 1. Card Registration

```
POST /api/v1/flash-cards/register
```

Request:
```json
{
  "cardUID": "0123456789AB",
  "cardName": "Employee Card",
  "initialBalance": 50000
}
```

Response:
```json
{
  "id": "card_123",
  "cardUid": "0123456789AB",
  "cardName": "Employee Card",
  "createdAt": "2023-04-26T12:34:56Z"
}
```

### 2. Card Tap Processing

```
POST /api/v1/flash-cards/tap
```

Request:
```json
{
  "cardUID": "0123456789AB",
  "amount": 1000,
  "merchantId": "merchant_123",
  "locationId": "store_456"
}
```

Response:
```json
{
  "success": true
}
```

### 3. Card Balance Checking

```
GET /api/v1/flash-cards/{cardUid}/balance
```

Response:
```json
{
  "cardUid": "0123456789AB",
  "cardName": "Employee Card",
  "balance": 49000,
  "currency": "SATS",
  "isBlocked": false
}
```

## Testing

The NFC card system should be tested thoroughly:

### 1. Unit Tests

- Key derivation functionality
- Card registration logic
- Transaction processing

### 2. Integration Tests

- API endpoint functionality
- Database interactions
- Pull Payment integration

### 3. End-to-End Tests

- Complete card registration flow
- Payment processing with real cards
- Balance management and history

## Future Enhancements

Potential future enhancements to the NFC card system:

1. **Multi-wallet Support**: Link cards to multiple wallets
2. **Advanced Limits**: Time-based or merchant-specific spending limits
3. **Card Groups**: Manage cards in groups for organizations
4. **Enhanced Security**: Additional security measures for high-value cards
5. **Mobile Integration**: Mobile app for card management

## Conclusion

The NFC card system provides a robust foundation for implementing tap-to-pay functionality with Flash Lightning wallet. By leveraging BTCPayServer's Pull Payment system and implementing secure card management, the plugin enables merchants to offer a seamless payment experience while maintaining security and control.