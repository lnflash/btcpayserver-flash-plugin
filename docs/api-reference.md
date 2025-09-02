# API Reference

## Lightning Client Methods

### Create Invoice
```csharp
CreateInvoice(LightMoney amount, string description, TimeSpan expiry)
```
Creates a new Lightning invoice.

### Pay Invoice
```csharp
Pay(string bolt11, PayInvoiceParams payParams)
```
Pays a Lightning invoice.

### Get Balance
```csharp
GetBalance()
```
Returns wallet balance information.

## Service Interfaces

### IFlashInvoiceService
Manages Lightning invoices:
- `CreateInvoiceAsync(amount, description, expiry)`
- `GetInvoiceAsync(paymentRequest)`
- `CancelInvoiceAsync(paymentHash)`

### IFlashPaymentService
Handles payment operations:
- `PayInvoiceAsync(paymentRequest, amount)`
- `GetPaymentStatusAsync(paymentHash)`

### IFlashWalletService
Wallet information and balance:
- `GetWalletInfoAsync()`
- `GetBalanceAsync()`

### IFlashExchangeRateService
Exchange rate operations:
- `GetExchangeRateAsync(from, to)`
- `ConvertAmountAsync(amount, from, to)`

### IFlashBoltcardService
NFC card operations:
- `ProcessBoltcardTapAsync(cardUid)`
- `GetBoltcardBalanceAsync(cardId)`
- `TopupBoltcardAsync(cardId, amount)`

### IFlashWebSocketService
Real-time updates:
- `ConnectAsync()`
- `DisconnectAsync()`
- `OnInvoiceUpdated`
- `OnPaymentReceived`

### IFlashMonitoringService
System monitoring:
- `GetHealthStatusAsync()`
- `GetMetricsAsync()`

## GraphQL API

The plugin communicates with Flash via GraphQL. Key operations:

### Queries
- `walletInfo` - Get wallet details
- `invoices` - List invoices
- `payments` - List payments
- `exchangeRates` - Get current rates

### Mutations
- `createInvoice` - Generate new invoice
- `payInvoice` - Send payment
- `cancelInvoice` - Cancel pending invoice

### Subscriptions
- `invoiceUpdated` - Real-time invoice updates
- `paymentReceived` - Instant payment notifications