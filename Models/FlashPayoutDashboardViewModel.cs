#nullable enable
using System;
using System.Collections.Generic;
using BTCPayServer.Plugins.Flash.Controllers;
using NBitcoin;

namespace BTCPayServer.Plugins.Flash.Models
{
    public class FlashPayoutDashboardViewModel
    {
        public string StoreId { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        
        // Dashboard statistics
        public PayoutDashboardStats Stats { get; set; } = new();
        
        // Recent payouts list
        public List<FlashPayoutController.PayoutViewModel> RecentPayouts { get; set; } = new();
        
        // Boltcard analytics
        public List<BoltcardStats> BoltcardStats { get; set; } = new();
        
        // Time-based analytics
        public List<PayoutTimelineEntry> Timeline { get; set; } = new();
        
        // Network info for formatting
        public BTCPayNetwork? Network { get; set; }
        
        // Computed properties
        public string FormattedTotalBtc => Stats.TotalAmountBtc.ToString("0.00000000") + " BTC";
        public decimal AveragePayoutBtc => Stats.CompletedPayouts > 0 
            ? Stats.TotalAmountBtc / Stats.CompletedPayouts 
            : 0;
    }
}