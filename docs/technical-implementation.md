# Flash Plugin Implementation Details

This document outlines the technical implementation details of the BTCPayServer.Plugins.Flash payment processor.

## Payment Processing Architecture

The Flash plugin integrates with the Flash Lightning Network API using GraphQL queries and mutations. The primary payment processing functionality is implemented in `FlashLightningClient.cs`.

### Key Components

1. **FlashLightningClient**: Main implementation of ILightningClient interface for BTCPay Server.
2. **FlashLnurlHelper**: Handles LNURL and Lightning Address resolution and processing.
3. **GraphQL Communication**: Uses GraphQLClient to communicate with Flash's API.

## Payment Status Tracking System

The plugin implements a sophisticated payment tracking system to handle asynchronous payment flows, especially for LNURL payments in pull payment scenarios:

### Payment Tracking Components

1. **Recent Payments Cache**: 
   - Uses `_recentPayments` dictionary to track payment status by hash
   - Uses `_paymentSubmitTimes` to record submission times
   - Considers payments "recent" if submitted within the last 60 seconds (5 minutes for LNURL)

2. **Multi-hash LNURL Tracking**:
   - For LNURL payments, tracks multiple possible hash representations
   - Stores the original LNURL string itself as a possible identifier
   - Calculates and stores SHA256 hash of the LNURL string
   - Stores hash of combined payment IDs (pullPaymentId + payoutId)
   - Tracks the payout ID directly

3. **Hash Association Logic**:
   - When BTCPay Server requests an unknown payment hash, the system attempts to associate it with known payments
   - For LNURL payments, it compares hashes of stored LNURL strings to find matches
   - Captures unknown hashes for consistent future responses

4. **Status Propagation**:
   - Returns PENDING status for payments that have been submitted but not yet confirmed
   - BTCPay Server displays "payment has been initiated but is still in-flight" message
   - Automatically assumes LNURL payments complete if actual verification is unavailable

### Implementation Flow

1. When a payment is submitted, its status (PENDING/COMPLETE) is stored with multiple possible identifying hashes
2. When BTCPay requests status with `GetPayment(hash)`, the system:
   - Checks direct match in the tracking dictionary
   - Checks if the hash might be derived from an LNURL string we're tracking
   - Returns consistent status based on submission time and prior knowledge
3. For unknown hashes, assumes association with recent submissions to provide a consistent experience

## Lightning Payment Processing

The plugin now implements sophisticated payment processing that supports:

### Invoice Type Detection and Handling

The payment flow has been redesigned to automatically detect invoice types and use the appropriate mutation:

1. **Invoice Type Detection**:
   - The plugin parses the BOLT11 invoice to determine if it has an amount.
   - Based on this and the wallet currency, it selects the appropriate payment mutation.

2. **Mutation Selection Logic**:
   ```csharp
   if (hasAmount) {
       // Use lnInvoicePaymentSend for invoices with amounts
   } 
   else if (isUsdWallet) {
       // Use lnNoAmountUsdInvoicePaymentSend for no-amount USD wallet payments
   }
   else {
       // Use lnNoAmountInvoicePaymentSend for no-amount BTC wallet payments
   }
   ```

3. **Response Handling**:
   - Different response classes for each mutation type
   - Consistent error handling across all payment types

### LNURL and Lightning Address Support

1. **Case Insensitivity**:
   - All LNURL inputs are converted to lowercase for consistent processing.
   - Lightning addresses (user@domain.com) are properly detected and handled.

2. **Payment Resolution**:
   - LNURL-pay endpoints are resolved to obtain BOLT11 invoices.
   - The resulting BOLT11 invoices are then paid using the appropriate mutation.

### Enhanced Error Handling and Diagnostics

The plugin now provides detailed diagnostic information at every stage:

1. **GraphQL Error Classification**:
   - Authentication/authorization errors
   - Balance/insufficient funds errors
   - Network/connection errors
   - Invoice validity errors

2. **Diagnostic Context**:
   - Wallet ID and currency information included in error messages
   - Detailed GraphQL error codes and messages

3. **Logging Improvements**:
   - Consistent [PAYMENT DEBUG] prefixing for payment-related logs
   - Enhanced logging of invoice details, wallet state, and API interactions

## Technical Implementation Notes

### Response Classes

The plugin defines specialized response classes for different mutation types:

```csharp
private class PayInvoiceResponse
{
    public PaymentData lnInvoicePaymentSend { get; set; } = null!;
    // ...
}

private class NoAmountPayInvoiceResponse
{
    public PaymentData lnNoAmountInvoicePaymentSend { get; set; } = null!;
    // ...
}

private class NoAmountUsdPayInvoiceResponse
{
    public PaymentData lnNoAmountUsdInvoicePaymentSend { get; set; } = null!;
    // ...
}
```

### Invoice Decoding and Fallback Mechanisms

The plugin includes robust invoice decoding with fallback mechanisms:

