#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Implementation of monitoring service for payment transactions and invoice status changes
    /// </summary>
    public class FlashMonitoringService : IFlashMonitoringService
    {
        private readonly IFlashInvoiceService _invoiceService;
        private readonly IFlashTransactionService _transactionService;
        private readonly IFlashBoltcardService _boltcardService;
        private readonly IFlashWebSocketService? _webSocketService;
        private readonly ILogger<FlashMonitoringService> _logger;
        
        // Monitoring state
        private CancellationTokenSource? _cts;
        private Task? _pollingTask;
        private readonly Channel<LightningInvoice> _channel;
        private readonly ChannelReader<LightningInvoice> _reader;
        private readonly ChannelWriter<LightningInvoice> _writer;
        private bool _isMonitoring;
        private readonly object _monitoringLock = new();
        
        // WebSocket state
        private bool _usingWebSocket;

        public bool IsMonitoring => _isMonitoring;
        public event EventHandler<PaymentDetectedEventArgs>? PaymentDetected;

        public FlashMonitoringService(
            IFlashInvoiceService invoiceService,
            IFlashTransactionService transactionService,
            IFlashBoltcardService boltcardService,
            IFlashWebSocketService? webSocketService,
            ILogger<FlashMonitoringService> logger)
        {
            _invoiceService = invoiceService ?? throw new ArgumentNullException(nameof(invoiceService));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _boltcardService = boltcardService ?? throw new ArgumentNullException(nameof(boltcardService));
            _webSocketService = webSocketService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Create the channel for paid invoice notifications
            _channel = Channel.CreateUnbounded<LightningInvoice>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });
            _reader = _channel.Reader;
            _writer = _channel.Writer;
        }

        public async Task StartMonitoringAsync(CancellationToken cancellation = default)
        {
            lock (_monitoringLock)
            {
                if (_isMonitoring)
                {
                    _logger.LogWarning("Monitoring is already active");
                    return;
                }

                _logger.LogInformation("Starting payment monitoring service");
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                _isMonitoring = true;
            }

            // Start monitoring based on available services
            if (_webSocketService != null)
            {
                _logger.LogInformation("Attempting to use WebSocket for real-time updates");
                _usingWebSocket = await TryStartWebSocketMonitoring();
                
                if (!_usingWebSocket)
                {
                    _logger.LogWarning("WebSocket connection failed, falling back to polling");
                }
            }

            // Always start polling as a fallback or primary method
            if (!_usingWebSocket || _webSocketService == null)
            {
                _logger.LogInformation("Starting invoice polling task");
                _pollingTask = Task.Run(() => PollInvoicesAsync(_cts!.Token), _cts!.Token);
            }
            else
            {
                _logger.LogInformation("Using WebSocket for real-time invoice updates");
                // Still start a lightweight polling task for redundancy
                _pollingTask = Task.Run(() => PollInvoicesAsync(_cts!.Token), _cts!.Token);
            }
        }

        public async Task StopMonitoringAsync()
        {
            lock (_monitoringLock)
            {
                if (!_isMonitoring)
                {
                    _logger.LogWarning("Monitoring is not active");
                    return;
                }

                _logger.LogInformation("Stopping payment monitoring service");
                _isMonitoring = false;
            }

            // Cancel monitoring tasks
            _cts?.Cancel();

            // Wait for polling task to complete
            if (_pollingTask != null)
            {
                try
                {
                    await _pollingTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            // Disconnect WebSocket if connected
            if (_usingWebSocket && _webSocketService != null)
            {
                await _webSocketService.DisconnectAsync();
            }

            _cts?.Dispose();
            _cts = null;
            _pollingTask = null;
            _usingWebSocket = false;
        }

        public async Task<LightningInvoice> WaitInvoiceAsync(CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("[INVOICE DEBUG] WaitInvoice called - waiting for invoice payment notifications");

                // Try to read an invoice from the channel
                var invoice = await _reader.ReadAsync(cancellation);

                // Ensure the invoice has all the required properties set
                if (invoice.Status == LightningInvoiceStatus.Paid)
                {
                    if (invoice.AmountReceived == null || invoice.AmountReceived.MilliSatoshi == 0)
                    {
                        _logger.LogInformation($"[INVOICE DEBUG] Invoice {invoice.Id} is marked as Paid but AmountReceived is not set, using Amount");
                        invoice.AmountReceived = invoice.Amount;
                    }
                }

                _logger.LogInformation($"[INVOICE DEBUG] WaitInvoice returning paid invoice: ID={invoice.Id}, " +
                    $"Status={invoice.Status}, Amount={invoice.Amount}, " +
                    $"AmountReceived={invoice.AmountReceived}, PaymentHash={invoice.PaymentHash}");

                return invoice;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[INVOICE DEBUG] WaitInvoice cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[INVOICE DEBUG] Error in WaitInvoice");
                throw;
            }
        }

        public async Task<bool> EnhancedBoltcardTrackingAsync(
            string paymentHash, 
            long amountSats, 
            string boltcardId,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation($"[BOLTCARD DEBUG] Starting enhanced tracking for {paymentHash}, amount: {amountSats} sats, card: {boltcardId}");

                // Extract sequence number if available
                string? sequenceNumber = null;
                var invoice = await _invoiceService.GetInvoiceAsync(paymentHash, cancellation);
                if (invoice != null && !string.IsNullOrEmpty(invoice.BOLT11))
                {
                    // Try to extract sequence from invoice memo/description
                    sequenceNumber = _boltcardService.ExtractSequenceFromMemo(invoice.BOLT11);
                }

                // Use the transaction service for enhanced tracking
                var result = await _transactionService.CheckBoltcardTransactionAsync(
                    paymentHash, 
                    amountSats, 
                    boltcardId, 
                    sequenceNumber,
                    cancellation);

                if (result)
                {
                    _logger.LogInformation($"[BOLTCARD DEBUG] Transaction detected! Marking invoice as paid.");
                    await MarkInvoiceAsPaidAsync(paymentHash, amountSats, boltcardId);
                    
                    // Raise payment detected event
                    PaymentDetected?.Invoke(this, new PaymentDetectedEventArgs(
                        paymentHash, 
                        amountSats, 
                        boltcardId));
                }
                else
                {
                    _logger.LogWarning($"[BOLTCARD DEBUG] No transaction detected after enhanced tracking");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[BOLTCARD DEBUG] Error in enhanced tracking for {paymentHash}");
                throw;
            }
        }

        public ChannelReader<LightningInvoice> GetInvoiceReader() => _reader;

        public void Dispose()
        {
            StopMonitoringAsync().GetAwaiter().GetResult();
            _channel.Writer.TryComplete();
        }

        private async Task<bool> TryStartWebSocketMonitoring()
        {
            if (_webSocketService == null)
                return false;

            try
            {
                // Set up WebSocket event handlers
                _webSocketService.PaymentReceived += OnWebSocketPaymentReceived;
                
                // Note: WebSocket connection is typically established by the FlashLightningClient
                // We just check if it's already connected
                if (_webSocketService.IsConnected)
                {
                    _logger.LogInformation("WebSocket already connected for payment monitoring");
                    return true;
                }
                
                _logger.LogWarning("WebSocket not connected - will rely on polling");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start WebSocket monitoring");
                return false;
            }
        }

        private void OnWebSocketPaymentReceived(object? sender, PaymentReceivedEventArgs e)
        {
            _logger.LogInformation($"[WEBSOCKET] Payment received event: PaymentHash={e.PaymentHash}, Amount={e.AmountSats}");
            
            // Find the corresponding invoice and mark it as paid
            Task.Run(async () =>
            {
                try
                {
                    var invoice = await _invoiceService.GetInvoiceAsync(e.PaymentHash, CancellationToken.None);
                    if (invoice != null)
                    {
                        invoice.Status = LightningInvoiceStatus.Paid;
                        invoice.AmountReceived = LightMoney.Satoshis(e.AmountSats);
                        
                        // Write to channel
                        if (!_writer.TryWrite(invoice))
                        {
                            _logger.LogWarning($"[WEBSOCKET] Failed to write invoice to channel: {e.PaymentHash}");
                        }
                        
                        // Raise event
                        PaymentDetected?.Invoke(this, new PaymentDetectedEventArgs(
                            e.PaymentHash, 
                            e.AmountSats,
                            transactionId: e.TransactionId));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[WEBSOCKET] Error processing payment event for {e.PaymentHash}");
                }
            });
        }

        private async Task PollInvoicesAsync(CancellationToken cancellation)
        {
            try
            {
                _logger.LogInformation("[INVOICE DEBUG] Invoice polling task started");

                // Dictionary to keep track of monitored invoices and their status
                var monitoredInvoices = new Dictionary<string, LightningInvoiceStatus>();

                while (!cancellation.IsCancellationRequested)
                {
                    // Sleep before polling to avoid high CPU usage
                    await Task.Delay(5000, cancellation);

                    try
                    {
                        // Get pending invoices from the service
                        var pendingInvoices = FlashInvoiceService.GetPendingInvoices();
                        var pendingInvoicesToCheck = pendingInvoices.Keys.ToList();
                        var totalCount = pendingInvoices.Count;

                        _logger.LogInformation($"[INVOICE DEBUG] Checking {pendingInvoicesToCheck.Count} pending invoices for payment updates");
                        _logger.LogInformation($"[INVOICE DEBUG] Total pending invoices in dictionary: {totalCount}");

                        if (pendingInvoicesToCheck.Count > 0)
                        {
                            var invoiceIds = string.Join(", ", pendingInvoicesToCheck.Take(5)); // Log first 5
                            _logger.LogInformation($"[INVOICE DEBUG] Sample pending invoice IDs: {invoiceIds}...");
                        }

                        foreach (var invoiceId in pendingInvoicesToCheck)
                        {
                            try
                            {
                                // Get the current status from our cache
                                if (!pendingInvoices.TryGetValue(invoiceId, out var pendingInvoice))
                                {
                                    _logger.LogWarning($"[INVOICE DEBUG] Invoice {invoiceId} no longer in pending invoices");
                                    continue;
                                }
                                
                                var oldStatus = pendingInvoice.Status;
                                _logger.LogDebug($"[INVOICE DEBUG] Checking invoice {invoiceId} - Current status: {oldStatus}");

                                // Try to get an updated status
                                var updatedInvoice = await _invoiceService.GetInvoiceAsync(invoiceId, cancellation);

                                if (updatedInvoice.Status == LightningInvoiceStatus.Paid &&
                                    oldStatus != LightningInvoiceStatus.Paid)
                                {
                                    _logger.LogInformation($"[INVOICE DEBUG] Detected payment for invoice {invoiceId}");

                                    // Make sure AmountReceived is set correctly
                                    if (updatedInvoice.AmountReceived == null || updatedInvoice.AmountReceived.MilliSatoshi == 0)
                                    {
                                        _logger.LogInformation($"[INVOICE DEBUG] Setting AmountReceived = Amount for invoice {invoiceId}");
                                        updatedInvoice.AmountReceived = updatedInvoice.Amount;
                                    }

                                    // Add to monitored invoices so we don't check it multiple times
                                    monitoredInvoices[invoiceId] = updatedInvoice.Status;

                                    // Notify about the payment
                                    _logger.LogInformation($"[INVOICE DEBUG] Writing paid invoice {invoiceId} to notification channel");
                                    
                                    if (!_writer.TryWrite(updatedInvoice))
                                    {
                                        _logger.LogWarning($"[INVOICE DEBUG] Failed to write invoice {invoiceId} to channel");
                                    }
                                    else
                                    {
                                        _logger.LogInformation($"[INVOICE DEBUG] Successfully wrote paid invoice to channel: ID={invoiceId}");
                                        
                                        // Raise event
                                        PaymentDetected?.Invoke(this, new PaymentDetectedEventArgs(
                                            updatedInvoice.PaymentHash ?? invoiceId,
                                            (long)(updatedInvoice.Amount?.ToUnit(LightMoneyUnit.Satoshi) ?? 0)));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"[INVOICE DEBUG] Error checking pending invoice {invoiceId}");
                            }
                        }

                        _logger.LogDebug($"[INVOICE DEBUG] Invoice polling cycle completed - monitoring {monitoredInvoices.Count} invoices");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[INVOICE DEBUG] Error polling for invoice status");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[INVOICE DEBUG] Invoice polling task cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[INVOICE DEBUG] Fatal error in invoice polling task");
            }
        }

        private async Task MarkInvoiceAsPaidAsync(string paymentHash, long amountSats, string? boltcardId = null)
        {
            try
            {
                var invoice = await _invoiceService.GetInvoiceAsync(paymentHash, CancellationToken.None);
                if (invoice != null)
                {
                    invoice.Status = LightningInvoiceStatus.Paid;
                    invoice.AmountReceived = LightMoney.Satoshis(amountSats);
                    
                    // Write to channel
                    if (!_writer.TryWrite(invoice))
                    {
                        _logger.LogWarning($"Failed to write paid invoice to channel: {paymentHash}");
                    }
                    
                    _logger.LogInformation($"Invoice {paymentHash} marked as paid with amount {amountSats} sats");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking invoice as paid: {paymentHash}");
            }
        }
    }
}