# Pull Payment Dashboard Implementation Guide

## Architecture Overview

The dashboard will be implemented as a new section within the Flash Plugin, following BTCPay Server's plugin architecture patterns.

### Component Structure

```
BTCPayServer.Plugins.Flash/
├── Controllers/
│   └── FlashDashboardController.cs      # Main dashboard controller
├── Services/
│   ├── FlashPayoutTrackingService.cs    # Payout tracking logic
│   └── FlashDashboardService.cs         # Dashboard data aggregation
├── Models/
│   ├── FlashPayoutModel.cs              # Payout data model
│   └── FlashDashboardViewModel.cs       # Dashboard view models
├── Views/Flash/
│   ├── Dashboard.cshtml                 # Main dashboard view
│   └── _PayoutTable.cshtml              # Partial view for payout table
├── Data/
│   ├── FlashPayoutRepository.cs         # Data access layer
│   └── Migrations/                      # Database migrations
└── wwwroot/flash/
    ├── js/
    │   └── dashboard.js                 # Dashboard JavaScript
    └── css/
        └── dashboard.css                # Dashboard styles
```

## Phase 1: Backend Implementation

### 1.1 Database Schema

```sql
-- Create payout tracking table
CREATE TABLE flash_payouts (
    id SERIAL PRIMARY KEY,
    payout_id VARCHAR(255) UNIQUE NOT NULL,
    pull_payment_id VARCHAR(255) NOT NULL,
    store_id VARCHAR(255) NOT NULL,
    amount DECIMAL(20, 8) NOT NULL,
    currency VARCHAR(10) NOT NULL,
    status VARCHAR(50) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP NULL,
    boltcard_id VARCHAR(255) NULL,
    boltcard_ntag VARCHAR(255) NULL,
    recipient_address TEXT NULL,
    transaction_id VARCHAR(255) NULL,
    error_message TEXT NULL,
    metadata JSONB NULL
);

-- Create indexes for performance
CREATE INDEX idx_flash_payouts_store_id ON flash_payouts(store_id);
CREATE INDEX idx_flash_payouts_status ON flash_payouts(status);
CREATE INDEX idx_flash_payouts_boltcard_id ON flash_payouts(boltcard_id);
CREATE INDEX idx_flash_payouts_created_at ON flash_payouts(created_at);

-- Create boltcard usage statistics table
CREATE TABLE flash_boltcard_usage (
    boltcard_id VARCHAR(255) PRIMARY KEY,
    alias VARCHAR(255) NULL,
    total_payouts INTEGER DEFAULT 0,
    total_amount DECIMAL(20, 8) DEFAULT 0,
    first_used TIMESTAMP NULL,
    last_used TIMESTAMP NULL,
    store_id VARCHAR(255) NOT NULL
);
```

### 1.2 Payout Tracking Service

