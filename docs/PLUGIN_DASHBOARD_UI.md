# Flash Plugin Dashboard UI Documentation

## Overview

The Flash Plugin Dashboard provides a comprehensive interface for managing Lightning payments, monitoring transactions, and configuring plugin settings within BTCPay Server.

## Dashboard Components

### 1. Main Dashboard View

The main dashboard is accessible at `/plugins/flash` and provides:

#### Connection Status Widget
- **Location**: Top of dashboard
- **Components**:
  - Connection indicator (green/red dot)
  - Lightning node alias
  - Current balance in satoshis
  - BTC/USD exchange rate
  - Last sync timestamp

#### Quick Actions Panel
- **Create Invoice**: Generate new Lightning invoices
- **Send Payment**: Quick payment interface
- **View Transactions**: Access transaction history
- **Settings**: Plugin configuration

### 2. Transaction History

#### List View Features
- **Columns**:
  - Date/Time
  - Type (Invoice/Payment)
  - Amount (Sats/USD)
  - Status
  - Payment Hash
  - Description/Memo
  
- **Filters**:
  - Date range selector
  - Transaction type (All/Invoices/Payments)
  - Status filter (Pending/Completed/Failed)
  - Amount range
  - Search by memo/hash

- **Actions**:
  - View details
  - Copy payment request
  - Export to CSV
  - Refresh status

### 3. Invoice Management

#### Create Invoice Interface
```
┌─────────────────────────────────────┐
│ Create Lightning Invoice            │
├─────────────────────────────────────┤
│ Amount: [____] sats  ≈ $___ USD    │
│ Memo:   [_____________________]     │
│ Expiry: [24 hours ▼]               │
│                                     │
│ [Generate Invoice]                  │
└─────────────────────────────────────┘
```

#### Invoice Details View
- QR code display
- Copy-to-clipboard for payment request
- Real-time status updates via WebSocket
- Share options (link, email)
- Invoice metadata display

### 4. Payment Interface

#### Send Payment Form
```
┌─────────────────────────────────────┐
│ Send Lightning Payment              │
├─────────────────────────────────────┤
│ Invoice: [_____________________]    │
│          [Scan QR]                  │
│                                     │
│ Amount:  [Auto-detected]            │
│ Fee:     ~1-3 sats                  │
│                                     │
│ [Pay Invoice]                       │
└─────────────────────────────────────┘
```

### 5. Pull Payment Dashboard (New Feature)

#### Overview Section
- Total payouts created
- Active payouts
- Completed payouts
- Total amount distributed

#### Payout List
```
┌─────────────────────────────────────────────┐
│ Pull Payment Payouts                        │
├─────────────────────────────────────────────┤
│ ID    │ Amount │ Status    │ Collected By  │
├───────┼────────┼───────────┼───────────────┤
│ PP001 │ 1000   │ Completed │ Boltcard-1234 │
│ PP002 │ 500    │ Pending   │ -             │
│ PP003 │ 2000   │ Completed │ Boltcard-5678 │
└─────────────────────────────────────────────┘
```

#### Boltcard Analytics
- Most active Boltcards
- Collection patterns
- Geographic distribution (if available)
- Time-based analytics

### 6. Settings Page

#### Connection Configuration
- API endpoint
- Access token management
- WebSocket settings
- Test connection button

#### Display Preferences
- Default currency display
- Transaction list size
- Date/time format
- Theme selection

#### Advanced Settings
- Cache duration
- Retry policies
- Debug logging
- Export/Import configuration

## UI Components Library

### 1. Status Indicators
```html
<span class="flash-status flash-status--connected">
  <i class="icon-circle"></i> Connected
</span>

<span class="flash-status flash-status--pending">
  <i class="icon-clock"></i> Pending
</span>

<span class="flash-status flash-status--error">
  <i class="icon-alert"></i> Failed
</span>
```

### 2. Amount Display
```html
<div class="flash-amount">
  <span class="flash-amount__sats">1,000</span>
  <span class="flash-amount__currency">sats</span>
  <span class="flash-amount__fiat">≈ $0.43 USD</span>
</div>
```

### 3. Action Buttons
```html
<button class="btn btn-primary flash-action">
  <i class="icon-lightning"></i> Create Invoice
</button>

<button class="btn btn-secondary flash-action">
  <i class="icon-send"></i> Send Payment
</button>
```

