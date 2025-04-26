#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.Flash.Data.Models
{
    public class CardRegistration
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string Id { get; set; } = null!;
        
        [Required]
        public string CardUID { get; set; } = null!;
        
        [Required]
        public string PullPaymentId { get; set; } = null!;
        
        [Required]
        public string StoreId { get; set; } = null!;
        
        public string? UserId { get; set; }
        
        [Required]
        public string CardName { get; set; } = "Flash Card";
        
        [Required]
        public int Version { get; set; } = 1;
        
        [Required]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        
        public DateTimeOffset? LastUsedAt { get; set; }
        
        public bool IsBlocked { get; set; } = false;
        
        // Custom fields for Flash card integration
        public string? FlashWalletId { get; set; }
        
        public decimal? SpendingLimitPerTransaction { get; set; }
    }
}