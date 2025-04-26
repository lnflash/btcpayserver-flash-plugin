# API Reference

This document provides detailed information about the API endpoints exposed by the Flash BTCPayServer plugin.

## Overview

The Flash BTCPayServer plugin exposes several API endpoints for integrating with external systems. These endpoints are grouped into:

1. **Card Management API**: Endpoints for managing Flash cards
2. **Card Payment API**: Endpoints for processing card payments
3. **Card Balance API**: Endpoints for checking card balances

## Authentication

API endpoints use different authentication methods depending on their purpose:

1. **API Key Authentication**: For store-specific operations
   - Requires a BTCPayServer API key with appropriate permissions
   - API key must be included in the `Authorization` header

2. **Anonymous Endpoints**: For public operations
   - No authentication required
   - Typically used for card tap processing and balance checking

## API Endpoints

### Card Management API

#### Register a New Card

Registers a new Flash card in the system.

```
POST /api/v1/flash-cards/register
```

**Authentication Required**: Yes (API Key)

**Request Body**:
```json
{
  "cardUID": "string",        // Required: Card UID
  "cardName": "string",       // Optional: Friendly name for the card
  "initialBalance": "number"  // Optional: Initial balance in satoshis
}
```

**Response Body**:
```json
{
  "id": "string",             // Card registration ID
  "cardUid": "string",        // Card UID
  "cardName": "string",       // Card name
  "createdAt": "string"       // ISO 8601 creation timestamp
}
```

**Status Codes**:
- 200 OK: Card registered successfully
- 400 Bad Request: Invalid input
- 401 Unauthorized: Missing or invalid API key
- 500 Internal Server Error: Server error

**Example**:
```bash
curl -X POST https://btcpay.example.com/api/v1/flash-cards/register \
  -H "Authorization: token [API_KEY]" \
  -H "Content-Type: application/json" \
  -d '{"cardUID": "0123456789AB", "cardName": "Employee Card", "initialBalance": 50000}'
```

#### List Cards

Returns a list of registered cards for the current store.

```
GET /api/v1/flash-cards
```

**Authentication Required**: Yes (API Key)

**Query Parameters**:
- `limit` (optional): Maximum number of results to return (default 100)
- `skip` (optional): Number of results to skip (for pagination)
- `sort` (optional): Field to sort by (default 'createdAt')
- `order` (optional): Sort order, 'asc' or 'desc' (default 'desc')

**Response Body**:
```json
{
  "total": "number",           // Total number of cards
  "cards": [
    {
      "id": "string",          // Card registration ID
      "cardUid": "string",     // Card UID
      "cardName": "string",    // Card name
      "createdAt": "string",   // ISO 8601 creation timestamp
      "lastUsedAt": "string",  // ISO 8601 last used timestamp
      "isBlocked": "boolean",  // Whether the card is blocked
      "balance": "number"      // Current balance in satoshis
    }
  ]
}
```

**Status Codes**:
- 200 OK: Cards retrieved successfully
- 401 Unauthorized: Missing or invalid API key
- 500 Internal Server Error: Server error

**Example**:
```bash
curl -X GET https://btcpay.example.com/api/v1/flash-cards?limit=10 \
  -H "Authorization: token [API_KEY]"
```

#### Get Card Details

Returns details for a specific card.

```
GET /api/v1/flash-cards/{id}
```

**Authentication Required**: Yes (API Key)

**Path Parameters**:
- `id`: Card registration ID

**Response Body**:
```json
{
  "id": "string",                     // Card registration ID
  "cardUid": "string",                // Card UID
  "cardName": "string",               // Card name
  "storeId": "string",                // Store ID
  "userId": "string",                 // User ID (if assigned)
  "pullPaymentId": "string",          // Pull Payment ID
  "createdAt": "string",              // ISO 8601 creation timestamp
  "lastUsedAt": "string",             // ISO 8601 last used timestamp
  "isBlocked": "boolean",             // Whether the card is blocked
  "version": "number",                // Card version
  "spendingLimitPerTransaction": "number", // Per-transaction limit (if set)
  "balance": "number",                // Current balance in satoshis
  "transactions": [
    {
      "id": "string",                 // Transaction ID
      "amount": "number",             // Transaction amount
      "currency": "string",           // Transaction currency
      "type": "string",               // Transaction type
      "status": "string",             // Transaction status
      "createdAt": "string",          // ISO 8601 creation timestamp
      "completedAt": "string"         // ISO 8601 completion timestamp
    }
  ]
}
```

