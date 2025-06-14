#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.Flash.Data;
using BTCPayServer.Plugins.Flash.Models;
using BTCPayServer.Plugins.Flash.Services;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Flash.Controllers
{
    [Authorize(Policy = Policies.CanViewStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [Route("plugins/flash/{storeId}/payouts")]
    public class FlashPayoutController : Controller
    {
        private readonly FlashPayoutRepository _payoutRepository;
        private readonly IFlashBoltcardService _boltcardService;
        private readonly StoreRepository _storeRepository;
        private readonly BTCPayNetworkProvider _networkProvider;
        private readonly ILogger<FlashPayoutController> _logger;

        public FlashPayoutController(
            FlashPayoutRepository payoutRepository,
            IFlashBoltcardService boltcardService,
            StoreRepository storeRepository,
            BTCPayNetworkProvider networkProvider,
            ILogger<FlashPayoutController> logger)
        {
            _payoutRepository = payoutRepository;
            _boltcardService = boltcardService;
            _storeRepository = storeRepository;
            _networkProvider = networkProvider;
            _logger = logger;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard(string storeId)
        {
            var store = await _storeRepository.FindStore(storeId);
            if (store == null)
            {
                return NotFound();
            }

            var stats = await _payoutRepository.GetDashboardStatsAsync(storeId);
            var recentPayouts = await _payoutRepository.GetPayoutsForStoreAsync(storeId, take: 10);
            var boltcardStats = await _payoutRepository.GetBoltcardStatsAsync(storeId);
            var timeline = await _payoutRepository.GetPayoutTimelineAsync(storeId, days: 30);

            var viewModel = new FlashPayoutDashboardViewModel
            {
                StoreId = storeId,
                StoreName = store.StoreName,
                Stats = stats,
                RecentPayouts = recentPayouts.Select(p => MapToViewModel(p)).ToList(),
                BoltcardStats = boltcardStats,
                Timeline = timeline,
                Network = _networkProvider.GetNetwork<BTCPayNetwork>("BTC")
            };

            return View("~/Plugins/Flash/Views/PayoutDashboard.cshtml", viewModel);
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetPayouts(
            string storeId,
            [FromQuery] PayoutFilter filter)
        {
            var payouts = await _payoutRepository.GetPayoutsForStoreAsync(
                storeId, 
                filter.Status, 
                filter.Skip, 
                filter.Take);

            var viewModels = payouts.Select(p => MapToViewModel(p)).ToList();

            return Json(new
            {
                payouts = viewModels,
                total = viewModels.Count,
                hasMore = viewModels.Count >= filter.Take
            });
        }

        [HttpGet("{payoutId}")]
        public async Task<IActionResult> GetPayout(string storeId, string payoutId)
        {
            var payout = await _payoutRepository.GetPayoutAsync(payoutId);
            
            if (payout == null || payout.StoreId != storeId)
            {
                return NotFound();
            }

            return Json(MapToViewModel(payout));
        }

        [HttpPost("{payoutId}/boltcard")]
        public async Task<IActionResult> SetBoltcardId(
            string storeId, 
            string payoutId,
            [FromBody] SetBoltcardRequest request)
        {
            if (string.IsNullOrEmpty(request.BoltcardId))
            {
                return BadRequest("BoltcardId is required");
            }

            var payout = await _payoutRepository.GetPayoutAsync(payoutId);
            
            if (payout == null || payout.StoreId != storeId)
            {
                return NotFound();
            }

            var success = await _payoutRepository.SetBoltcardIdAsync(payoutId, request.BoltcardId);
            
            if (success)
            {
                _logger.LogInformation("Boltcard {BoltcardId} associated with payout {PayoutId}", 
                    request.BoltcardId, payoutId);
                
                return Ok(new { success = true, message = "Boltcard ID set successfully" });
            }

            return BadRequest("Failed to set Boltcard ID");
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportPayouts(
            string storeId,
            [FromQuery] PayoutFilter filter,
            [FromQuery] string format = "csv")
        {
            var payouts = await _payoutRepository.GetPayoutsForStoreAsync(
                storeId, 
                filter.Status, 
                0, 
                10000); // Max export limit

            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                return ExportAsCsv(payouts);
            }
            else if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                return ExportAsJson(payouts);
            }

            return BadRequest("Unsupported export format");
        }

        [HttpGet("boltcard-stats")]
        public async Task<IActionResult> GetBoltcardStats(string storeId)
        {
            var stats = await _payoutRepository.GetBoltcardStatsAsync(storeId, topCount: 20);
            return Json(stats);
        }

        [HttpGet("timeline")]
        public async Task<IActionResult> GetPayoutTimeline(string storeId, int days = 30)
        {
            var timeline = await _payoutRepository.GetPayoutTimelineAsync(storeId, days);
            return Json(timeline);
        }

        private PayoutViewModel MapToViewModel(FlashPayout payout)
        {
            return new PayoutViewModel
            {
                Id = payout.Id,
                PullPaymentId = payout.PullPaymentId,
                AmountSats = payout.AmountSats,
                AmountBtc = payout.AmountSats / 100_000_000m,
                Status = payout.Status.ToString(),
                BoltcardId = payout.BoltcardId,
                PaymentHash = payout.PaymentHash,
                Memo = payout.Memo,
                CreatedAt = payout.CreatedAt,
                CompletedAt = payout.CompletedAt,
                ErrorMessage = payout.ErrorMessage
            };
        }

        private IActionResult ExportAsCsv(List<FlashPayout> payouts)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Id,PullPaymentId,Amount (sats),Amount (BTC),Status,Boltcard ID,Payment Hash,Created,Completed,Error");

            foreach (var payout in payouts)
            {
                csv.AppendLine($"{payout.Id},{payout.PullPaymentId},{payout.AmountSats}," +
                    $"{payout.AmountSats / 100_000_000m},{payout.Status}," +
                    $"{payout.BoltcardId ?? ""},{payout.PaymentHash ?? ""}," +
                    $"{payout.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                    $"{payout.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""}," +
                    $"{payout.ErrorMessage ?? ""}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"flash-payouts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
        }

        private IActionResult ExportAsJson(List<FlashPayout> payouts)
        {
            var json = JsonConvert.SerializeObject(payouts.Select(p => new
            {
                p.Id,
                p.PullPaymentId,
                p.AmountSats,
                AmountBtc = p.AmountSats / 100_000_000m,
                p.Status,
                p.BoltcardId,
                p.PaymentHash,
                p.Memo,
                p.CreatedAt,
                p.CompletedAt,
                p.ErrorMessage
            }), Formatting.Indented);

            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", $"flash-payouts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        }

        public class SetBoltcardRequest
        {
            public string BoltcardId { get; set; } = string.Empty;
        }

        public class PayoutViewModel
        {
            public string Id { get; set; } = string.Empty;
            public string PullPaymentId { get; set; } = string.Empty;
            public long AmountSats { get; set; }
            public decimal AmountBtc { get; set; }
            public string Status { get; set; } = string.Empty;
            public string? BoltcardId { get; set; }
            public string? PaymentHash { get; set; }
            public string? Memo { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
            public DateTimeOffset? CompletedAt { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}