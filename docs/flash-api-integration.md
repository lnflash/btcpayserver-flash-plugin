# Flash API Integration

This document details the integration between the BTCPayServer plugin and the Flash Lightning wallet GraphQL API.

## Overview

The Flash Lightning wallet provides a GraphQL API for managing Lightning Network operations. The BTCPayServer plugin integrates with this API to enable:

1. Lightning invoice creation and management
2. Lightning payment processing
3. Wallet balance checking
4. Real-time payment notifications

## GraphQL API Endpoints

The Flash GraphQL API is accessed through a single endpoint:

```
https://api.flashapp.me/graphql
```

For WebSocket subscriptions:

```
wss://ws.flashapp.me/graphql
```

## Authentication

All API requests require authentication using an API key, which should be included in the HTTP headers:

```
X-API-KEY: flash_api_key_here
Authorization: Bearer flash_api_key_here
```

## API Mapping

The following table shows how the BTCPayServer Lightning interface methods map to Flash GraphQL queries/mutations:

| ILightningClient Method | Flash GraphQL Query/Mutation | Description |
|--------------------------|------------------------------|-------------|
| `GetInvoice` | `InvoiceByPaymentHash` | Fetch invoice details by payment hash |
| `ListInvoices` | `Invoices` | List invoices with optional filtering |
| `GetPayment` | `TransactionsByPaymentHash` | Get payment details by payment hash |
| `ListPayments` | `Transactions` | List payments with optional filtering |
| `CreateInvoice` | `lnInvoiceCreate` or `lnUsdInvoiceCreate` | Create new Lightning invoice |
| `Pay` | `lnInvoicePaymentSend` | Pay a Lightning invoice |
| `GetBalance` | `GetWallet` | Get wallet balance |
| `Listen` | Subscription to `myUpdates` | Listen for real-time payment updates |
| `GetNetworkAndDefaultWallet` | `GetNetworkAndDefaultWallet` | Get network and default wallet info |

## GraphQL Queries and Mutations

Below are the key GraphQL queries and mutations used by the plugin:

### 1. Fetching an Invoice

```graphql
query InvoiceByPaymentHash($paymentHash: PaymentHash!, $walletId: WalletId!) {
  me {
    defaultAccount {
      walletById(walletId: $walletId) {
        invoiceByPaymentHash(paymentHash: $paymentHash) {
          createdAt
          paymentHash
          paymentRequest
          paymentSecret
          paymentStatus
          satoshis
        }
      }
    }
  }
}
```

### 2. Listing Invoices

```graphql
query Invoices($walletId: WalletId!) {
  me {
    defaultAccount {
      walletById(walletId: $walletId) {
        invoices {
          edges {
            node {
              createdAt
              paymentHash
              paymentRequest
              paymentSecret
              paymentStatus
              ... on LnInvoice {
                satoshis
              }
            }
          }
        }
      }
    }
  }
}
```

### 3. Creating an Invoice

```graphql
mutation lnInvoiceCreate($input: LnInvoiceCreateOnBehalfOfRecipientInput!) {
  lnInvoiceCreateOnBehalfOfRecipient(input: $input) {
    invoice {
      createdAt
      paymentHash
      paymentRequest
      paymentSecret
      paymentStatus
      satoshis
    },
    errors {
      message
    }
  }
}
```

For USD-denominated invoices:

```graphql
mutation lnUsdInvoiceCreate($input: LnUsdInvoiceBtcDenominatedCreateOnBehalfOfRecipientInput!) {
  lnUsdInvoiceBtcDenominatedCreateOnBehalfOfRecipient(input: $input) {
    invoice {
      createdAt
      paymentHash
      paymentRequest
      paymentSecret
      paymentStatus
      satoshis
    },
    errors {
      message
    }
  }
}
```

### 4. Paying an Invoice

```graphql
mutation LnInvoicePaymentSend($input: LnInvoicePaymentInput!) {
  lnInvoicePaymentSend(input: $input) {
    transaction {
      createdAt
      direction
      id
      initiationVia {
        ... on InitiationViaLn {
          paymentHash
          paymentRequest
        }
      }
      memo
      settlementAmount
      settlementCurrency
      settlementVia {
        ... on SettlementViaLn {
          preImage
        }
        ... on SettlementViaIntraLedger {
          preImage
        }
      }
      status
    }
    errors {
      message
    }
    status
  }
}
```

### 5. Getting Wallet Balance

