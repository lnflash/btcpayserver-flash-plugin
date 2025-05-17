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