# Pull Payment Payout Dashboard Requirements

## Overview

Create a comprehensive dashboard on the Flash Plugin page (`https://btcpay.test.flashapp/plugins/flash`) to track and display pull payment payouts with enhanced Boltcard tracking capabilities.

## Business Requirements

### Primary Goals
1. **Visibility**: Provide merchants with real-time visibility into pull payment payouts
2. **Tracking**: Track which Boltcards are being used to collect payouts
3. **Analytics**: Display payout statistics and trends
4. **Audit Trail**: Maintain a complete history of all payouts

### Key Features
1. **Payout List View**
   - Display all pull payment payouts (pending, processing, completed, failed)
   - Show payout amount, status, timestamp, and recipient details
   - Include Boltcard identifier when available

2. **Boltcard Association**
   - Capture unique Boltcard identifier during payout collection
   - Link payouts to specific Boltcards
   - Track payout frequency per card

3. **Dashboard Metrics**
   - Total payouts (count and value)
   - Payouts by status
   - Daily/weekly/monthly trends
   - Most active Boltcards

4. **Search and Filter**
   - Filter by date range
   - Filter by status
   - Search by Boltcard ID
   - Search by payout ID or amount

## Technical Requirements

### Data Model

```csharp
public class FlashPayout
{
    public string PayoutId { get; set; }
    public string PullPaymentId { get; set; }
    public string StoreId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public string Status { get; set; } // Pending, Processing, Completed, Failed
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? BoltcardId { get; set; }
    public string? BoltcardNtag { get; set; }
    public string? RecipientAddress { get; set; }
    public string? TransactionId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BoltcardUsage
{
    public string BoltcardId { get; set; }
    public string? Alias { get; set; }
    public int TotalPayouts { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime FirstUsed { get; set; }
    public DateTime LastUsed { get; set; }
}
```

### API Endpoints

1. **GET /plugins/flash/api/payouts**
   - Parameters: storeId, startDate, endDate, status, boltcardId, limit, offset
   - Returns: Paginated list of payouts

2. **GET /plugins/flash/api/payouts/{payoutId}**
   - Returns: Detailed payout information

3. **GET /plugins/flash/api/payouts/stats**
   - Parameters: storeId, startDate, endDate
   - Returns: Dashboard statistics

4. **GET /plugins/flash/api/boltcards/usage**
   - Parameters: storeId, startDate, endDate
   - Returns: Boltcard usage statistics

### UI Components

1. **Dashboard Header**
   - Summary cards showing key metrics
   - Date range selector
   - Refresh button

2. **Payouts Table**
   - Sortable columns
   - Status badges
   - Click-through to payout details
   - Pagination controls

3. **Charts**
   - Line chart for payout trends
   - Pie chart for status distribution
   - Bar chart for top Boltcards

4. **Detail Modal**
   - Full payout information
   - Transaction details
   - Boltcard information (if available)

## Implementation Plan

### Phase 1: Backend Infrastructure (3-4 days)

1. **Database Schema**
   - Create tables for payout tracking
   - Add indexes for performance
   - Migration scripts

2. **Data Collection Service**
   - Hook into existing payout processing
   - Capture Boltcard identifiers
   - Store payout events

3. **API Development**
   - Implement REST endpoints
   - Add authentication/authorization
   - Create DTOs and response models

### Phase 2: Frontend Dashboard (4-5 days)

1. **Create Dashboard View**
   - Set up routing
   - Create layout structure
   - Implement responsive design

2. **Build UI Components**
   - Summary cards
   - Data table with sorting/filtering
   - Charts using Chart.js or similar

3. **Integrate with API**
   - Fetch data from endpoints
   - Implement real-time updates
   - Handle loading/error states

### Phase 3: Boltcard Integration (2-3 days)

1. **Enhance Payout Flow**
   - Modify payout collection to capture Boltcard ID
   - Update LNURL flow to include card info
   - Store association in database

2. **Boltcard Management**
   - Allow aliasing of Boltcard IDs
   - Track card usage patterns
   - Export card usage reports

### Phase 4: Testing & Polish (2-3 days)

1. **Testing**
   - Unit tests for new services
   - Integration tests for API
   - UI testing

2. **Performance Optimization**
   - Database query optimization
   - Caching frequently accessed data
   - Pagination for large datasets

3. **Documentation**
   - API documentation
   - User guide
   - Administrator guide

## Technical Considerations

### Boltcard Identification

The Boltcard NTAG UID can be captured during the LNURL-withdraw flow:

1. When a Boltcard initiates a payout collection, the LNURL request includes card data
2. Extract the NTAG UID from the request headers or parameters
3. Associate this with the payout record
4. Store for future reference and analytics

### Data Storage Options

1. **Option 1: Extend BTCPay Database**
   - Add new tables to existing PostgreSQL/SQLite database
   - Leverage existing migration infrastructure
   - Easiest integration

2. **Option 2: Separate Storage**
   - Use dedicated storage for plugin data
   - More flexibility but increased complexity
   - Consider for future scalability

### Real-time Updates

1. **WebSocket Integration**
   - Push updates to dashboard when payouts change status
   - Show live notifications for new payouts
   - Update metrics in real-time

2. **Polling Fallback**
   - Refresh data every 30 seconds if WebSocket unavailable
   - Allow manual refresh

## Security Considerations

1. **Access Control**
   - Dashboard only visible to store owners/managers
   - API endpoints require authentication
   - Respect BTCPay Server permissions

2. **Data Privacy**
   - Don't expose sensitive payment details
   - Allow merchants to opt-out of Boltcard tracking
   - Provide data export/deletion capabilities

3. **Rate Limiting**
   - Protect API endpoints from abuse
   - Implement request throttling
   - Cache frequently accessed data

## Future Enhancements

1. **Advanced Analytics**
   - Predictive analytics for payout patterns
   - Fraud detection based on usage patterns
   - Integration with external analytics tools

2. **Automated Actions**
   - Alert on suspicious activity
   - Auto-approve trusted Boltcards
   - Batch payout processing

3. **Export Capabilities**
   - CSV/Excel export
   - API for external integrations
   - Scheduled reports

## Success Criteria

1. **Functional Requirements**
   - All payouts are tracked and displayed
   - Boltcard associations are captured when available
   - Dashboard loads within 2 seconds
   - Data is accurate and up-to-date

2. **User Experience**
   - Intuitive navigation
   - Clear visual hierarchy
   - Responsive on all devices
   - Helpful error messages

3. **Performance**
   - Dashboard handles 10,000+ payouts efficiently
   - API responds within 500ms
   - Real-time updates work reliably

## Estimated Timeline

- **Total Duration**: 12-15 days
- **Phase 1**: 3-4 days (Backend)
- **Phase 2**: 4-5 days (Frontend)
- **Phase 3**: 2-3 days (Boltcard Integration)
- **Phase 4**: 2-3 days (Testing & Polish)

## Dependencies

1. **BTCPay Server**: v2.0.0+
2. **Flash Plugin**: v1.4.1+
3. **Frontend Framework**: ASP.NET Core MVC/Razor Pages
4. **Charting Library**: Chart.js or similar
5. **Database**: PostgreSQL/SQLite (matching BTCPay)