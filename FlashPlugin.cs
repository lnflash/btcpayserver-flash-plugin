using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Flash.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.Flash;

public class FlashPlugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } = 
        Array.Empty<IBTCPayServerPlugin.PluginDependency>();

    public override void Execute(IServiceCollection services)
    {
        // Register Lightning connection string handler
        services.AddSingleton<ILightningConnectionStringHandler, FlashLightningConnectionStringHandler>();
        
        // Register pages for site navigation
        services.AddBTCPayUIPlugin(new UIPluginDescriptor
        {
            Currency = "BTC",
            LightningNode = typeof(FlashLightningConnectionStringHandler).FullName,
            LightningNodeSetup = typeof(UIFlashSetup)
        });
        
        // Add Flash specific services
        services.AddSingleton<FlashClientProvider>();
    }
}

public class UIFlashSetup : IUIExtension
{
    public string Location => "lightning-node-setup";
    
    public string View => "~/Plugins/BTCPayServer.Plugins.Flash/UI/_FlashSetup.cshtml";
    
    public int Order => 100;
}