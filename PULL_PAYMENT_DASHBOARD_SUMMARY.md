# Pull Payment Dashboard - Implementation Summary

## Overview

We need to add a Pull Payment Payout Dashboard to the Flash Plugin that tracks and displays all pull payment payouts with Boltcard identification capabilities.

## Key Requirements

1. **Dashboard Location**: Add to existing Flash plugin page at `/plugins/flash/payouts`
2. **Core Features**:
   - Display all pull payment payouts (pending, processing, completed, failed)
   - Show payout amount, status, timestamp, and recipient details
   - Capture and display Boltcard identifier when available
   - Summary statistics and metrics

## Quick Start Implementation

### MVP Approach (1 Day)

We've created a simplified implementation plan that can be completed in 1 day:

1. **Add Dashboard Link** to existing Flash index page
2. **Create Basic Controller** for payout display
3. **Implement Simple Tracking** using in-memory storage initially
4. **Build Basic View** with table and summary cards
5. **Hook into Existing** payout processing

### Files to Create/Modify

**New Files:**
- `/Controllers/UIFlashPayoutsController.cs` - Dashboard controller
- `/Services/FlashPayoutTrackingService.cs` - Tracking service
- `/Views/UIFlashPayouts/Dashboard.cshtml` - Dashboard view
- `/Models/FlashPayoutDashboardViewModel.cs` - View models (✅ Already created)

**Modify:**
- `/Views/UIFlash/Index.cshtml` - Add dashboard link
- `/Services/FlashPaymentService.cs` - Add Boltcard tracking
- `FlashPlugin.cs` - Register new services

## Boltcard Tracking Strategy

### During LNURL-withdraw Flow

When a Boltcard is used to collect a payout:

1. Extract Boltcard ID from LNURL request headers/parameters
2. Associate with payout ID
3. Store in tracking service
4. Display in dashboard

### Identification Sources

- **Header**: `X-Boltcard-UID`
- **Query Parameter**: `card` or `boltcard`
- **LNURL Metadata**: Card information in withdraw request

## Implementation Phases

### Phase 1: MVP (1 Day) ✅
- Basic dashboard with payout list
- Summary statistics
- In-memory Boltcard tracking
- Auto-refresh every 30 seconds

### Phase 2: Enhanced (3-5 Days)
- Persistent database storage
- Charts and visualizations
- Advanced filtering and search
- Export functionality

### Phase 3: Advanced (1 Week)
- Real-time updates via WebSocket
- Boltcard usage analytics
- Automated reports
- API endpoints for external integration

## Technical Decisions

1. **Storage**: Start with in-memory, migrate to database later
2. **UI Framework**: Use existing BTCPay Server Razor Pages/Bootstrap
3. **Integration**: Leverage existing BTCPay payout infrastructure
4. **Security**: Respect existing store permissions

## Benefits

1. **Visibility**: Merchants can track all payouts in one place
2. **Accountability**: Know which Boltcards collected payouts
3. **Analytics**: Understand payout patterns and usage
4. **Compliance**: Complete audit trail for financial records

## Next Steps

1. **Review** the implementation plan documents
2. **Approve** the MVP approach
3. **Start** with the simplest implementation
4. **Test** with real pull payments
5. **Iterate** based on user feedback

## Success Metrics

- Dashboard loads in < 2 seconds
- All payouts are tracked accurately
- Boltcard associations work when available
- Users find the interface intuitive

## Estimated Timeline

- **MVP**: 1 day
- **Enhanced Version**: 1 week
- **Full Feature Set**: 2-3 weeks

This approach allows us to deliver value quickly while building toward a comprehensive solution.