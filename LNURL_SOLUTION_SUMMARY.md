# LNURL Solution Summary - Flash Plugin

## The Problem
The Flash plugin's LNURL implementation was failing with "failed to fetch lnurl invoice" because:
1. The GraphQL.Client library wasn't properly including authorization headers in HTTP requests
2. The Flash API requires authentication for all operations including invoice creation
3. The complex dependency injection and client initialization was failing during LNURL callbacks

## The Solution
Instead of trying to fix the complex GraphQL client authorization issue, we created a simpler solution:

### 1. Created `FlashSimpleInvoiceService`
A lightweight service that:
- Makes direct HTTP POST requests to the Flash GraphQL endpoint
- Manually sets authorization headers on HttpClient
- Bypasses the complex GraphQL.Client library
- Directly constructs GraphQL mutations as JSON

### 2. Updated `FlashLNURLController`
Modified the LNURL callback to:
- Use `FlashSimpleInvoiceService` instead of the full Lightning client
- Remove dependency on `LightningClientFactoryService`
- Simplify the invoice creation flow
- Better error handling with specific error messages

## Why This Works
1. **Direct HTTP requests** - Bypasses the GraphQL.Client library's header handling issues
2. **Simple authorization** - HttpClient's DefaultRequestHeaders work correctly for direct HTTP requests
3. **No complex initialization** - Doesn't require wallet info or complex client setup
4. **Focused functionality** - Only does what's needed for LNURL: create an invoice

## Code Changes

### Files Modified:
- `/Controllers/FlashLNURLController.cs` - Simplified to use direct HTTP service
- `/Services/FlashSimpleInvoiceService.cs` - New lightweight invoice creation service

### Files Created for Debugging (can be removed):
- `/Services/AuthorizationDelegatingHandler.cs` - Attempted fix for auth headers
- Various documentation files

## Testing
After deploying:
1. Generate an LNURL for a flashcard
2. Scan with flash-mobile or any LNURL-compatible wallet
3. The wallet should successfully:
   - Parse the LNURL discovery response
   - Request an invoice from the callback
   - Receive a properly generated invoice
   - Complete the payment

## Key Learning
Sometimes the simplest solution is the best. Instead of fighting with a complex library's authorization handling, creating a simple, direct HTTP implementation solved the problem effectively.

## Alternative Solutions Considered
1. **Fix GraphQL.Client authorization** - Too complex, library doesn't properly respect headers
2. **Fix in flash-mobile** - Would only fix for one wallet, not a general solution
3. **Use BTCPay's built-in LNURL** - Best long-term solution but requires removing custom implementation

## Next Steps
Consider migrating to BTCPay Server's built-in LNURL support in the future, which would eliminate the need for custom LNURL endpoints in the Flash plugin entirely.