1. **Primary Decoding**: Attempts to use the Flash API's `decodeInvoice` field to get invoice details
2. **Fallback Decoding**: When `decodeInvoice` is unavailable (as in some Flash API instances), uses a custom BOLT11 parser
3. **Amount Extraction**: Parses BOLT11 format to identify if an invoice has an amount and what the amount is

For no-amount invoices in a USD wallet context, the plugin provides several fallback strategies:

1. **Pull Payment Amount Tracking**: Stores amount information from Pull Payment creation to use later when making payments
2. **Amount Forwarding**: Passes along amount information from LNURL resolution to ensure it's available for payment
3. **Context-aware Payment**: Associates Pull Payment IDs with invoices to track and retrieve amounts as needed
4. **Currency Conversion**: Automatic conversion from satoshis to USD cents for Flash's USD wallet API

#### Currency Conversion Details

When processing a no-amount invoice payment with a USD wallet, the plugin:

1. Retrieves the amount in satoshis (either from invoice decoding or stored pull payment context)
2. Fetches the current BTC/USD exchange rate from the Flash API
3. Converts this amount from satoshis to USD cents using the live rate
4. Passes the USD cents amount to the `lnNoAmountUsdInvoicePaymentSend` mutation

The plugin also implements a robust exchange rate system:

- **Primary Source**: Flash API's `realtimePrice` GraphQL query
- **Fallback Chain**: If the primary source fails, the plugin tries multiple public APIs:
  1. CoinGecko API (bitcoin price in USD)
  2. CoinDesk Bitcoin Price Index API
  3. Conservative estimate only as last resort
- **Intelligent Caching**:
  - Primary rates are cached for 5 minutes
  - Fallback rates are cached for 15 minutes
  - Different cache durations optimize for accuracy vs. API call efficiency
- **Detailed Logging**: All rate sources and conversion steps are logged for diagnostics

This multi-tier approach ensures reliable operation even when individual rate sources are unavailable, while maintaining accurate conversions for the Flash API's USD cents requirement.

### Payment Processing Methods

The payment flow has been redesigned with the method `SendPaymentWithCorrectMutation` that:

1. Decodes the invoice to determine its type
2. Checks the wallet currency
3. Constructs the appropriate mutation
4. Calls the mutation with the right parameters
5. Processes the response using specialized handlers

### Error Processing Methods

Three specialized methods handle the error processing for different mutation types:

1. `ProcessPaymentResponse`: For standard amount invoices
2. `ProcessNoAmountPaymentResponse`: For no-amount BTC wallet payments
3. `ProcessNoAmountUsdPaymentResponse`: For no-amount USD wallet payments

Each provides detailed diagnostic information in case of failures.

## Design Patterns

The implementation follows these key design patterns:

1. **Strategy Pattern**: Different payment strategies are selected based on invoice type.
2. **Factory Method**: Response processing objects are created based on mutation type.
3. **Dependency Injection**: Dependencies like GraphQL client and logger are injected.
4. **Chain of Responsibility**: Fallback mechanisms for invoice decoding and amount determination.
5. **Context Object**: Storing and retrieving contextual information for payment processing.
6. **Cache Pattern**: Intelligent caching of exchange rates and payment status.
7. **Observer Pattern**: Tracking payment status changes and notifying interested components.

## Alignment with Flash Mobile Implementation

The implementation is modeled after the approach used in the Flash mobile app:

1. Same mutation selection logic as in flash-mobile/app/screens/send-bitcoin-screen/payment-details/lightning.ts
2. Similar error handling approach
3. Consistent GraphQL payload structure

## Handling API Limitations

The implementation includes mechanisms to handle various API limitations:

1. **Missing Fields**: Fallbacks for when GraphQL fields like `decodeInvoice` are not available
2. **No-Amount Invoices**: Special handling for no-amount invoices through amount tracking and context preservation
3. **Case Sensitivity**: Consistent lowercase handling for LNURL and Lightning addresses
4. **Detailed Logging**: Enhanced logging to provide clear diagnostic information
5. **Async Payment Status**: Tracking system for handling asynchronous payment flows with Flash's API

## Known Limitations and Future Improvements

While the plugin now successfully handles both LNURL and Lightning invoice inputs in pull payments, some limitations remain:

1. **Payment Verification**: The plugin can't always definitively verify that a payment was completed
2. **Status Reporting**: For some payments, especially LNURL ones, the plugin assumes success if it can't verify
3. **Status Tracking**: There's no persistent storage of payment status between restarts

Potential future improvements to the implementation include:

1. Convert to USD cents as needed for USD wallet payments
2. Add better currency conversion support
3. Implement WebSocket support for real-time payment notifications
4. Add support for other mutation types like intraLedgerPaymentSend
5. Improve the basic invoice decoder to extract more details like payment hash and expiry
6. Add persistent storage for payment status tracking
7. Implement webhook callbacks for payment status updates 