**Status Codes**:
- 200 OK: Card details retrieved successfully
- 401 Unauthorized: Missing or invalid API key
- 404 Not Found: Card not found
- 500 Internal Server Error: Server error

**Example**:
```bash
curl -X GET https://btcpay.example.com/api/v1/flash-cards/card_123 \
  -H "Authorization: token [API_KEY]"
```

#### Block Card

Blocks a card, preventing its use for payments.

```
POST /api/v1/flash-cards/{id}/block
```

**Authentication Required**: Yes (API Key)

**Path Parameters**:
- `id`: Card registration ID

**Response Body**:
```json
{
  "id": "string",          // Card registration ID
  "cardUid": "string",     // Card UID
  "isBlocked": true        // Always true for this endpoint
}
```

**Status Codes**:
- 200 OK: Card blocked successfully
- 401 Unauthorized: Missing or invalid API key
- 404 Not Found: Card not found
- 500 Internal Server Error: Server error

**Example**:
```bash
curl -X POST https://btcpay.example.com/api/v1/flash-cards/card_123/block \
  -H "Authorization: token [API_KEY]"
```

#### Unblock Card

Unblocks a card, allowing its use for payments.

```
POST /api/v1/flash-cards/{id}/unblock
```

**Authentication Required**: Yes (API Key)

**Path Parameters**:
- `id`: Card registration ID

**Response Body**:
```json
{
  "id": "string",          // Card registration ID
  "cardUid": "string",     // Card UID
  "isBlocked": false       // Always false for this endpoint
}
```

**Status Codes**:
- 200 OK: Card unblocked successfully
- 401 Unauthorized: Missing or invalid API key
- 404 Not Found: Card not found
- 500 Internal Server Error: Server error

**Example**:
```bash
curl -X POST https://btcpay.example.com/api/v1/flash-cards/card_123/unblock \
  -H "Authorization: token [API_KEY]"
```

### Card Payment API

#### Process Card Tap

Processes a payment initiated by a card tap.

```
POST /api/v1/flash-cards/tap
```

**Authentication Required**: No

**Request Body**:
```json
{
  "cardUID": "string",     // Required: Card UID
  "amount": "number",      // Required: Payment amount in satoshis
  "merchantId": "string",  // Required: Merchant identifier
  "locationId": "string",  // Optional: Location identifier
  "description": "string"  // Optional: Payment description
}
```

**Response Body**:
```json
{
  "success": "boolean",    // Whether the payment was processed successfully
  "transactionId": "string", // Transaction ID (if successful)
  "error": "string"        // Error message (if unsuccessful)
}
```

**Status Codes**:
- 200 OK: Payment processed successfully
- 400 Bad Request: Invalid input
- 404 Not Found: Card not found
- 402 Payment Required: Insufficient funds
- 500 Internal Server Error: Server error

**Example**:
```bash
curl -X POST https://btcpay.example.com/api/v1/flash-cards/tap \
  -H "Content-Type: application/json" \
  -d '{"cardUID": "0123456789AB", "amount": 1000, "merchantId": "merchant_123"}'
```

### Card Balance API

#### Get Card Balance

Returns the current balance for a specific card.

```
GET /api/v1/flash-cards/{cardUid}/balance
```

**Authentication Required**: No

**Path Parameters**:
- `cardUid`: Card UID

**Response Body**:
```json
{
  "cardUid": "string",     // Card UID
  "cardName": "string",    // Card name
  "balance": "number",     // Current balance in satoshis
  "currency": "string",    // Currency (e.g., "SATS")
  "isBlocked": "boolean"   // Whether the card is blocked
}
```

