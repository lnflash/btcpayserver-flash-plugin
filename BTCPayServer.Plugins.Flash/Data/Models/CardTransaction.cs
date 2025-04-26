#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.Flash.Data.Models
{
    public enum CardTransactionType
    {
        Payment,
        TopUp,
        Refund
    }
    
    public enum CardTransactionStatus
    {
        Pending,
        Completed,
        Failed,
        Cancelled
    }
    
    public class CardTransaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; } = null!;
        
        [Required]
        public string CardRegistrationId { get; set; } = null!;
        
        [ForeignKey("CardRegistrationId")]
        public CardRegistration? CardRegistration { get; set; }
        
        [Required]
        public string? PayoutId { get; set; }
        
        [Required]
        public decimal Amount { get; set; }
        
        [Required]
        public string Currency { get; set; } = "SATS";
        
        [Required]
        public CardTransactionType Type { get; set; }
        
        [Required]
        public CardTransactionStatus Status { get; set; }
        
        public string? InvoiceId { get; set; }
        
        public string? PaymentHash { get; set; }
        
        [Required]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        
        public DateTimeOffset? CompletedAt { get; set; }
        
        public string? MerchantId { get; set; }
        
        public string? LocationId { get; set; }
        
        public string? Description { get; set; }
    }
}