### 4. Real-time Updates
```javascript
// WebSocket connection for live updates
const ws = new WebSocket(wsEndpoint);

ws.onmessage = (event) => {
  const update = JSON.parse(event.data);
  if (update.type === 'invoice_paid') {
    updateInvoiceStatus(update.paymentHash, 'paid');
    showNotification('Invoice paid!', 'success');
  }
};
```

## Responsive Design

### Mobile View (< 768px)
- Stacked layout for dashboard widgets
- Simplified transaction list
- Touch-optimized controls
- Swipe actions for common operations

### Tablet View (768px - 1024px)
- Two-column layout
- Collapsible sidebars
- Optimized spacing

### Desktop View (> 1024px)
- Full dashboard with all widgets
- Multi-column transaction view
- Keyboard shortcuts enabled

## Accessibility Features

### ARIA Labels
```html
<button aria-label="Create new Lightning invoice" 
        aria-describedby="invoice-help">
  Create Invoice
</button>
```

### Keyboard Navigation
- Tab order optimization
- Enter/Space for actions
- Escape to close modals
- Arrow keys for lists

### Screen Reader Support
- Descriptive labels
- Status announcements
- Form validation messages
- Progress indicators

## Theme Support

### Light Theme (Default)
```css
:root {
  --flash-primary: #ff9500;
  --flash-background: #ffffff;
  --flash-text: #1a1a1a;
  --flash-border: #e0e0e0;
}
```

### Dark Theme
```css
[data-theme="dark"] {
  --flash-primary: #ffa500;
  --flash-background: #1a1a1a;
  --flash-text: #ffffff;
  --flash-border: #333333;
}
```

## Integration with BTCPay UI

### Navigation Menu
```html
<li class="nav-item">
  <a class="nav-link" href="/plugins/flash">
    <i class="icon-flash"></i>
    <span>Flash Lightning</span>
  </a>
</li>
```

### Store Settings Integration
- Flash appears in store's Lightning payment methods
- Configuration accessible from store settings
- Unified notification system

## Performance Optimizations

### Lazy Loading
- Transaction history pagination
- Image lazy loading for QR codes
- Deferred loading of analytics

### Caching Strategy
- Local storage for user preferences
- Session storage for temporary data
- Service worker for offline support

### Bundle Optimization
- Code splitting by route
- Tree shaking unused components
- Minified CSS/JS in production

## Error Handling UI

### Connection Errors
```html
<div class="alert alert-warning">
  <i class="icon-warning"></i>
  Unable to connect to Flash API. 
  <a href="#" onclick="retryConnection()">Retry</a>
</div>
```

### Transaction Errors
- Clear error messages
- Suggested actions
- Retry mechanisms
- Support contact info

## Analytics Dashboard

### Key Metrics Display
- Transaction volume chart
- Success rate gauge
- Average payment time
- Fee analysis

### Export Options
- CSV export
- PDF reports
- API access for custom analytics

## Future UI Enhancements

### Planned Features
1. **Enhanced Boltcard Management**
   - Card registration UI
   - Usage statistics per card
   - Card limit management

2. **Advanced Analytics**
   - Custom date ranges
   - Comparative analysis
   - Predictive insights

3. **Automation Rules**
   - Auto-convert amounts
   - Scheduled payouts
   - Alert configurations

4. **Multi-language Support**
   - Internationalization framework
   - RTL language support
   - Currency localization

## Development Guidelines

### Component Structure
```
/Views/
  /Flash/
    Dashboard.cshtml
    Transactions.cshtml
    Settings.cshtml
    /Components/
      StatusWidget.cshtml
      TransactionList.cshtml
      InvoiceForm.cshtml
```

### CSS Organization
```
/wwwroot/
  /css/
    flash-plugin.css
    flash-theme-light.css
    flash-theme-dark.css
    flash-responsive.css
```

### JavaScript Modules
```
/wwwroot/
  /js/
    /flash/
      dashboard.js
      websocket.js
      transactions.js
      utils.js
```

## Testing Checklist

### Functional Tests
- [ ] Invoice creation flow
- [ ] Payment sending flow
- [ ] Transaction filtering
- [ ] Settings persistence
- [ ] WebSocket reconnection

### Visual Tests
- [ ] Responsive layouts
- [ ] Theme switching
- [ ] Animation performance
- [ ] Print styles

### Accessibility Tests
- [ ] Keyboard navigation
- [ ] Screen reader compatibility
- [ ] Color contrast ratios
- [ ] Focus indicators

## Conclusion

The Flash Plugin Dashboard provides a modern, intuitive interface for Lightning payment management within BTCPay Server. By following these UI guidelines and patterns, developers can extend and customize the dashboard while maintaining consistency and usability.