```csharp
// Services/FlashPayoutTrackingService.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Services
{
    public class FlashPayoutTrackingService
    {
        private readonly ILogger<FlashPayoutTrackingService> _logger;
        private readonly FlashPayoutRepository _repository;
        private readonly IEventAggregator _eventAggregator;

        public FlashPayoutTrackingService(
            ILogger<FlashPayoutTrackingService> logger,
            FlashPayoutRepository repository,
            IEventAggregator eventAggregator)
        {
            _logger = logger;
            _repository = repository;
            _eventAggregator = eventAggregator;
        }

        public async Task TrackPayoutCreated(PayoutData payoutData, string pullPaymentId)
        {
            try
            {
                var flashPayout = new FlashPayout
                {
                    PayoutId = payoutData.Id,
                    PullPaymentId = pullPaymentId,
                    StoreId = payoutData.StoreId,
                    Amount = payoutData.Amount,
                    Currency = payoutData.Currency,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    RecipientAddress = payoutData.Destination
                };

                await _repository.CreatePayoutAsync(flashPayout);
                
                _logger.LogInformation($"Tracked new payout: {payoutData.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track payout creation");
            }
        }

        public async Task TrackPayoutWithBoltcard(string payoutId, string boltcardId, string ntagUid)
        {
            try
            {
                var payout = await _repository.GetPayoutAsync(payoutId);
                if (payout != null)
                {
                    payout.BoltcardId = boltcardId;
                    payout.BoltcardNtag = ntagUid;
                    
                    await _repository.UpdatePayoutAsync(payout);
                    await UpdateBoltcardUsage(boltcardId, payout.StoreId, payout.Amount);
                    
                    _logger.LogInformation($"Associated Boltcard {boltcardId} with payout {payoutId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track Boltcard association");
            }
        }

        private async Task UpdateBoltcardUsage(string boltcardId, string storeId, decimal amount)
        {
            var usage = await _repository.GetBoltcardUsageAsync(boltcardId) 
                ?? new BoltcardUsage
                {
                    BoltcardId = boltcardId,
                    StoreId = storeId,
                    FirstUsed = DateTime.UtcNow
                };

            usage.TotalPayouts++;
            usage.TotalAmount += amount;
            usage.LastUsed = DateTime.UtcNow;

            await _repository.UpsertBoltcardUsageAsync(usage);
        }
    }
}
```

### 1.3 Enhanced Payout Processing Hook

```csharp
// Modifications to FlashPaymentService.cs
public partial class FlashPaymentService
{
    private readonly FlashPayoutTrackingService _payoutTracking;

    // In SendPayment method, add tracking:
    public async Task<PayResponse> SendPayment(string bolt11, PaymentSendOptions options)
    {
        var response = await base.SendPayment(bolt11, options);
        
        // Track if this is a pull payment payout
        if (options.Metadata?.ContainsKey("pullPaymentId") == true)
        {
            await _payoutTracking.TrackPayoutCreated(new PayoutData
            {
                Id = response.PaymentId,
                StoreId = _storeId,
                Amount = response.TotalAmount,
                Currency = "BTC",
                Destination = bolt11
            }, options.Metadata["pullPaymentId"]);
        }

        return response;
    }
}
```

### 1.4 LNURL-withdraw Enhancement for Boltcard Tracking

```csharp
// Services/FlashLNURLService.cs
public class FlashLNURLService
{
    public async Task<LNURLWithdrawResponse> ProcessWithdraw(
        string payoutId, 
        HttpRequest request)
    {
        // Extract Boltcard information from request
        var boltcardId = ExtractBoltcardId(request);
        var ntagUid = ExtractNtagUid(request);
        
        if (!string.IsNullOrEmpty(boltcardId))
        {
            await _payoutTracking.TrackPayoutWithBoltcard(
                payoutId, 
                boltcardId, 
                ntagUid);
        }
        
        // Continue with normal LNURL-withdraw processing
        return await ProcessWithdrawRequest(payoutId);
    }

    private string ExtractBoltcardId(HttpRequest request)
    {
        // Check headers for Boltcard identifier
        if (request.Headers.TryGetValue("X-Boltcard-UID", out var uid))
            return uid;
            
        // Check query parameters
        if (request.Query.TryGetValue("card", out var cardId))
            return cardId;
            
        return null;
    }
}
```

## Phase 2: Frontend Implementation

### 2.1 Dashboard Controller

```csharp
// Controllers/FlashDashboardController.cs
[Authorize(Policy = Policies.CanModifyStoreSettings)]
[Route("plugins/flash/dashboard")]
public class FlashDashboardController : Controller
{
    private readonly FlashDashboardService _dashboardService;
    private readonly BTCPayNetworkProvider _networkProvider;

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string storeId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string status = null,
        int page = 1)
    {
        var store = HttpContext.GetStoreData();
        if (store == null)
            return NotFound();

        var viewModel = await _dashboardService.GetDashboardData(
            store.Id,
            startDate ?? DateTime.UtcNow.AddDays(-30),
            endDate ?? DateTime.UtcNow,
            status,
            page);

        return View(viewModel);
    }

    [HttpGet("api/payouts")]
    public async Task<IActionResult> GetPayouts(
        string storeId,
        [FromQuery] PayoutFilterModel filter)
    {
        var payouts = await _dashboardService.GetPayouts(storeId, filter);
        return Json(payouts);
    }

    [HttpGet("api/stats")]
    public async Task<IActionResult> GetStats(string storeId)
    {
        var stats = await _dashboardService.GetDashboardStats(storeId);
        return Json(stats);
    }
}
```

