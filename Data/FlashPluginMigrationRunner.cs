using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Data
{
    public class FlashPluginMigrationRunner : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FlashPluginMigrationRunner> _logger;

        public FlashPluginMigrationRunner(
            IServiceProvider serviceProvider,
            ILogger<FlashPluginMigrationRunner> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Flash plugin database migration");
            
            try
            {
                using var scope = _serviceProvider.CreateScope();
                await using var ctx = scope.ServiceProvider.GetRequiredService<FlashPluginDbContext>();
                
                // Check if we're using an in-memory database
                var isInMemory = ctx.Database.IsInMemory();
                
                if (isInMemory)
                {
                    // For in-memory database, just ensure it's created
                    _logger.LogInformation("Using in-memory database, ensuring database is created");
                    await ctx.Database.EnsureCreatedAsync(cancellationToken);
                }
                else
                {
                    // For real databases, run migrations
                    _logger.LogInformation("Running database migrations");
                    await ctx.Database.MigrateAsync(cancellationToken);
                }
                
                _logger.LogInformation("Flash plugin database initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Flash plugin database migration");
                // Don't throw - let BTCPay Server continue without our database features
                // This prevents the entire server from crashing due to our plugin
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}