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
        private ILogger<FlashPlugin>? _logger;

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
        public override string Name => "Flash";
        public override string Description => "Integration with Flash wallet featuring full LNURL and Lightning Address support.";
        public override Version Version => new Version(1, 3, 0);

        public override IBTCPayServerPlugin.PluginDependency[] Dependencies => new[]
        {
            new IBTCPayServerPlugin.PluginDependency { Identifier = "BTCPayServer", Condition = ">=2.0.0" }
        };

        public override void Execute(IServiceCollection applicationBuilder)
        {
            try
            {
                FlashPluginLogger.Log("Execute method called");

                // Create a service provider for initialization only
                // This is safer than building the entire service collection which might not be ready
                var serviceProvider = applicationBuilder.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = false });
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                _logger = loggerFactory?.CreateLogger<FlashPlugin>();

                _logger?.LogInformation("Flash Plugin: Starting plugin initialization");
                FlashPluginLogger.Log("Got logger service");

                // Register UI extensions for the Lightning setup section
                applicationBuilder.AddUIExtension("ln-payment-method-setup-tab", "Flash/LNPaymentMethodSetupTab");
                _logger?.LogInformation("Flash Plugin: Registered UI extension");
                FlashPluginLogger.Log("Registered UI extension");

                // Register the Flash Lightning client service
                applicationBuilder.AddSingleton<FlashLightningConnectionStringHandler>();
                applicationBuilder.AddSingleton<ILightningConnectionStringHandler>(provider => provider.GetRequiredService<FlashLightningConnectionStringHandler>());

                // Register FlashLightningClient with a factory method that creates it when needed
                // The factory will use IServiceProvider to get other dependencies like loggers
                applicationBuilder.AddScoped<FlashLightningClient>(provider =>
                {
                    // Get configuration from service or use default values for development
                    var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<FlashLightningClient>();

                    try
                    {
                        // Use default values for development/testing only
                        // In real usage, this client will be created by the connection string handler
                        var bearerToken = "development_token";
                        var endpoint = new Uri("https://api.flashapp.me/graphql");

                        return new FlashLightningClient(
                            bearerToken,
                            endpoint,
                            logger);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error creating FlashLightningClient in factory method");
                        throw;
                    }
                });

                _logger?.LogInformation("Flash Plugin: Registered Lightning services");
                FlashPluginLogger.Log("Registered Lightning services");

                // Register the Pull Payment handler services
                applicationBuilder.AddScoped<Models.FlashPullPaymentHandler>(provider =>
                {
                    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<Models.FlashPullPaymentHandler>();
                    var flashClient = provider.GetService<FlashLightningClient>();
                    return new Models.FlashPullPaymentHandler(logger, flashClient);
                });

                // Register plugin hook filters for Pull Payment support with factory methods

                // 1. PullPaymentDestinationValidator
                applicationBuilder.AddScoped<Models.PullPaymentDestinationValidator>(provider =>
                {
                    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<Models.PullPaymentDestinationValidator>();
                    return new Models.PullPaymentDestinationValidator(logger);
                });

                applicationBuilder.AddScoped<IPluginHookFilter>(provider =>
                    provider.GetRequiredService<Models.PullPaymentDestinationValidator>());

                // 2. PullPaymentClaimProcessor 
                applicationBuilder.AddScoped<Models.PullPaymentClaimProcessor>(provider =>
                {
                    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<Models.PullPaymentClaimProcessor>();
                    var flashClient = provider.GetService<FlashLightningClient>();
                    return new Models.PullPaymentClaimProcessor(logger, flashClient);
                });

                applicationBuilder.AddScoped<IPluginHookFilter>(provider =>
                    provider.GetRequiredService<Models.PullPaymentClaimProcessor>());

                // 3. LnurlWithdrawHandler
                applicationBuilder.AddScoped<Models.LnurlWithdrawHandler>(provider =>
                {
                    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<Models.LnurlWithdrawHandler>();
                    var flashClient = provider.GetService<FlashLightningClient>();
                    var pullPaymentHandler = provider.GetService<Models.FlashPullPaymentHandler>();
                    return new Models.LnurlWithdrawHandler(logger, flashClient, pullPaymentHandler);
                });

                applicationBuilder.AddScoped<IPluginHookFilter>(provider =>
                    provider.GetRequiredService<Models.LnurlWithdrawHandler>());

                _logger?.LogInformation("Flash Plugin: Registered Pull Payment handlers");
                FlashPluginLogger.Log("Registered Pull Payment handlers");

                base.Execute(applicationBuilder);
                _logger?.LogInformation("Flash Plugin: Initialization completed successfully");
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