### 2.2 Dashboard View

```html
<!-- Views/Flash/Dashboard.cshtml -->
@model FlashDashboardViewModel
@{
    ViewData["Title"] = "Flash Payouts Dashboard";
    Layout = "../Shared/_NavLayout.cshtml";
}

@section PageHeadContent {
    <link rel="stylesheet" href="~/flash/css/dashboard.css" asp-append-version="true" />
}

@section PageFootContent {
    <script src="~/vendor/chartjs/chart.min.js" asp-append-version="true"></script>
    <script src="~/flash/js/dashboard.js" asp-append-version="true"></script>
}

<div class="flash-dashboard">
    <div class="d-flex align-items-center justify-content-between mb-4">
        <h2>Pull Payment Payouts</h2>
        <div class="btn-group">
            <button id="refreshDashboard" class="btn btn-secondary">
                <i class="fa fa-refresh"></i> Refresh
            </button>
            <button id="exportData" class="btn btn-secondary">
                <i class="fa fa-download"></i> Export
            </button>
        </div>
    </div>

    <!-- Summary Cards -->
    <div class="row mb-4">
        <div class="col-md-3">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="card-title">Total Payouts</h5>
                    <h3 class="mb-0">@Model.TotalPayouts</h3>
                    <small class="text-muted">@Model.TotalAmount BTC</small>
                </div>
            </div>
        </div>
        <div class="col-md-3">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="card-title">Pending</h5>
                    <h3 class="mb-0 text-warning">@Model.PendingCount</h3>
                    <small class="text-muted">@Model.PendingAmount BTC</small>
                </div>
            </div>
        </div>
        <div class="col-md-3">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="card-title">Completed</h5>
                    <h3 class="mb-0 text-success">@Model.CompletedCount</h3>
                    <small class="text-muted">@Model.CompletedAmount BTC</small>
                </div>
            </div>
        </div>
        <div class="col-md-3">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="card-title">Active Boltcards</h5>
                    <h3 class="mb-0">@Model.ActiveBoltcards</h3>
                    <small class="text-muted">Last 30 days</small>
                </div>
            </div>
        </div>
    </div>

    <!-- Charts Row -->
    <div class="row mb-4">
        <div class="col-md-8">
            <div class="card">
                <div class="card-header">
                    <h5 class="mb-0">Payout Trends</h5>
                </div>
                <div class="card-body">
                    <canvas id="payoutTrendsChart" height="100"></canvas>
                </div>
            </div>
        </div>
        <div class="col-md-4">
            <div class="card">
                <div class="card-header">
                    <h5 class="mb-0">Top Boltcards</h5>
                </div>
                <div class="card-body">
                    <canvas id="topBoltcardsChart" height="200"></canvas>
                </div>
            </div>
        </div>
    </div>

    <!-- Filters -->
    <div class="card mb-4">
        <div class="card-body">
            <form id="filterForm" class="row g-3">
                <div class="col-md-3">
                    <label class="form-label">Date Range</label>
                    <input type="date" name="startDate" class="form-control" 
                           value="@Model.StartDate.ToString("yyyy-MM-dd")">
                </div>
                <div class="col-md-3">
                    <label class="form-label">&nbsp;</label>
                    <input type="date" name="endDate" class="form-control" 
                           value="@Model.EndDate.ToString("yyyy-MM-dd")">
                </div>
                <div class="col-md-3">
                    <label class="form-label">Status</label>
                    <select name="status" class="form-control">
                        <option value="">All</option>
                        <option value="Pending">Pending</option>
                        <option value="Processing">Processing</option>
                        <option value="Completed">Completed</option>
                        <option value="Failed">Failed</option>
                    </select>
                </div>
                <div class="col-md-3">
                    <label class="form-label">Boltcard</label>
                    <input type="text" name="boltcardId" class="form-control" 
                           placeholder="Card ID or alias">
                </div>
            </form>
        </div>
    </div>

    <!-- Payouts Table -->
    <div class="card">
        <div class="card-header">
            <h5 class="mb-0">Recent Payouts</h5>
        </div>
        <div class="card-body">
            <div id="payoutsTableContainer">
                @await Html.PartialAsync("_PayoutTable", Model.Payouts)
            </div>
        </div>
    </div>
</div>

<!-- Payout Details Modal -->
<div class="modal fade" id="payoutDetailsModal" tabindex="-1">
    <div class="modal-dialog modal-lg">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title">Payout Details</h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body" id="payoutDetailsContent">
                <!-- Loaded dynamically -->
            </div>
        </div>
    </div>
</div>
```