**Status Codes**:
- 200 OK: Balance retrieved successfully
- 404 Not Found: Card not found
- 500 Internal Server Error: Server error

**Example**:
```bash
curl -X GET https://btcpay.example.com/api/v1/flash-cards/0123456789AB/balance
```

#### Top Up Card

Adds funds to a card's balance.

```
POST /api/v1/flash-cards/{id}/topup
```

**Authentication Required**: Yes (API Key)

**Path Parameters**:
- `id`: Card registration ID

**Request Body**:
```json
{
  "amount": "number",      // Required: Top-up amount in satoshis
  "description": "string"  // Optional: Top-up description
}
```

**Response Body**:
```json
{
  "id": "string",          // Card registration ID
  "cardUid": "string",     // Card UID
  "transactionId": "string", // Transaction ID
  "amount": "number",      // Top-up amount
  "newBalance": "number"   // New balance after top-up
}
```

**Status Codes**:
- 200 OK: Top-up processed successfully
- 400 Bad Request: Invalid input
- 401 Unauthorized: Missing or invalid API key
- 404 Not Found: Card not found
- 500 Internal Server Error: Server error

**Example**:
```bash
curl -X POST https://btcpay.example.com/api/v1/flash-cards/card_123/topup \
  -H "Authorization: token [API_KEY]" \
  -H "Content-Type: application/json" \
  -d '{"amount": 50000, "description": "Monthly top-up"}'
```

## Error Handling

API responses include detailed error information in case of failures:

```json
{
  "error": {
    "code": "string",      // Error code
    "message": "string",   // Human-readable error message
    "details": {}          // Additional error details (if available)
  }
}
```

Common error codes:

| Code | Description |
|------|-------------|
| `invalid_request` | The request is missing required parameters or is malformed |
| `authentication_required` | The endpoint requires authentication |
| `invalid_card` | The card UID is invalid or the card is not registered |
| `card_blocked` | The card is blocked and cannot be used |
| `insufficient_funds` | The card has insufficient funds for the operation |
| `server_error` | An internal server error occurred |

## API Rate Limits

To ensure system stability, the API implements rate limiting:

| Endpoint | Rate Limit |
|----------|------------|
| Card Registration | 10 requests per minute |
| Card Tap Processing | 100 requests per minute |
| Balance Checking | 100 requests per minute |
| Other Endpoints | 60 requests per minute |

When rate limits are exceeded, the API returns a 429 Too Many Requests status code with headers indicating the rate limit and when it will reset.

## Webhook Notifications

In addition to direct API calls, the plugin can send webhook notifications for card events:

1. **Card Registration**: When a new card is registered
2. **Card Top-Up**: When a card is topped up
3. **Card Payment**: When a payment is made with a card
4. **Card Status Change**: When a card is blocked or unblocked

Webhooks can be configured in the BTCPayServer store settings.

## API Versioning

The API uses a versioned URL structure (/api/v1/...) to ensure backward compatibility. Future versions will use /api/v2/..., /api/v3/..., etc.

Breaking changes will only be introduced in new API versions, while the existing versions will continue to function according to their documentation.

## Testing the API

A sandbox environment is available for testing API integrations:

```
https://testnet.btcpay.example.com/api/v1/...
```

The sandbox environment uses testnet Bitcoin and allows testing without real funds.

## Security Considerations

When using the API, consider these security best practices:

1. **API Keys**: Keep API keys secure and use keys with minimal required permissions
2. **HTTPS**: Always use HTTPS for API requests
3. **TLS**: Use TLS 1.2 or later for secure communication
4. **Input Validation**: Validate all input before sending to the API
5. **Error Handling**: Properly handle and log API errors

## Conclusion

The Flash BTCPayServer plugin API provides a comprehensive set of endpoints for integrating with the NFC card system. By following this documentation, developers can build integrations that leverage the full capabilities of the plugin while maintaining security and reliability.