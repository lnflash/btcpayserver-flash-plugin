# Flash Plugin Pull Payment Guide

This guide covers how to use Pull Payments with the Flash plugin, including handling LNURL destinations, no-amount invoices, and troubleshooting common issues.

## Pull Payment Overview

Pull Payments allow you to create a payment request that can be claimed by recipients using their Lightning wallets. When using Flash with Pull Payments, you need to be aware of certain behaviors, especially when dealing with LNURL destinations and no-amount invoices.

## Pull Payment Support Options

The Flash plugin now supports the following claim methods:

1. **LNURL Destinations**: Recipients can scan the LNURL QR code with their Lightning wallet
2. **Paste LNURL String**: A Lightning LNURL string can be directly pasted into the claim form
3. **BOLT11 Lightning Invoice**: A standard Lightning invoice can be pasted into the claim form

When using LNURL destinations, the payment status may initially show as "in-flight" while processing. This is normal behavior due to the asynchronous nature of the payment flow between BTCPay Server and the Flash API.

## Important Notes on Flash Payment Limits

### Minimum Payment Amount

When using Flash to make Lightning payments, it's important to be aware that Flash's Lightning backend (IBEX) enforces a minimum payment threshold:

- **Minimum payment amount**: 10,000 satoshis (approximately $10 USD)
- Payments below this threshold may fail with an `IBEX_ERROR`
- This applies to both amount and no-amount invoices

The Flash plugin now automatically enforces this minimum threshold by:

1. Detecting small amounts (below 10,000 satoshis) 
2. Automatically increasing them to the safe minimum
3. Providing clear error messages when IBEX rejects payments

### IBEX Error Handling

If you see an error message containing "IBEX_ERROR" in the logs or UI, this indicates that Flash's Lightning backend rejected the payment. Common causes include:

- Payment amount below the minimum threshold
- Temporary network routing issues
- Node connection problems

**Best Practice**: Always use an invoice with an explicit amount of at least 10,000 satoshis when possible.

## Payment Status Tracking

The plugin implements a sophisticated payment tracking system to handle asynchronous payment flows:

### LNURL Payment Status Display

When an LNURL destination is used for a pull payment claim, the payment status may show:

- **"The payment has been initiated but is still in-flight"** - This means:
  - The payment has been submitted to the Flash API
  - The plugin hasn't yet received confirmation of completion
  - The payment may still complete successfully even with this message

For LNURL payments, this message is expected and normal. The payment is tracked internally and assumed to complete successfully after a reasonable time period.

### Payment Tracking Components

The plugin uses several strategies to track payment status:

1. **Recent Payment Cache**: Tracks payments submitted in the last 60 seconds
2. **Multi-hash Tracking**: Generates and tracks multiple possible hash formats that BTCPay might use
3. **Hash Matching**: Attempts to associate unknown payment hashes with known LNURL payments
4. **Status Propagation**: Provides consistent responses for subsequent payment status requests

## No-Amount Invoice Handling

The Flash API has some limitations when it comes to handling no-amount invoices, especially with USD wallets. Here's what you need to know:

### How It Works 

1. When you create a pull payment in BTCPay Server, the amount is tracked internally
2. When a recipient claims the pull payment with a Lightning invoice, the plugin:
   - Attempts to decode the invoice to determine if it has an amount
   - If it's a no-amount invoice and using a USD wallet, the plugin tries multiple strategies to find the amount:
     1. Looks for stored pull payment amount in dedicated tracking storage
     2. Attempts to extract amount information from invoice metadata
     3. Performs direct BOLT11 string parsing to identify amount if present
     4. Uses fallback mechanisms if all else fails
   - For LNURL addresses, the amount is passed along to ensure it's included in the final invoice

### Enhanced No-Amount Invoice Support

The latest version includes several improvements for handling no-amount invoices:

1. **Redundant Amount Storage**: The amount is now stored in multiple places to ensure it remains available
2. **Advanced BOLT11 Parsing**: Direct parsing of BOLT11 strings to extract amount information even when API decoding fails
3. **Detailed Diagnostics**: Comprehensive logging at each step of the amount determination process
4. **Multiple Fallback Mechanisms**: Layered approach ensures the highest possible chance of successful payment

These improvements make the Flash plugin more reliable with a wider range of Lightning wallets, even those that primarily use no-amount invoices.

