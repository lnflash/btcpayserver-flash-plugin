#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.Flash.Models
{
    public class FlashSettings
    {
        [Display(Name = "Flash Bearer Token")]
        [Required(ErrorMessage = "A valid bearer token is required to connect to Flash")]
        public string? BearerToken { get; set; }

        [Display(Name = "API URL")]
        [Required(ErrorMessage = "API URL is required")]
        public string? ApiUrl { get; set; } = "https://api.flashapp.me/graphql";
    }
}