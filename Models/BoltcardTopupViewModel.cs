#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.Flash.Models
{
    public class BoltcardTopupViewModel
    {
        [Required(ErrorMessage = "Amount in satoshis is required")]
        [Range(500, 100000, ErrorMessage = "Amount must be between 500 and 100,000 satoshis")]
        [Display(Name = "Amount (satoshis)")]
        public long? Amount { get; set; } = 5000;

        [Display(Name = "Description")]
        public string? Description { get; set; } = "Flashcard topup";
    }

    public class BoltcardInvoiceViewModel
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string PaymentRequest { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Unpaid";
        public int ExpirySeconds { get; set; } = 3600;
    }

    public class BoltcardResultViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string InvoiceId { get; set; } = string.Empty;
        public long? Amount { get; set; }
    }
}