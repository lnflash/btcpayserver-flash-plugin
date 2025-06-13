#nullable enable
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Background service that polls for Boltcard invoice status updates
    /// </summary>
    public class BoltcardInvoicePoller : BackgroundService
    {
        private readonly ILogger<BoltcardInvoicePoller> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, DateTime> _recentBoltcardPayments = new();
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1); // More aggressive polling
        private readonly TimeSpan _trackingDuration = TimeSpan.FromMinutes(5);

        public BoltcardInvoicePoller(
            ILogger<BoltcardInvoicePoller> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Track a potential Boltcard payment
        /// </summary>
        public void TrackBoltcardPayment(string bolt11)
        {
            var key = $"boltcard_{DateTime.UtcNow.Ticks}_{bolt11.GetHashCode()}";
            _recentBoltcardPayments[key] = DateTime.UtcNow;
            _logger.LogInformation("[BOLTCARD POLLER] Tracking potential Boltcard payment: {Key}", key);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[BOLTCARD POLLER] Starting Boltcard invoice polling service");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollRecentTransactions(stoppingToken);
                    await Task.Delay(_pollInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[BOLTCARD POLLER] Error in polling loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("[BOLTCARD POLLER] Stopping Boltcard invoice polling service");
        }

        private async Task PollRecentTransactions(CancellationToken cancellationToken)
        {
            // Clean up old entries
            var cutoffTime = DateTime.UtcNow - _trackingDuration;
            var keysToRemove = _recentBoltcardPayments
                .Where(kvp => kvp.Value < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _recentBoltcardPayments.TryRemove(key, out _);
            }

            // Skip if no recent payments to track
            if (_recentBoltcardPayments.IsEmpty)
            {
                return;
            }

            _logger.LogDebug("[BOLTCARD POLLER] Polling for {Count} potential Boltcard payments", _recentBoltcardPayments.Count);

            try
            {
                // Create a scope to get scoped services
                using var scope = _serviceProvider.CreateScope();
                var graphQLService = scope.ServiceProvider.GetRequiredService<IFlashGraphQLService>();
                var invoiceService = scope.ServiceProvider.GetRequiredService<IFlashInvoiceService>();
                
                // Get recent transactions from Flash
                var transactions = await graphQLService.GetTransactionHistoryAsync(20, cancellationToken);
                
                // Look for recent small payments that might be Boltcard
                var recentPayments = transactions
                    .Where(t => t.SettlementAmount.HasValue && 
                               Math.Abs(t.SettlementAmount.Value) < 10000 && // Increased threshold
                               t.CreatedAt > DateTime.UtcNow.AddMinutes(-2) && // Look at very recent transactions
                               t.Status?.ToLowerInvariant() == "success")
                    .ToList();

                if (recentPayments.Any())
                {
                    _logger.LogInformation("[BOLTCARD POLLER] Found {Count} recent small payments that might be Boltcard", recentPayments.Count);
                    
                    foreach (var payment in recentPayments)
                    {
                        _logger.LogInformation("[BOLTCARD POLLER] Detected recent small payment: {Id}, Amount: {Amount} sats, Memo: {Memo}",
                            payment.Id, Math.Abs(payment.SettlementAmount ?? 0), payment.Memo);
                        
                        // For any recent small payment, check if there's a corresponding unpaid invoice
                        var pendingInvoices = FlashInvoiceService.GetPendingInvoices();
                        
                        // Try to match by payment hash or memo
                        var matchingInvoice = pendingInvoices.FirstOrDefault(kvp => 
                            kvp.Key == payment.Id || 
                            (payment.Memo != null && payment.Memo.Contains(kvp.Key)));
                        
                        if (matchingInvoice.Value != null)
                        {
                            _logger.LogInformation("[BOLTCARD POLLER] Found matching pending invoice for payment {PaymentId}!", payment.Id);
                            // Mark as paid
                            await invoiceService.MarkInvoiceAsPaidAsync(
                                matchingInvoice.Key, 
                                Math.Abs(payment.SettlementAmount ?? 0));
                        }
                        else if (payment.Memo?.Contains("Boltcard", StringComparison.OrdinalIgnoreCase) == true ||
                                 Math.Abs(payment.SettlementAmount ?? 0) < 1000)
                        {
                            // Even if we don't have a matching invoice, mark it as paid
                            // This handles cases where the invoice wasn't tracked initially
                            _logger.LogInformation("[BOLTCARD POLLER] Marking likely Boltcard payment as paid: {Id}", payment.Id);
                            await invoiceService.MarkInvoiceAsPaidAsync(
                                payment.Id, 
                                Math.Abs(payment.SettlementAmount ?? 0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BOLTCARD POLLER] Error polling transactions");
            }
        }
    }
}