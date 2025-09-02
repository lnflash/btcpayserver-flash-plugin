# LNURL Fix V2 - Enhanced Logging

## Problem
The Flash plugin's LNURL implementation fails with "failed to fetch lnurl invoice" errors. Investigation shows that `FlashInvoiceService` is being called and throwing a `NullReferenceException` when trying to create invoices for LNURL callbacks.

## Solution Implemented
1. Created `FlashSimpleInvoiceService` that bypasses the complex GraphQL client and makes direct HTTP requests
2. Modified `FlashLNURLController` to use `FlashSimpleInvoiceService` directly
3. Added extensive logging to trace the exact code path being executed

## Files Modified

### /Controllers/FlashLNURLController.cs
- Added logging at the start of `LnurlPayCallback` method
- Added logging before creating `FlashSimpleInvoiceService`
- Added logging before calling `CreateInvoiceAsync`
- Uses `FlashSimpleInvoiceService` directly without dependency injection

### /Services/FlashSimpleInvoiceService.cs  
- Created new service that makes direct HTTP POST requests to Flash GraphQL API
- Manually sets authorization headers (bypasses GraphQL.Client issues)
- Added logging to track when the service is called

## Enhanced Logging Added
The following log messages will help identify which code path is being executed:

1. `=== FlashLNURLController.LnurlPayCallback CALLED ===`
   - Shows when the controller method is hit
   - Logs storeId, cardId, amount, and comment

2. `=== Creating FlashSimpleInvoiceService ===`
   - Shows when the simple invoice service is being instantiated
   - Logs the API endpoint and token presence

3. `=== FlashSimpleInvoiceService.CreateInvoiceAsync CALLED ===`
   - Shows when the simple service's create method is called
   - Logs the amount and description

## Deployment Instructions

1. Install the updated plugin package:
   ```bash
   ./build-package.sh
   # Package created at: bin/Release/BTCPayServer.Plugins.Flash.btcpay
   ```

2. Upload to BTCPay Server and restart

3. Monitor logs for the new log messages to confirm the correct code path is being executed

## Expected Log Output

When an LNURL callback is received, you should see:
```
info: BTCPayServer.Plugins.Flash.Controllers.FlashLNURLController
      === FlashLNURLController.LnurlPayCallback CALLED ===
info: BTCPayServer.Plugins.Flash.Controllers.FlashLNURLController
      StoreId: xxx, CardId: xxx, Amount: xxx, Comment: xxx
info: BTCPayServer.Plugins.Flash.Controllers.FlashLNURLController
      === Creating FlashSimpleInvoiceService ===
info: BTCPayServer.Plugins.Flash.Services.FlashSimpleInvoiceService
      === FlashSimpleInvoiceService.CreateInvoiceAsync CALLED ===
```

If you see logs from `FlashInvoiceService` instead, then the wrong code path is being executed and we need to investigate further.

## Troubleshooting

If the fix doesn't work after deployment:

1. **Check if the controller is being hit**: Look for the `=== FlashLNURLController.LnurlPayCallback CALLED ===` message
   - If NOT present: The route might not be registered or another handler is intercepting
   - If present: Continue to next step

2. **Check if FlashSimpleInvoiceService is being used**: Look for `=== Creating FlashSimpleInvoiceService ===`
   - If NOT present: The controller code might not be updated
   - If present: The simple service is being instantiated correctly

3. **Check if the simple service method is called**: Look for `=== FlashSimpleInvoiceService.CreateInvoiceAsync CALLED ===`
   - If NOT present: There might be an error before reaching the create method
   - If present: Check for any error messages after this point

## Next Steps

After deploying with enhanced logging:
1. Test LNURL callback with a flashcard
2. Check logs to confirm the correct code path
3. If `FlashInvoiceService` errors still appear, we need to find what's calling it
4. If `FlashSimpleInvoiceService` is called but fails, check the error details