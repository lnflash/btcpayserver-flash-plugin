#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Plugins.Flash.Data;
using BTCPayServer.Plugins.Flash.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash
{
    public class PluginMigrationRunner : IHostedService
    {
        private readonly FlashCardDbContextFactory _dbContextFactory;
        private readonly ILogger<PluginMigrationRunner> _logger;
        private readonly ISettingsRepository _settingsRepository;

        public PluginMigrationRunner(
            FlashCardDbContextFactory dbContextFactory,
            ILogger<PluginMigrationRunner> logger,
            ISettingsRepository settingsRepository)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _settingsRepository = settingsRepository;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Running Flash plugin database migrations");
                
                var settings = await _settingsRepository.GetSettingAsync<FlashPluginMigrationHistory>() ??
                           new FlashPluginMigrationHistory();
                
                await using var context = _dbContextFactory.CreateContext();
                await context.Database.MigrateAsync(cancellationToken);
                
                // Record that migrations were run
                if (!settings.MigrationsApplied)
                {
                    settings.MigrationsApplied = true;
                    await _settingsRepository.UpdateSetting(settings);
                }
                
                _logger.LogInformation("Flash plugin database migrations completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying Flash plugin database migrations");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        
        private class FlashPluginMigrationHistory
        {
            public bool MigrationsApplied { get; set; }
        }
    }
}