# LNURL Metadata Fix for Flash Plugin

## Problem
The Flash plugin's LNURL-pay implementation was causing "failed to fetch lnurl invoice" errors in wallets (particularly flash-mobile) when trying to top up flashcards. The Blink plugin worked fine because it doesn't implement custom LNURL endpoints and relies on BTCPay Server's standard implementation.

## Root Cause
The issue was **double-serialization** of the metadata field in the LNURL-pay response:

### Before (Broken)
```csharp
// Line 66 - Discovery endpoint
metadata = JsonConvert.SerializeObject(new[] { new[] { "text/plain", $"Flashcard {cardId} top-up" } })
```

This resulted in a response like:
```json
{
  "metadata": "[[\"text/plain\",\"Flashcard test-123 top-up\"]]",
  ...
}
```

The metadata field was a **string containing escaped JSON**, which wallets couldn't parse correctly.

### After (Fixed)
```csharp
// Create metadata as a proper JSON array string
var metadataArray = new[] { new[] { "text/plain", $"Flashcard {cardId} top-up" } };
var metadataJson = JsonConvert.SerializeObject(metadataArray);

var response = new
{
    metadata = metadataJson,  // This is already a JSON string, will not be double-serialized
    ...
};
```

This produces the correct response:
```json
{
  "metadata": "[["text/plain","Flashcard test-123 top-up"]]",
  ...
}
```

The metadata field is now a **properly formatted JSON string** that wallets can parse.

## Additional Fixes

### Invoice Creation (Line 122)
Changed from passing a JSON-serialized array to passing a plain string description:

**Before:**
```csharp
string memo = JsonConvert.SerializeObject(new[] { new[] { "text/plain", description } });
var invoice = await client.CreateInvoice(amount, memo, expiry);
```

**After:**
```csharp
var invoice = await client.CreateInvoice(amount, description, expiry);
```

The Flash Lightning client expects a plain string description, not a JSON array.

## LNURL-pay Specification
According to the [LNURL-pay spec](https://github.com/lnurl/luds/blob/luds/06.md):

1. The `metadata` field must be a **JSON string** containing an array of arrays
2. Each inner array has two elements: `[type, content]`
3. The invoice's `description_hash` should be the SHA256 hash of the metadata string
4. Common format: `[["text/plain", "description text"]]`

## Testing
To verify the fix:

1. Deploy the updated Flash plugin
2. Generate an LNURL for a flashcard
3. Scan with a wallet (flash-mobile or any LNURL-compatible wallet)
4. The wallet should successfully fetch the invoice and complete payment

## Files Changed
- `/Controllers/FlashLNURLController.cs` - Fixed metadata serialization in both discovery and callback endpoints
- `/Tests/LnurlMetadataTest.cs` - Added test to demonstrate the fix

## Impact
This fix ensures compatibility with all LNURL-compliant wallets, particularly:
- Flash mobile wallet
- Phoenix wallet
- Breez wallet
- Zeus wallet
- Any other wallet implementing LNURL-pay correctly

## Related Issues
- Flash-mobile had a previous fix (commit bf12d3a3) for URL compatibility and amount rounding
- This fix addresses the server-side metadata format issue that was preventing invoice fetching