### Best Practices

For the most reliable operation:

1. **Use Invoices with Amounts**: When possible, use Lightning invoices that include an amount
2. **Check Logs for Diagnostic Information**: If you encounter issues, the logs will contain detailed information about what's happening
3. **Small Test Claims First**: When using a new wallet for claims, start with small amounts to verify compatibility

## Troubleshooting Pull Payments

### Common Issues and Solutions

#### "The payment has been initiated but is still in-flight"

This message appears for LNURL payments where the plugin doesn't have direct confirmation of payment completion.

**Solutions:**
- This is normal for LNURL payments and doesn't necessarily indicate a problem
- Check your Flash wallet to see if the payment was actually completed
- For more reliable status tracking, use BOLT11 invoices instead of LNURL destinations

#### "No-amount invoice requires an amount parameter for USD wallet"

This error occurs when:
- The recipient provided a no-amount invoice
- The plugin can't determine the amount from the invoice
- The plugin can't find the original pull payment amount in its tracking system

**Solutions:**
- Ask the recipient to provide an invoice with an amount specified
- Check if the pull payment was created recently (amount tracking works best for recently created pull payments)
- Verify that the pull payment amount is non-zero and valid
- Check that the wallet currency is correctly detected as USD

#### "Failed to pay no-amount invoice: Amount information was not available"

This error occurs when all fallback methods for determining the amount have failed.

**Solutions:**
- Use invoices with explicit amounts
- For recurring pull payments, recreate the pull payment to refresh the amount tracking
- Check logs for more detailed diagnostic information

## Implementation Details

The plugin implements several strategies to handle different payment scenarios:

1. **Amount Tracking**: The amount from pull payment creation is stored for later use
2. **Invoice Decoding**: Multiple methods to extract amount information from invoices
3. **LNURL Resolution**: Amount information is preserved when resolving LNURL addresses
4. **Fallback Decoders**: Custom BOLT11 parsing when the API's decoder is unavailable
5. **Currency Conversion**: Dynamic conversion from satoshis to USD cents using current exchange rates
6. **Payment Status Tracking**: Sophisticated system for tracking asynchronous payment flows

### Currency Conversion

When using Flash's USD wallet with no-amount invoices, the plugin performs a dynamic currency conversion:

1. The amount is initially stored in satoshis (Bitcoin's smallest unit)
2. For USD wallet payments, this amount needs to be converted to US cents
3. The plugin queries the Flash API for the current BTC/USD exchange rate
4. If the Flash API is unavailable, the plugin automatically tries alternative sources:
   - CoinGecko API for real-time Bitcoin price
   - CoinDesk Bitcoin Price Index as a secondary fallback
   - Conservative estimate only if all other sources fail
5. This real-time rate is used to calculate the exact USD cent amount
6. The converted amount is passed to the `lnNoAmountUsdInvoicePaymentSend` mutation

For example:
- Current BTC price: $64,000 USD
- A 10,000 satoshi payment is converted as follows:
  - 10,000 satoshis = 0.0001 BTC
  - 0.0001 BTC × $64,000 = $6.40 USD
  - $6.40 USD = 640 cents

The plugin implements intelligent caching of exchange rates to minimize API calls while ensuring accurate pricing. This multi-tier approach ensures the plugin continues to work reliably even during API outages or rate fluctuations.

## Best Practices for Different Wallet Types

### For Recipients Using Bitcoin Lightning Wallets

- Most Lightning wallets let you specify an amount when claiming a pull payment
- Using this feature creates an invoice with an amount, which works best with Flash

### For Recipients Using USD Lightning Wallets

- Flash's USD wallet requires an amount for no-amount invoices
- The plugin tries to track and provide this amount automatically
- If issues persist, suggest using a wallet that creates invoices with amounts

## Testing Your Setup

To verify that your Flash plugin is correctly handling different pull payment scenarios:

1. Create a small test pull payment (e.g. $1)
2. Try claiming with different methods:
   - Scan the LNURL QR code with a Lightning wallet
   - Paste an LNURL string directly into the claim form
   - Paste a BOLT11 invoice into the claim form
3. Check the logs for any errors or warnings
4. The payment status may show as "in-flight" for LNURL claims - this is normal
5. Verify the payment in your Flash wallet to confirm completion
6. If you encounter issues, try using a BOLT11 invoice with an explicit amount 