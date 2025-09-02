#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.Flash.Models
{
    public class FlashPluginSettings
    {
        [Display(Name = "Flash Bearer Token")]
        [Required(ErrorMessage = "A valid bearer token is required to connect to Flash")]
        public string? BearerToken { get; set; }

        [Display(Name = "API Endpoint")]
        [Required(ErrorMessage = "API endpoint URL is required")]
        public string? ApiEndpoint { get; set; } = "https://api.flashapp.me/graphql";
        
        [Display(Name = "Request Timeout")]
        [Range(5, 120, ErrorMessage = "Timeout must be between 5 and 120 seconds")]
        public int RequestTimeout { get; set; } = 30;
        
        [Display(Name = "Enable LNURL-Auth Support")]
        public bool AllowLnurlAuth { get; set; } = true;
        
        [Display(Name = "Enable Boltcard Topup")]
        public bool AllowBoltcardTopup { get; set; } = true;

        public bool IsConfigured => !string.IsNullOrEmpty(BearerToken);
    }

    public class TestConnectionResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public long? Balance { get; set; }
        public string? Currency { get; set; }
    }
}