### 2.3 Dashboard JavaScript

```javascript
// wwwroot/flash/js/dashboard.js
document.addEventListener('DOMContentLoaded', function() {
    const dashboard = new FlashDashboard();
    dashboard.init();
});

class FlashDashboard {
    constructor() {
        this.storeId = document.querySelector('[data-store-id]').dataset.storeId;
        this.refreshInterval = null;
        this.charts = {};
    }

    init() {
        this.setupEventListeners();
        this.initializeCharts();
        this.startAutoRefresh();
    }

    setupEventListeners() {
        // Refresh button
        document.getElementById('refreshDashboard').addEventListener('click', () => {
            this.refreshData();
        });

        // Export button
        document.getElementById('exportData').addEventListener('click', () => {
            this.exportData();
        });

        // Filter form
        document.getElementById('filterForm').addEventListener('change', () => {
            this.applyFilters();
        });

        // Payout details
        document.addEventListener('click', (e) => {
            if (e.target.matches('[data-payout-id]')) {
                this.showPayoutDetails(e.target.dataset.payoutId);
            }
        });
    }

    initializeCharts() {
        // Trends chart
        const trendsCtx = document.getElementById('payoutTrendsChart').getContext('2d');
        this.charts.trends = new Chart(trendsCtx, {
            type: 'line',
            data: {
                labels: [], // Populated from data
                datasets: [{
                    label: 'Payouts',
                    data: [],
                    borderColor: 'rgb(75, 192, 192)',
                    tension: 0.1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });

        // Boltcards chart
        const boltcardsCtx = document.getElementById('topBoltcardsChart').getContext('2d');
        this.charts.boltcards = new Chart(boltcardsCtx, {
            type: 'doughnut',
            data: {
                labels: [],
                datasets: [{
                    data: [],
                    backgroundColor: [
                        'rgb(255, 99, 132)',
                        'rgb(54, 162, 235)',
                        'rgb(255, 205, 86)',
                        'rgb(75, 192, 192)',
                        'rgb(153, 102, 255)'
                    ]
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false
            }
        });

        this.updateCharts();
    }

    async refreshData() {
        try {
            // Update stats
            const stats = await this.fetchStats();
            this.updateSummaryCards(stats);

            // Update table
            const filters = this.getFilters();
            const payouts = await this.fetchPayouts(filters);
            this.updatePayoutsTable(payouts);

            // Update charts
            this.updateCharts();
        } catch (error) {
            console.error('Failed to refresh data:', error);
            this.showError('Failed to refresh dashboard data');
        }
    }

    async fetchStats() {
        const response = await fetch(`/plugins/flash/dashboard/api/stats?storeId=${this.storeId}`);
        if (!response.ok) throw new Error('Failed to fetch stats');
        return response.json();
    }

    async fetchPayouts(filters) {
        const params = new URLSearchParams(filters);
        params.append('storeId', this.storeId);
        
        const response = await fetch(`/plugins/flash/dashboard/api/payouts?${params}`);
        if (!response.ok) throw new Error('Failed to fetch payouts');
        return response.json();
    }

    updateSummaryCards(stats) {
        // Update card values with animation
        this.animateValue('totalPayouts', stats.totalPayouts);
        this.animateValue('totalAmount', stats.totalAmount);
        this.animateValue('pendingCount', stats.pendingCount);
        this.animateValue('completedCount', stats.completedCount);
    }

    animateValue(elementId, value) {
        const element = document.getElementById(elementId);
        if (!element) return;

        const current = parseInt(element.textContent) || 0;
        const increment = (value - current) / 20;
        let step = 0;

        const timer = setInterval(() => {
            step++;
            element.textContent = Math.round(current + increment * step);
            if (step >= 20) {
                element.textContent = value;
                clearInterval(timer);
            }
        }, 50);
    }

    async showPayoutDetails(payoutId) {
        try {
            const response = await fetch(`/plugins/flash/dashboard/api/payouts/${payoutId}`);
            const payout = await response.json();
            
            const content = this.renderPayoutDetails(payout);
            document.getElementById('payoutDetailsContent').innerHTML = content;
            
            const modal = new bootstrap.Modal(document.getElementById('payoutDetailsModal'));
            modal.show();
        } catch (error) {
            console.error('Failed to load payout details:', error);
        }
    }

    renderPayoutDetails(payout) {
        return `
            <div class="row">
                <div class="col-md-6">
                    <h6>Payout Information</h6>
                    <dl class="row">
                        <dt class="col-sm-4">ID:</dt>
                        <dd class="col-sm-8">${payout.payoutId}</dd>
                        
                        <dt class="col-sm-4">Amount:</dt>
                        <dd class="col-sm-8">${payout.amount} ${payout.currency}</dd>
                        
                        <dt class="col-sm-4">Status:</dt>
                        <dd class="col-sm-8">
                            <span class="badge bg-${this.getStatusColor(payout.status)}">
                                ${payout.status}
                            </span>
                        </dd>
                        
                        <dt class="col-sm-4">Created:</dt>
                        <dd class="col-sm-8">${new Date(payout.createdAt).toLocaleString()}</dd>
                    </dl>
                </div>
                <div class="col-md-6">
                    <h6>Boltcard Information</h6>
                    ${payout.boltcardId ? `
                        <dl class="row">
                            <dt class="col-sm-4">Card ID:</dt>
                            <dd class="col-sm-8">${payout.boltcardId}</dd>
                            
                            <dt class="col-sm-4">NTAG UID:</dt>
                            <dd class="col-sm-8">${payout.boltcardNtag || 'N/A'}</dd>
                            
                            <dt class="col-sm-4">Alias:</dt>
                            <dd class="col-sm-8">${payout.boltcardAlias || 'No alias set'}</dd>
                        </dl>
                    ` : '<p class="text-muted">No Boltcard information available</p>'}
                </div>
            </div>
            ${payout.transactionId ? `
                <hr>
                <h6>Transaction Details</h6>
                <dl class="row">
                    <dt class="col-sm-3">Transaction ID:</dt>
                    <dd class="col-sm-9">
                        <code>${payout.transactionId}</code>
                        <a href="/tx/${payout.transactionId}" target="_blank" class="ms-2">
                            <i class="fa fa-external-link"></i>
                        </a>
                    </dd>
                </dl>
            ` : ''}
        `;
    }

    getStatusColor(status) {
        const colors = {
            'Pending': 'warning',
            'Processing': 'info',
            'Completed': 'success',
            'Failed': 'danger'
        };
        return colors[status] || 'secondary';
    }

    startAutoRefresh() {
        // Refresh every 30 seconds
        this.refreshInterval = setInterval(() => {
            this.refreshData();
        }, 30000);
    }

    stopAutoRefresh() {
        if (this.refreshInterval) {
            clearInterval(this.refreshInterval);
            this.refreshInterval = null;
        }
    }
}
```

