using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.Flash.Models
{
    public class FlashPayoutDashboardViewModel
    {
        public string StoreId { get; set; }
        public List<PayoutViewModel> RecentPayouts { get; set; } = new List<PayoutViewModel>();
        public int TotalPayouts { get; set; }
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-30);
        public DateTime EndDate { get; set; } = DateTime.UtcNow;
    }

    public class PayoutViewModel
    {
        public string Id { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Destination { get; set; }
        public string BoltcardId { get; set; }
        public string BoltcardNtag { get; set; }
        public string TransactionId { get; set; }
    }
}