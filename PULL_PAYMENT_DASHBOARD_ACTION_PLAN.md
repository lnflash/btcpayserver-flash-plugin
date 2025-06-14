# Pull Payment Dashboard - Quick Action Plan

## Summary
Add a new "Pull Payment Payouts" dashboard to the existing Flash plugin page at `/plugins/flash` that tracks and displays all pull payment payouts with Boltcard identification.

## Implementation Steps

### Step 1: Add Dashboard Link to Existing Flash Index Page (30 minutes)

Update `/Views/UIFlash/Index.cshtml` to add a new card for the Pull Payment Dashboard:

```html
<!-- Add after Lightning Address card -->
<div class="col-lg-4 mb-4 mb-lg-0">
    <div class="card h-100">
        <div class="card-header bg-primary text-white">
            <h3 class="h5 mb-0"><i class="fas fa-money-check-alt me-2"></i>Pull Payment Payouts</h3>
        </div>
        <div class="card-body d-flex flex-column">
            <p>
                Track and monitor all pull payment payouts. View payout history, Boltcard usage, 
                and detailed analytics for your store's payouts.
            </p>
            <div class="mt-auto text-center">
                <a href="/plugins/flash/payouts" class="btn btn-primary">
                    <i class="fas fa-chart-line me-2"></i>View Dashboard
                </a>
            </div>
        </div>
    </div>
</div>
```

### Step 2: Create the Dashboard Controller (2 hours)