## Phase 3: Integration Points

### 3.1 Menu Integration

```csharp
// In FlashPlugin.cs
public override void Execute(IServiceCollection services)
{
    services.AddSingleton<IUIExtension>(new UIExtension(
        "Flash/StoreNavFlashDashboard", 
        "store-nav"));
}
```

### 3.2 Navigation Partial

```html
<!-- Views/Flash/StoreNavFlashDashboard.cshtml -->
<li class="nav-item">
    <a asp-controller="FlashDashboard" 
       asp-action="Index" 
       asp-route-storeId="@Context.GetStoreData().Id"
       class="nav-link @(ViewContext.RouteData.Values["Controller"]?.ToString() == "FlashDashboard" ? "active" : "")"
       id="StoreNav-FlashDashboard">
        <span class="fa fa-bolt"></span>
        <span>Flash Payouts</span>
    </a>
</li>
```

## Phase 4: Testing Strategy

### 4.1 Unit Tests

```csharp
[TestClass]
public class FlashPayoutTrackingServiceTests
{
    [TestMethod]
    public async Task TrackPayoutCreated_ShouldCreatePayoutRecord()
    {
        // Arrange
        var service = CreateService();
        var payoutData = new PayoutData
        {
            Id = "test-payout-1",
            StoreId = "store-1",
            Amount = 0.001m,
            Currency = "BTC"
        };

        // Act
        await service.TrackPayoutCreated(payoutData, "pull-payment-1");

        // Assert
        var payout = await _repository.GetPayoutAsync("test-payout-1");
        Assert.IsNotNull(payout);
        Assert.AreEqual("Pending", payout.Status);
    }

    [TestMethod]
    public async Task TrackPayoutWithBoltcard_ShouldUpdateBoltcardUsage()
    {
        // Arrange
        var service = CreateService();
        await CreateTestPayout("test-payout-1");

        // Act
        await service.TrackPayoutWithBoltcard(
            "test-payout-1", 
            "boltcard-123", 
            "04:AB:CD:EF");

        // Assert
        var usage = await _repository.GetBoltcardUsageAsync("boltcard-123");
        Assert.IsNotNull(usage);
        Assert.AreEqual(1, usage.TotalPayouts);
    }
}
```

