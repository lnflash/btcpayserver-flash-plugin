#nullable enable
using System;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.Flash.Models
{
    public class FlashPayout
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [Required]
        public string StoreId { get; set; } = string.Empty;
        
        [Required]
        public string PullPaymentId { get; set; } = string.Empty;
        
        public long AmountSats { get; set; }
        
        public PayoutStatus Status { get; set; } = PayoutStatus.Pending;
        
        public string? BoltcardId { get; set; }
        
        public string? PaymentHash { get; set; }
        
        public string? LightningInvoice { get; set; }
        
        public string? ErrorMessage { get; set; }
        
        public string? Metadata { get; set; } // JSON for additional data
        
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        
        public DateTimeOffset? CompletedAt { get; set; }
        
        // Navigation properties
        public string? Memo { get; set; }
        
        public string? DestinationAddress { get; set; } // Lightning address if provided
    }

    public enum PayoutStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Cancelled
    }

    public class PayoutDashboardStats
    {
        public int TotalPayouts { get; set; }
        public int ActivePayouts { get; set; }
        public int CompletedPayouts { get; set; }
        public int FailedPayouts { get; set; }
        public long TotalAmountSats { get; set; }
        public int UniqueBoltcards { get; set; }
        
        public decimal TotalAmountBtc => TotalAmountSats / 100_000_000m;
    }

    public class BoltcardStats
    {
        public string BoltcardId { get; set; } = string.Empty;
        public int TotalPayouts { get; set; }
        public long TotalAmountSats { get; set; }
        public DateTimeOffset FirstUsed { get; set; }
        public DateTimeOffset LastUsed { get; set; }
        
        public decimal TotalAmountBtc => TotalAmountSats / 100_000_000m;
        public int DaysActive => (int)(LastUsed - FirstUsed).TotalDays + 1;
    }

    public class PayoutTimelineEntry
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public long TotalAmountSats { get; set; }
        
        public decimal TotalAmountBtc => TotalAmountSats / 100_000_000m;
    }

    public class PayoutFilter
    {
        public PayoutStatus? Status { get; set; }
        public string? BoltcardId { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public long? MinAmountSats { get; set; }
        public long? MaxAmountSats { get; set; }
        public string? SearchTerm { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
    }
}