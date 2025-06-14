#nullable enable
using System;
using System.Diagnostics;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.Data;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Flash.Services;
using Microsoft.EntityFrameworkCore;
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
        public override string Description => "Integration with Flash wallet featuring full LNURL, Lightning Address and Boltcard support.";
        public override Version Version => new Version(1, 4, 8);

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

                // Simple UI extension for Lightning setup
                applicationBuilder.AddUIExtension("ln-payment-method-setup-tab", "Flash/LNPaymentMethodSetupTab");

                // Add a simple navigation item
                try
                {
                    applicationBuilder.AddUIExtension("header-nav", "Flash/BasicNav");
                }
                catch (Exception ex)
                {
                    FlashPluginLogger.Log($"Error adding header-nav extension: {ex.Message}");
                    // Fallback to another extension point
                    try
                    {
                        applicationBuilder.AddUIExtension("navbar", "Flash/BasicNav");
                    }
                    catch (Exception fallbackEx)
                    {
                        FlashPluginLogger.Log($"Error adding navbar extension: {fallbackEx.Message}");
                    }
                }

                // Add store navigation items
                applicationBuilder.AddUIExtension("store-nav", "Flash/_Nav");
                
                _logger?.LogInformation("Flash Plugin: Registered UI extensions");
                FlashPluginLogger.Log("Registered UI extensions");

                // Register the Flash Lightning client service
                applicationBuilder.AddSingleton<FlashLightningConnectionStringHandler>();
                applicationBuilder.AddSingleton<ILightningConnectionStringHandler>(provider => provider.GetRequiredService<FlashLightningConnectionStringHandler>());

                // Register WebSocket service for real-time updates
                applicationBuilder.AddScoped<IFlashWebSocketService, FlashWebSocketService>();
                
                // Register core services
                applicationBuilder.AddScoped<IFlashGraphQLService, FlashGraphQLService>();
                applicationBuilder.AddScoped<IFlashInvoiceService, FlashInvoiceService>();
                applicationBuilder.AddScoped<IFlashExchangeRateService, FlashExchangeRateService>();
                applicationBuilder.AddScoped<IFlashBoltcardService, FlashBoltcardService>();
                applicationBuilder.AddScoped<IFlashTransactionService, FlashTransactionService>();
                applicationBuilder.AddScoped<IFlashWalletService, FlashWalletService>();
                applicationBuilder.AddScoped<IFlashMonitoringService, FlashMonitoringService>();
                applicationBuilder.AddScoped<IFlashPaymentService>(provider => 
                    new FlashPaymentService(
                        provider.GetRequiredService<IFlashGraphQLService>(),
                        provider.GetRequiredService<IFlashExchangeRateService>(),
                        provider.GetRequiredService<ILogger<FlashPaymentService>>(),
                        provider
                    ));
                applicationBuilder.AddHostedService<BoltcardInvoicePoller>();
                
                // Register payout tracking services and database
                applicationBuilder.AddDbContext<Data.FlashPluginDbContext>((provider, options) =>
                {
                    // Get the database context factory from BTCPay Server
                    var dbContextFactory = provider.GetService<ApplicationDbContextFactory>();
                    if (dbContextFactory != null)
                    {
                        // Use the same database configuration as BTCPay Server
                        dbContextFactory.ConfigureBuilder(options);
                    }
                    else
                    {
                        // Fallback to in-memory database for development/testing
                        options.UseInMemoryDatabase("FlashPlugin");
                        _logger?.LogWarning("Using in-memory database for Flash plugin - this is not recommended for production");
                    }
                });
                applicationBuilder.AddScoped<Data.FlashPayoutRepository>();
                applicationBuilder.AddScoped<IFlashPayoutTrackingService, FlashPayoutTrackingService>();
                applicationBuilder.AddHostedService<Data.FlashPluginMigrationRunner>();

                // Register FlashLightningClient with a factory method that creates it when needed
                // The factory will use IServiceProvider to get other dependencies like loggers
                applicationBuilder.AddScoped<FlashLightningClient>(provider =>
                {
                    var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger<FlashLightningClient>();

                    try
                    {
                        // NOTE: This factory is only used for dependency injection registration
                        // The actual client instances are created by FlashLightningConnectionStringHandler
                        // with proper authentication tokens from the connection string
                        logger.LogWarning("FlashLightningClient factory method called - this should only happen during DI registration");

                        // Return null - the actual clients will be created by the connection string handler
                        return null;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in FlashLightningClient factory method");
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
                    return new Models.PullPaymentClaimProcessor(logger, flashClient, provider);
                });

                applicationBuilder.AddScoped<IPluginHookFilter>(provider =>
                    provider.GetRequiredService<Models.PullPaymentClaimProcessor>());

                // 3. Regular LnurlWithdrawHandler
                applicationBuilder.AddScoped<Models.LnurlWithdrawHandler>(provider =>
                {
                    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<Models.LnurlWithdrawHandler>();
                    var flashClient = provider.GetService<FlashLightningClient>();
                    var pullPaymentHandler = provider.GetService<Models.FlashPullPaymentHandler>();
                    return new Models.LnurlWithdrawHandler(logger, flashClient, pullPaymentHandler);
                });

                applicationBuilder.AddScoped<IPluginHookFilter>(provider =>
                    provider.GetRequiredService<Models.LnurlWithdrawHandler>());

                // 4. Boltcard patch for amount verification
                applicationBuilder.AddScoped<Models.BoltcardPatch>(provider =>
                {
                    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<Models.BoltcardPatch>();
                    return new Models.BoltcardPatch(logger);
                });

                applicationBuilder.AddScoped<IPluginHookFilter>(provider =>
                    provider.GetRequiredService<Models.BoltcardPatch>());

                // 5. Boltcard invoice tracker for payment detection
                applicationBuilder.AddScoped<Models.BoltcardInvoiceTracker>(provider =>
                {
                    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<Models.BoltcardInvoiceTracker>();
                    return new Models.BoltcardInvoiceTracker(logger, provider);
                });

                applicationBuilder.AddScoped<IPluginHookFilter>(provider =>
                    provider.GetRequiredService<Models.BoltcardInvoiceTracker>());
                    
                // 6. Payout event listeners for tracking
                applicationBuilder.AddScoped<Models.PayoutEventListener>();
                applicationBuilder.AddScoped<IPluginHookFilter>(provider =>
                    provider.GetRequiredService<Models.PayoutEventListener>());
                    
                applicationBuilder.AddScoped<Models.PayoutStateChangeListener>();
                applicationBuilder.AddScoped<IPluginHookFilter>(provider =>
                    provider.GetRequiredService<Models.PayoutStateChangeListener>());

                _logger?.LogInformation("Flash Plugin: Registered Pull Payment handlers");
                FlashPluginLogger.Log("Registered Pull Payment handlers");

                // Register controllers
                applicationBuilder.AddScoped<Controllers.BoltcardTopupController>();
                applicationBuilder.AddScoped<Controllers.FlashMainController>();
                applicationBuilder.AddScoped<Controllers.UIFlashController>();
                applicationBuilder.AddScoped<Controllers.FlashRedirectController>();
                applicationBuilder.AddScoped<Controllers.FlashPayoutController>();
                applicationBuilder.AddSingleton<FlashPlugin>(this);
                _logger?.LogInformation("Flash Plugin: Registered controllers");
                FlashPluginLogger.Log("Registered controllers");

                base.Execute(applicationBuilder);
                _logger?.LogInformation("Flash Plugin: Initialization completed successfully");
                FlashPluginLogger.Log("Initialization completed successfully");
            }
            catch (Exception ex)
            {
                // Log to file system first
                FlashPluginLogger.Log($"ERROR: {ex.Message}\n{ex.StackTrace}");

                // Use proper logging instead of console output

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