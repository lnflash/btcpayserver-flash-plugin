# Payment Synchronization Fix Implementation

## Problem
BTCPay Server couldn't find payments when querying by payment hash immediately after payment because the Flash API doesn't immediately index transactions.

## Solution
Implemented a comprehensive fix using a static in-memory cache to track recently paid invoices:

### 1. FlashLightningClient.cs Updates

#### Added Static Collection and Helper Class
```csharp
// Static collection to track recently paid invoices across all instances
private static readonly ConcurrentDictionary<string, RecentlyPaidInvoice> _recentlyPaidInvoices = new ConcurrentDictionary<string, RecentlyPaidInvoice>();
private static readonly TimeSpan _recentlyPaidExpiration = TimeSpan.FromMinutes(5);

public class RecentlyPaidInvoice
{
    public string InvoiceId { get; set; }
    public string PaymentHash { get; set; }
    public long AmountSats { get; set; }
    public DateTime PaidAt { get; set; }
    public string? Bolt11 { get; set; }
    public string? TransactionId { get; set; }
}
```

#### Added Static Methods
- `RegisterRecentlyPaidInvoice()`: Registers an invoice as recently paid
- `GetRecentlyPaidInvoice()`: Retrieves a recently paid invoice by ID or payment hash
- `CleanupExpiredRecentlyPaidInvoices()`: Removes expired entries (older than 5 minutes)

#### Updated GetInvoice Methods
Both `GetInvoice(string)` and `GetInvoice(uint256)` now:
1. First check the recently paid cache
2. If found, immediately return the invoice as paid
3. Otherwise, proceed with normal API query

### 2. FlashPaymentService.cs Updates

#### Updated PayInvoiceAsync Method
After successful payment:
1. Immediately registers the invoice in the static collection
2. Stores both by payment hash and invoice ID (if available)
3. Includes the BOLT11, amount, and transaction details

#### Updated GetPaymentAsync Method
1. First checks the recently paid cache
2. If found, returns the payment as complete
3. Otherwise, proceeds with normal API query

## Benefits

1. **Immediate Availability**: Payments are immediately available for query after successful payment
2. **No API Dependency**: Eliminates race conditions with Flash API indexing
3. **Automatic Cleanup**: Entries expire after 5 minutes to prevent memory buildup
4. **Dual Lookup**: Supports lookup by both invoice ID and payment hash
5. **Thread-Safe**: Uses ConcurrentDictionary for thread safety

## Testing

Created `test-recently-paid.cs` to verify:
- Registration of recently paid invoices
- Retrieval by invoice ID
- Retrieval by payment hash
- Cache expiration behavior

## Usage

The fix works automatically:
1. When a payment succeeds, it's registered in the cache
2. When BTCPay queries for the invoice/payment, it finds it immediately
3. After 5 minutes, the cache entry expires and normal API queries take over