### 4.2 Integration Tests

```csharp
[TestClass]
public class FlashDashboardIntegrationTests : IntegrationTestBase
{
    [TestMethod]
    public async Task Dashboard_ShouldLoadWithCorrectData()
    {
        // Arrange
        await CreateTestPayouts(10);
        
        // Act
        var response = await Client.GetAsync("/plugins/flash/dashboard");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains("Total Payouts"));
        Assert.IsTrue(content.Contains("10"));
    }
}
```

## Deployment Considerations

### Database Migrations

```bash
# Create migration
dotnet ef migrations add AddFlashPayoutTracking -c ApplicationDbContext

# Apply migration
dotnet ef database update -c ApplicationDbContext
```

### Performance Optimization

1. **Caching**
   - Cache dashboard stats for 60 seconds
   - Cache Boltcard usage data for 5 minutes
   - Use memory cache for frequently accessed data

2. **Database Indexes**
   - Index on store_id + created_at for time-based queries
   - Index on boltcard_id for card-specific lookups
   - Composite index for status + store_id

3. **Pagination**
   - Limit table display to 50 records per page
   - Use server-side pagination for large datasets
   - Implement infinite scroll as enhancement

## Security Checklist

- [ ] Verify store ownership before displaying data
- [ ] Sanitize all user inputs
- [ ] Rate limit API endpoints
- [ ] Log all data access for audit trail
- [ ] Implement CSRF protection on forms
- [ ] Validate Boltcard identifiers
- [ ] Secure sensitive data in transit and at rest