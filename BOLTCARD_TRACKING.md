# Enhanced Boltcard Tracking and Reporting

This document describes the enhanced Boltcard payment tracking and reporting features implemented in the Flash BTCPayServer plugin.

## Overview

The Flash plugin now includes advanced tracking capabilities specifically designed for Boltcard NFC payment transactions, with dedicated UI reporting and statistics.

## Features

### 🔧 Enhanced Payment Detection

- **Multi-Method Detection**: Uses multiple approaches to detect successful payments:
  - Flash transaction history monitoring
  - Account balance change detection
  - Fallback assumption for small amounts after timeout

- **Smart Timeout Handling**: 
  - Small amounts (≤$0.50): Assumes success after 2 minutes
  - Larger amounts: Continues monitoring with detailed error logging

- **Frequent Polling**: Checks payment status every 5 seconds for Boltcard transactions

### 🏷️ Boltcard ID Extraction

The system automatically extracts Boltcard IDs from transaction memos using multiple patterns:

```csharp
// Supported patterns:
- "Boltcard {ID}"
- "Card ID: {ID}" 
- "ID: {ID}"
- Generic alphanumeric IDs (8-16 characters)
- JSON format: [["text/plain","Boltcard Top-Up"]]
```

If no ID is found, generates a deterministic hash from the memo content.

### 📊 Transaction Tracking

Each Boltcard transaction is tracked with:

```csharp
public class BoltcardTransaction
{
    public string InvoiceId { get; set; }     // Lightning invoice ID
    public string BoltcardId { get; set; }    // Extracted card ID
    public long AmountSats { get; set; }      // Amount in satoshis
    public DateTime CreatedAt { get; set; }   // Transaction timestamp
    public string Status { get; set; }        // Pending/Paid/Timeout
    public DateTime? PaidAt { get; set; }     // Payment confirmation time
    public string? TransactionHash { get; set; } // Flash transaction hash
}
```

### 📈 Statistics and Reporting

#### Available Statistics:
- Total transactions count
- Unique cards count  
- Total amount processed (in sats)
- Success rate percentage
- Last 24 hours transaction count

#### API Endpoints:
- `GET /plugins/{storeId}/Flash/boltcard` - Dashboard view
- `GET /plugins/{storeId}/Flash/boltcard/transactions` - Transaction history
- `GET /plugins/{storeId}/Flash/boltcard/card/{cardId}` - Individual card details
- `GET /plugins/{storeId}/Flash/boltcard/api/stats` - JSON statistics API

## Usage

### 1. Accessing the Dashboard

Navigate to your BTCPayServer store settings and look for the "Flash Boltcard Dashboard" option in the plugin menu.

Direct URL: `/plugins/{your-store-id}/Flash/boltcard`

### 2. Viewing Statistics

The dashboard shows:
- **Overview Cards**: Total transactions, unique cards, total amount, success rate
- **Recent Activity**: Last 20 Boltcard transactions with real-time updates
- **Individual Card Analysis**: Click any card ID to see detailed transaction history

### 3. Programmatic Access

Use the JSON API endpoint for integrations:

```javascript
// Get current statistics
fetch('/plugins/your-store-id/Flash/boltcard/api/stats')
  .then(response => response.json())
  .then(stats => {
    console.log(`Success rate: ${stats.successRate}%`);
    console.log(`Total processed: ${stats.totalAmountSats} sats`);
  });
```

## How It Works

### Payment Detection Flow

1. **Invoice Creation**: When a Boltcard topup invoice is created:
   ```
   Amount < 10,000 sats OR memo contains "boltcard" → Identified as Boltcard
   ```

2. **ID Extraction**: Extract unique ID from memo using pattern matching

3. **Enhanced Tracking**: Start background monitoring:
   - Check Flash transaction history every 5 seconds
   - Monitor account balance changes for small amounts
   - Apply smart timeout rules based on amount

4. **Status Updates**: Mark as paid when detection succeeds or timeout rules apply

### Detection Methods

#### Method 1: Transaction History
```csharp
// Looks for incoming transactions matching:
- Direction: "receive"
- Amount: Within 10 sats of expected
- Status: "success" 
- Recent: Within last 20 transactions
```

#### Method 2: Balance Monitoring
```csharp
// For small amounts, monitors USD wallet balance:
- Expected increase = (sats * exchange_rate) / 100
- Tolerance: ±10% of expected increase
- Updates cached balance for future comparisons
```

#### Method 3: Smart Timeout
```csharp
// Fallback assumptions:
- Amounts ≤ $0.50: Assume paid after 2 minutes
- Amounts > $0.50: Mark as timeout, require manual verification
```

## Configuration

### Boltcard Detection Criteria

Invoices are identified as Boltcard transactions if:
```csharp
bool isBoltcard = amountSats < 10000 || memo.ToLowerInvariant().Contains("boltcard");
```

You can customize this logic in `FlashLightningClient.cs` around line 385.

### Timeout Settings

Current timeouts in `EnhancedBoltcardTracking()`:
```csharp
var maxWaitTime = TimeSpan.FromMinutes(2);     // Maximum monitoring time
var pollInterval = TimeSpan.FromSeconds(5);    // Check frequency
long assumeSuccessThreshold = 5000;            // Auto-success for ≤ $0.50
```

## Troubleshooting

### Common Issues

1. **Transactions not detected**: Check BTCPayServer logs for `[BOLTCARD DEBUG]` entries
2. **Wrong card IDs**: Ensure memo format matches supported patterns
3. **False timeouts**: Adjust `assumeSuccessThreshold` for your use case

### Debug Logging

Enable detailed logging by checking for these log entries:
```
[BOLTCARD DEBUG] Identified as Boltcard invoice - Amount: {sats} sats
[BOLTCARD DEBUG] Extracted Boltcard ID: {id}
[BOLTCARD DEBUG] Payment detected via transaction history: {hash}
[BOLTCARD DEBUG] Payment detected via balance increase: {hash}
```

### Performance Considerations

- Background tracking tasks run independently per transaction
- Statistics are computed in-memory (no database queries)
- UI auto-refreshes every 30 seconds
- Transaction history is capped at recent transactions to prevent memory issues

## Integration Examples

### Store Integration

```csharp
// Get Flash client instance
var flashClient = GetFlashLightningClient(storeId);

// Get statistics
var stats = flashClient.GetBoltcardStats();
Console.WriteLine($"Success rate: {stats.SuccessRate:F1}%");

// Get card-specific history  
var cardTransactions = flashClient.GetBoltcardTransactionsByCardId("card123", 50);
Console.WriteLine($"Card has {cardTransactions.Count} transactions");
```

### Real-time Monitoring

```javascript
// Auto-updating dashboard
setInterval(() => {
    fetch('/plugins/store123/Flash/boltcard/api/stats')
        .then(response => response.json())
        .then(updateDashboard);
}, 10000);
```

## Security Notes

- All Boltcard data is stored in-memory only (not persisted to database)
- Transaction data is automatically cleaned up after 24 hours
- Access requires BTCPayServer store-level permissions
- Card IDs are extracted from memo fields (not sensitive payment data)

---

For additional support or feature requests, please contact the Flash team or submit an issue on the BTCPayServer Flash plugin repository. 