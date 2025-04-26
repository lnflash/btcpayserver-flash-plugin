#nullable enable
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Flash.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.Flash
{
    public class FlashPlugin : BaseBTCPayServerPlugin
    {
        public const string PLUGIN_NAME = "Flash";
        public const string ViewsDirectory = "/Views";
        
        public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
        {
            new() { Identifier = nameof(BTCPayServer), Condition = ">=2.1.0" }
        };

        public override void Execute(IServiceCollection services)
        {
            // Register Lightning connection handler
            services.AddSingleton<ILightningConnectionStringHandler>(provider => 
                provider.GetRequiredService<FlashLightningConnectionStringHandler>());
            services.AddSingleton<FlashLightningConnectionStringHandler>();
            
            // Add UI extensions for Lightning payment setup
            services.AddSingleton<IUIExtension>(new UIExtension("Flash/LNPaymentMethodSetupTab", "ln-payment-method-setup-tab"));
            
            // Add UI extensions for header navigation
            services.AddUIExtension("header-nav", $"{ViewsDirectory}/Shared/Flash/NavExtension.cshtml");
            
            // Register services for database access
            services.AddSingleton<FlashCardDbContextFactory>();
            services.AddDbContext<Data.FlashCardDbContext>((provider, o) =>
            {
                var factory = provider.GetRequiredService<FlashCardDbContextFactory>();
                factory.ConfigureBuilder(o);
            });
            
            // Register hosted services
            services.AddHostedService<HostedServices.FlashPaymentHostedService>();
            
            // Register card services
            services.AddSingleton<FlashCardRegistrationService>();
            
            base.Execute(services);
        }
    }
}