```graphql
query GetWallet($walletId: WalletId!) {
  me {
    defaultAccount {
      walletById(walletId: $walletId) {
        id
        balance
        walletCurrency
      }
    }
  }
}
```

### 6. Listening for Payment Updates

```graphql
subscription myUpdates {
  myUpdates {
    update {
      ... on LnUpdate {
        transaction {
          initiationVia {
            ... on InitiationViaLn {
              paymentHash
            }
          }
          direction
        }
      }
    }
  }
}
```

### 7. Getting Network and Default Wallet

```graphql
query GetNetworkAndDefaultWallet {
  globals {
    network
  }
  me {
    defaultAccount {
      defaultWallet {
        id
        currency
      }
    }
  }
}
```

## Data Mapping

The plugin maps data between BTCPayServer's data structures and Flash API responses as follows:

### Invoice Mapping

Flash API invoice data is mapped to BTCPayServer's `LightningInvoice` object:

| BTCPayServer Field | Flash Field | Notes |
|--------------------|-------------|-------|
| `Id` | `paymentHash` | Unique identifier for the invoice |
| `Amount` | `satoshis` | Invoice amount in satoshis |
| `BOLT11` | `paymentRequest` | BOLT11 invoice string |
| `Status` | `paymentStatus` | Mapped to BTCPayServer status enum |
| `ExpiresAt` | Derived from BOLT11 | Extract expiry from decoded BOLT11 |
| `PaidAt` | Calculated | Set when status is PAID |
| `PaymentHash` | `paymentHash` | Lightning payment hash |
| `Preimage` | `paymentSecret` | Payment preimage |

### Payment Mapping

Flash API payment data is mapped to BTCPayServer's `LightningPayment` object:

| BTCPayServer Field | Flash Field | Notes |
|--------------------|-------------|-------|
| `Id` | `initiationVia.paymentHash` | Payment identifier |
| `PaymentHash` | `initiationVia.paymentHash` | Lightning payment hash |
| `Preimage` | `settlementVia.preImage` | Payment preimage |
| `Amount` | From BOLT11 | Extracted from BOLT11 |
| `Status` | `status` | Mapped to BTCPayServer status enum |
| `BOLT11` | `initiationVia.paymentRequest` | BOLT11 payment request |
| `CreatedAt` | `createdAt` | Creation timestamp |

## Error Handling

The plugin implements error handling for various API scenarios:

1. **Connection Errors**:
   - Network connectivity issues
   - Timeouts
   - Server errors

2. **Authentication Errors**:
   - Invalid API key
   - Expired credentials
   - Permission issues

3. **Business Logic Errors**:
   - Insufficient funds
   - Invalid invoice
   - Expired invoice

Each error is properly mapped to BTCPayServer's error model and presented to the user in a meaningful way.

## Testing the Integration

To test the Flash API integration:

1. **Setup Test Environment**:
   - Create a test Flash wallet
   - Generate an API key with appropriate permissions
   - Configure the plugin with test credentials

2. **Test Scenarios**:
   - Invoice creation and retrieval
   - Payment processing
   - Balance checking
   - Real-time notification handling

See [Testing Plan](testing-plan.md) for detailed testing procedures.

## Implementation Considerations

When implementing the Flash API integration, consider the following:

1. **Rate Limiting**:
   - Implement backoff strategies for API rate limits
   - Cache frequently accessed data where appropriate

2. **Connection Pooling**:
   - Reuse HTTP connections when possible
   - Maintain persistent WebSocket connections for subscriptions

3. **Error Resilience**:
   - Implement retry logic for transient errors
   - Handle network instability gracefully

4. **Performance Optimization**:
   - Batch API requests where possible
   - Implement response caching for suitable endpoints

## Security Considerations

When integrating with the Flash API:

1. **API Key Security**:
   - Store API keys securely
   - Use environment variables or secure storage
   - Never expose API keys in client-side code

2. **Transport Security**:
   - Always use HTTPS for API communication
   - Validate server certificates
   - Implement proper TLS/SSL handling

3. **Data Handling**:
   - Validate all data from the API
   - Sanitize inputs and outputs
   - Implement proper error boundaries

## Conclusion

The Flash API integration provides a robust foundation for the BTCPayServer plugin, enabling Lightning Network functionality through the Flash wallet. By following the mapping outlined in this document, the plugin can seamlessly integrate with Flash while maintaining compatibility with BTCPayServer's Lightning interface.