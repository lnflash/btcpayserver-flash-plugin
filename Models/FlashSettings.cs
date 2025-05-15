using BTCPayServer.Abstractions.Contracts;
using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.Flash.Models;

public class FlashSettings : IHasBlobId
{
    // Store specific settings
    [Display(Name = "Flash Bearer Token")]
    [Required(ErrorMessage = "A bearer token is required.")]
    public string? BearerToken { get; set; }
    
    [Display(Name = "API URL")]
    public string? ApiUrl { get; set; } = "https://api.flashapp.me/graphql";

    // Implements the IHasBlobId interface
    // This interface is required for settings persistence
    [Required]
    public string Id { get; set; } = "FlashSettings";

    public string StoreId { get; set; } = string.Empty;
}