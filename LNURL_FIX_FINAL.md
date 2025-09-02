# LNURL Fix - Final Solution

## Problem Root Cause
1. The Flash plugin's LNURL controller routes are NOT being registered (marked as "Controllers temporarily removed for UI rebuild")
2. BTCPay Server's built-in LNURL support is handling the requests and calling `FlashLightningClient.CreateInvoice`
3. `FlashLightningClient` uses `FlashInvoiceService` which relies on the GraphQL client
4. The GraphQL client fails with "Unauthorized" errors when making HTTP requests (though WebSocket works fine)

## Solution Implemented
Modified `FlashLightningClient.CreateInvoice` methods to use `FlashSimpleInvoiceService` directly, bypassing the broken GraphQL client. This fix works at the Lightning client level since our LNURL controller isn't being loaded.

## Files Modified

### /FlashLightningClient.cs
- Added `_endpoint` field to store the API endpoint
- Modified both `CreateInvoice` overloads to:
  1. First try using `FlashSimpleInvoiceService` (which makes direct HTTP requests)
  2. Fall back to the standard `FlashInvoiceService` if the simple service fails
- Added extensive logging with `===` markers to track execution

### /Services/FlashSimpleInvoiceService.cs (Previously created)
- Makes direct HTTP POST requests to Flash GraphQL API
- Manually sets authorization headers
- Bypasses the problematic GraphQL.Client library

### /Controllers/FlashLNURLController.cs (Previously modified but not active)
- Contains enhanced logging but won't be called since controllers are disabled
- Would use `FlashSimpleInvoiceService` if it were active

## How It Works

When BTCPay Server receives an LNURL callback request:
1. BTCPay's built-in LNURL handler processes it
2. Calls `FlashLightningClient.CreateInvoice`
3. Our modified method detects this and uses `FlashSimpleInvoiceService`
4. The simple service makes a direct HTTP request with proper auth headers
5. Returns the invoice successfully

## Deployment

1. Install the updated plugin package:
   ```bash
   # Package location: bin/Release/BTCPayServer.Plugins.Flash.btcpay
   ```

2. Upload to BTCPay Server and restart

3. Monitor logs for these new messages:
   - `=== Attempting to use FlashSimpleInvoiceService for invoice creation ===`
   - `=== Successfully created invoice via FlashSimpleInvoiceService: [id] ===`

## Expected Behavior

When an LNURL invoice is requested, you should see in the logs:
```
info: BTCPayServer.Plugins.Flash.FlashLightningClient
      === Attempting to use FlashSimpleInvoiceService for invoice creation ===
info: BTCPayServer.Plugins.Flash.Services.FlashSimpleInvoiceService
      === FlashSimpleInvoiceService.CreateInvoiceAsync CALLED ===
info: BTCPayServer.Plugins.Flash.FlashLightningClient
      === Successfully created invoice via FlashSimpleInvoiceService: [invoice-id] ===
```

## Why This Works

1. **Bypasses GraphQL.Client**: The problematic library that doesn't handle auth headers correctly
2. **Direct HTTP requests**: Simple HttpClient with manually set headers works reliably
3. **Works with disabled controllers**: Since the fix is in the Lightning client, it works even though our LNURL controller isn't loaded
4. **Fallback mechanism**: If the simple service fails, it falls back to the standard service

## Testing

1. Generate an LNURL for a flashcard in BTCPay
2. Scan with any LNURL-compatible wallet
3. The wallet should successfully receive an invoice
4. Check logs to confirm `FlashSimpleInvoiceService` was used

## Future Improvements

Once the Flash plugin controllers are re-enabled:
1. The `FlashLNURLController` will handle LNURL requests directly
2. Remove the workaround from `FlashLightningClient`
3. Consider fixing the GraphQL.Client authorization issue properly