Create `/Controllers/UIFlashPayoutsController.cs`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    [Route("plugins/flash/payouts")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanViewStoreSettings)]
    public class UIFlashPayoutsController : Controller
    {
        private readonly ILogger<UIFlashPayoutsController> _logger;
        private readonly StoreRepository _storeRepository;
        private readonly ApplicationDbContextFactory _dbContextFactory;
        private readonly FlashPayoutTrackingService _payoutTracking;

        public UIFlashPayoutsController(
            ILogger<UIFlashPayoutsController> logger,
            StoreRepository storeRepository,
            ApplicationDbContextFactory dbContextFactory,
            FlashPayoutTrackingService payoutTracking)
        {
            _logger = logger;
            _storeRepository = storeRepository;
            _dbContextFactory = dbContextFactory;
            _payoutTracking = payoutTracking;
        }

        [HttpGet("")]
        public async Task<IActionResult> Dashboard(string storeId)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
                return NotFound();

            var viewModel = await BuildDashboardViewModel(storeId);
            return View(viewModel);
        }

        [HttpGet("api/stats")]
        public async Task<IActionResult> GetStats(string storeId)
        {
            var stats = await _payoutTracking.GetDashboardStats(storeId);
            return Json(stats);
        }

        private async Task<FlashPayoutDashboardViewModel> BuildDashboardViewModel(string storeId)
        {
            using var ctx = _dbContextFactory.CreateContext();
            
            // Get recent payouts
            var recentPayouts = await ctx.PayoutData
                .Where(p => p.StoreDataId == storeId)
                .OrderByDescending(p => p.Date)
                .Take(50)
                .ToListAsync();

            // Get stats
            var stats = await _payoutTracking.GetDashboardStats(storeId);

            return new FlashPayoutDashboardViewModel
            {
                StoreId = storeId,
                RecentPayouts = recentPayouts.Select(p => new PayoutViewModel
                {
                    Id = p.Id,
                    Amount = p.Amount,
                    Currency = p.Currency,
                    Status = p.State.ToString(),
                    CreatedDate = p.Date,
                    Destination = p.Destination,
                    // Boltcard info will be added from our tracking table
                }).ToList(),
                TotalPayouts = stats.TotalPayouts,
                PendingCount = stats.PendingCount,
                CompletedCount = stats.CompletedCount,
                TotalAmount = stats.TotalAmount
            };
        }
    }
}
```

### Step 3: Create Simple Tracking Service (2 hours)

Create `/Services/FlashPayoutTrackingService.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Services
{
    public class FlashPayoutTrackingService
    {
        private readonly ILogger<FlashPayoutTrackingService> _logger;
        private readonly ApplicationDbContextFactory _dbContextFactory;
        
        // In-memory storage for MVP - replace with database later
        private static readonly ConcurrentDictionary<string, BoltcardInfo> _boltcardTracking = new();

        public FlashPayoutTrackingService(
            ILogger<FlashPayoutTrackingService> logger,
            ApplicationDbContextFactory dbContextFactory)
        {
            _logger = logger;
            _dbContextFactory = dbContextFactory;
        }

        public async Task TrackBoltcardUsage(string payoutId, string boltcardId, string ntagUid = null)
        {
            _boltcardTracking[payoutId] = new BoltcardInfo
            {
                BoltcardId = boltcardId,
                NtagUid = ntagUid,
                Timestamp = DateTime.UtcNow
            };
            
            _logger.LogInformation($"Tracked Boltcard {boltcardId} for payout {payoutId}");
        }

        public BoltcardInfo GetBoltcardInfo(string payoutId)
        {
            _boltcardTracking.TryGetValue(payoutId, out var info);
            return info;
        }

        public async Task<DashboardStats> GetDashboardStats(string storeId)
        {
            using var ctx = _dbContextFactory.CreateContext();
            
            var payouts = await ctx.PayoutData
                .Where(p => p.StoreDataId == storeId)
                .ToListAsync();

            return new DashboardStats
            {
                TotalPayouts = payouts.Count,
                PendingCount = payouts.Count(p => p.State == PayoutState.AwaitingPayment),
                CompletedCount = payouts.Count(p => p.State == PayoutState.Completed),
                TotalAmount = payouts.Sum(p => p.Amount),
                ActiveBoltcards = _boltcardTracking.Values
                    .Select(b => b.BoltcardId)
                    .Distinct()
                    .Count()
            };
        }
    }

    public class BoltcardInfo
    {
        public string BoltcardId { get; set; }
        public string NtagUid { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DashboardStats
    {
        public int TotalPayouts { get; set; }
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public decimal TotalAmount { get; set; }
        public int ActiveBoltcards { get; set; }
    }
}
```

### Step 4: Create Dashboard View (2 hours)

Create `/Views/UIFlashPayouts/Dashboard.cshtml`:

```html
@model FlashPayoutDashboardViewModel
@{
    ViewData["Title"] = "Pull Payment Payouts";
    Layout = "_Layout";
}

<div class="container">
    <div class="d-flex align-items-center justify-content-between mb-4">
        <div>
            <h1>Pull Payment Payouts</h1>
            <p class="text-muted">Track and monitor all pull payment payouts for your store</p>
        </div>
        <a href="/plugins/flash" class="btn btn-secondary">
            <i class="fas fa-arrow-left me-2"></i>Back to Flash
        </a>
    </div>

    <!-- Summary Cards -->
    <div class="row mb-4">
        <div class="col-md-3">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="card-title">Total Payouts</h5>
                    <h3 class="mb-0">@Model.TotalPayouts</h3>
                </div>
            </div>
        </div>
        <div class="col-md-3">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="card-title">Pending</h5>
                    <h3 class="mb-0 text-warning">@Model.PendingCount</h3>
                </div>
            </div>
        </div>
        <div class="col-md-3">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="card-title">Completed</h5>
                    <h3 class="mb-0 text-success">@Model.CompletedCount</h3>
                </div>
            </div>
        </div>
        <div class="col-md-3">
            <div class="card text-center">
                <div class="card-body">
                    <h5 class="card-title">Total Amount</h5>
                    <h3 class="mb-0">@Model.TotalAmount.ToString("F8") BTC</h3>
                </div>
            </div>
        </div>
    </div>

    <!-- Payouts Table -->
    <div class="card">
        <div class="card-header">
            <h3 class="h5 mb-0">Recent Payouts</h3>
        </div>
        <div class="card-body">
            @if (Model.RecentPayouts.Any())
            {
                <div class="table-responsive">
                    <table class="table table-hover">
                        <thead>
                            <tr>
                                <th>Date</th>
                                <th>Payout ID</th>
                                <th>Amount</th>
                                <th>Status</th>
                                <th>Boltcard</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var payout in Model.RecentPayouts)
                            {
                                <tr>
                                    <td>@payout.CreatedDate.ToString("g")</td>
                                    <td><code>@payout.Id.Substring(0, 8)...</code></td>
                                    <td>@payout.Amount @payout.Currency</td>
                                    <td>
                                        <span class="badge bg-@GetStatusColor(payout.Status)">
                                            @payout.Status
                                        </span>
                                    </td>
                                    <td>
                                        @if (!string.IsNullOrEmpty(payout.BoltcardId))
                                        {
                                            <code>@payout.BoltcardId</code>
                                        }
                                        else
                                        {
                                            <span class="text-muted">N/A</span>
                                        }
                                    </td>
                                    <td>
                                        <button class="btn btn-sm btn-outline-primary" 
                                                onclick="showPayoutDetails('@payout.Id')">
                                            Details
                                        </button>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
            else
            {
                <p class="text-center text-muted py-5">No payouts found</p>
            }
        </div>
    </div>
</div>

@functions {
    string GetStatusColor(string status)
    {
        return status switch
        {
            "AwaitingPayment" => "warning",
            "InProgress" => "info",
            "Completed" => "success",
            "Cancelled" => "danger",
            _ => "secondary"
        };
    }
}

@section Scripts {
    <script>
        function showPayoutDetails(payoutId) {
            // Simple alert for MVP - replace with modal later
            alert('Payout Details:\nID: ' + payoutId + '\n\nFull details view coming soon!');
        }

        // Auto-refresh every 30 seconds
        setInterval(() => {
            location.reload();
        }, 30000);
    </script>
}
```

### Step 5: Hook into Payout Processing (1 hour)

Modify the existing `FlashPaymentService.cs` to track Boltcard usage:

```csharp
// In the SendPayment method where pull payments are processed
if (context?.Metadata?.TryGetValue("boltcardId", out var boltcardId) == true)
{
    await _payoutTracking.TrackBoltcardUsage(paymentResult.Id, boltcardId.ToString());
}
```

### Step 6: Register Services (30 minutes)

In `FlashPlugin.cs`:

```csharp
public override void Execute(IServiceCollection services)
{
    // Existing services...
    
    // Add new tracking service
    services.AddSingleton<FlashPayoutTrackingService>();
    
    // Add new controller
    services.AddMvc().AddApplicationPart(typeof(UIFlashPayoutsController).Assembly);
}
```

## Deployment Steps

1. **Build and Test** (1 hour)
   - Build the plugin with new dashboard
   - Test in development environment
   - Verify payout tracking works

2. **Deploy to Test Environment** (30 minutes)
   - Deploy updated plugin
   - Test with real pull payments
   - Verify Boltcard tracking

3. **Production Deployment** (30 minutes)
   - Deploy to production
   - Monitor for issues
   - Gather user feedback

## Total Estimated Time: 1 Day

This MVP implementation provides:
- ✅ Basic dashboard showing all pull payment payouts
- ✅ Summary statistics
- ✅ Recent payouts table
- ✅ Basic Boltcard tracking (in-memory for MVP)
- ✅ Auto-refresh every 30 seconds
- ✅ Integration with existing Flash plugin page

## Future Enhancements

1. **Database Storage** (Phase 2)
   - Move from in-memory to persistent storage
   - Add proper migrations
   - Historical data retention

2. **Advanced Features** (Phase 3)
   - Charts and visualizations
   - Export functionality
   - Advanced filtering
   - Real-time updates via WebSocket
   - Detailed payout modal

3. **Boltcard Management** (Phase 4)
   - Boltcard aliasing
   - Usage analytics per card
   - Card blocking/allowlisting

## Next Steps

1. Review and approve the plan
2. Start with Step 1 (adding dashboard link)
3. Implement basic tracking service
4. Create simple dashboard view
5. Test with actual pull payments
6. Iterate based on feedback