#nullable enable
using System;
using System.Diagnostics;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Lightning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash
{
    public class FlashPlugin : BaseBTCPayServerPlugin
    {
        private ILogger _logger;

        // Static constructor that will run when the class is loaded
        static FlashPlugin()
        {
            // Use direct file system logging to diagnose issues
            try
            {
                FlashPluginLogger.Log("FlashPlugin class is being loaded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FLASH ERROR in static constructor: {ex.Message}");
            }
        }

        public override string Identifier => "BTCPayServer.Plugins.Flash";
        public override string Name => "Flash Lightning";
        public override string Description => "Integration with Flash Lightning Network wallet.";
        public override Version Version => new Version(1, 0, 0);

        public override IBTCPayServerPlugin.PluginDependency[] Dependencies => new[]
        {
            new IBTCPayServerPlugin.PluginDependency { Identifier = "BTCPayServer", Condition = ">=2.0.0" }
        };

        public override void Execute(IServiceCollection applicationBuilder)
        {
            try
            {
                FlashPluginLogger.Log("Execute method called");

                // Get the logger service
                _logger = applicationBuilder.BuildServiceProvider().GetRequiredService<ILoggerFactory>().CreateLogger("BTCPayServer.Plugins.Flash");
                _logger.LogInformation("Flash Plugin: Starting plugin initialization");
                FlashPluginLogger.Log("Got logger service");

                // Register UI extensions for the Lightning setup section
                applicationBuilder.AddUIExtension("ln-payment-method-setup-tab", "Flash/LNPaymentMethodSetupTab");
                _logger.LogInformation("Flash Plugin: Registered UI extension");
                FlashPluginLogger.Log("Registered UI extension");

                // Register the Flash Lightning client service
                applicationBuilder.AddSingleton<ILightningConnectionStringHandler>(provider => provider.GetRequiredService<FlashLightningConnectionStringHandler>());
                applicationBuilder.AddSingleton<FlashLightningConnectionStringHandler>();
                applicationBuilder.AddSingleton<FlashLightningClient>();
                _logger.LogInformation("Flash Plugin: Registered Lightning services");
                FlashPluginLogger.Log("Registered Lightning services");

                base.Execute(applicationBuilder);
                _logger.LogInformation("Flash Plugin: Initialization completed successfully");
                FlashPluginLogger.Log("Initialization completed successfully");
            }
            catch (Exception ex)
            {
                // Log to file system first
                FlashPluginLogger.Log($"ERROR: {ex.Message}\n{ex.StackTrace}");

                // Log to console 
                Console.WriteLine($"Flash Plugin ERROR: {ex.Message}");
                Console.WriteLine($"Flash Plugin ERROR: {ex.StackTrace}");

                // Also try standard Debug output
                Debug.WriteLine($"Flash Plugin ERROR: {ex.Message}");
                Debug.WriteLine($"Flash Plugin ERROR: {ex.StackTrace}");

                if (_logger != null)
                {
                    _logger.LogError(ex, "Flash Plugin: Error during plugin initialization");
                }
                throw; // Rethrow to let BTCPay Server handle it
            }
        }
    }
}