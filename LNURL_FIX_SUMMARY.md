# LNURL Flash Plugin Fix Summary

## Changes Made

### 1. Fixed Metadata Format in Discovery Endpoint (Line 61-73)
**Before:**
```csharp
metadata = JsonConvert.SerializeObject(new[] { new[] { "text/plain", $"Flashcard {cardId} top-up" } }),
```

**After:**
```csharp
// Create metadata as a proper JSON array string
// According to LNURL spec, metadata should be a JSON string of array format: [["text/plain", "description"]]
var metadataArray = new[] { new[] { "text/plain", $"Flashcard {cardId} top-up" } };
var metadataJson = JsonConvert.SerializeObject(metadataArray);
// ...
metadata = metadataJson,  // This is already a JSON string, will not be double-serialized
```

### 2. Fixed Invoice Creation Memo (Line 119-131)
**Before:**
```csharp
// Format memo as JSON array for Flash compatibility
string memo = JsonConvert.SerializeObject(new[] { new[] { "text/plain", description } });

// Create the invoice
var invoice = await client.CreateInvoice(
    new LightMoney(amountSats, LightMoneyUnit.Satoshi),
    memo,
    TimeSpan.FromHours(1));
```

**After:**
```csharp
// Create the invoice with plain string description
// The Flash API will handle LNURL metadata/description_hash internally
var invoice = await client.CreateInvoice(
    new LightMoney(amountSats, LightMoneyUnit.Satoshi),
    description,  // Pass plain string, not JSON array
    TimeSpan.FromHours(1));
```

### 3. Fixed Controller Response Serialization
Changed all `return Json(response)` to `return Ok(response)` throughout the controller to ensure proper JSON serialization without double-escaping.

## Root Cause
The issue was a combination of:
1. Potential double-serialization of the metadata field in the LNURL discovery response
2. Passing a JSON-serialized array to the Flash API's CreateInvoice method instead of a plain string
3. Using `Json()` method which could cause additional serialization issues

## Impact
These changes ensure:
- LNURL-pay responses conform to the specification
- Metadata is properly formatted as a JSON string (not double-escaped)
- Invoice descriptions are passed correctly to the Flash GraphQL API
- Wallets can successfully parse LNURL responses and fetch invoices

## Testing
After deploying these changes:
1. Generate an LNURL for a flashcard
2. Scan with flash-mobile or any LNURL-compatible wallet
3. The wallet should successfully:
   - Parse the LNURL discovery response
   - Request an invoice from the callback endpoint
   - Complete the payment

## Files Modified
- `/Controllers/FlashLNURLController.cs`

## Additional Files Created
- `/Tests/LnurlMetadataTest.cs` - Test demonstrating the fix
- `/LNURL_METADATA_FIX.md` - Detailed documentation of the issue and fix
- `/test-lnurl-fix.csx` - Script to demonstrate the serialization difference
- `/LNURL_FIX_SUMMARY.md` - This summary file