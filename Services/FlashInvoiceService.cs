#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using BTCPayServer.Lightning;
using NBitcoin;
using GraphQL;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Implementation of invoice service for Lightning invoice operations
    /// </summary>
    public class FlashInvoiceService : IFlashInvoiceService
    {
        private readonly IFlashGraphQLService _graphQLService;
        private readonly IFlashExchangeRateService _exchangeRateService;
        private readonly IFlashBoltcardService _boltcardService;
        private readonly ILogger<FlashInvoiceService> _logger;

        // Shared static tracking for invoice monitoring across all instances
        private static readonly Dictionary<string, LightningInvoice> _pendingInvoices = new Dictionary<string, LightningInvoice>();
        private static readonly Dictionary<string, DateTime> _invoiceCreationTimes = new Dictionary<string, DateTime>();
        private static readonly object _invoiceTrackingLock = new object();

        // Store reference to current invoice listener so we can notify BTCPay Server when payments are detected
        // Static so all instances can notify BTCPay Server regardless of which instance detects the payment
        private static System.Threading.Channels.Channel<LightningInvoice>? _currentInvoiceListener;

        // Constants
        private const int FLASH_MINIMUM_CENTS = 1; // Flash minimum is $0.01 (1 cent)

        public FlashInvoiceService(
            IFlashGraphQLService graphQLService,
            IFlashExchangeRateService exchangeRateService,
            IFlashBoltcardService boltcardService,
            ILogger<FlashInvoiceService> logger)
        {
            _graphQLService = graphQLService ?? throw new ArgumentNullException(nameof(graphQLService));
            _exchangeRateService = exchangeRateService ?? throw new ArgumentNullException(nameof(exchangeRateService));
            _boltcardService = boltcardService ?? throw new ArgumentNullException(nameof(boltcardService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<LightningInvoice> CreateInvoiceAsync(
            CreateInvoiceParams createParams,
            CancellationToken cancellation = default)
        {
            try
            {
                // Get wallet information
                var walletInfo = await _graphQLService.GetWalletInfoAsync(cancellation);
                if (walletInfo == null)
                {
                    throw new InvalidOperationException("Could not determine wallet information for invoice creation");
                }

                // Get the amount in satoshis
                long amountSats = 0;
                if (createParams.Amount != null)
                {
                    amountSats = (long)createParams.Amount.MilliSatoshi / 1000;
                }

                // We'll check and adjust for Flash's USD minimum later when we have the exchange rate
                // For now, just ensure we have at least 1 satoshi
                if (amountSats < 1)
                {
                    _logger.LogInformation("Requested amount {AmountSats} sats is below 1 sat. Adjusting to 1 sat.",
                        amountSats);
                    amountSats = 1;
                }

                string memo = createParams.Description ?? "BTCPay Server Payment";

                // Simplify memo format for Flash API compatibility
                memo = ProcessMemoForFlashApi(memo);

                _logger.LogInformation("Creating invoice for {AmountSats} sats with memo: '{Memo}'", amountSats, memo);

                // Check if this is a Boltcard invoice and prepare enhanced memo BEFORE creating the invoice
                bool isBoltcard = amountSats < 10000 || memo.ToLowerInvariant().Contains("boltcard");
                string finalMemo = memo;
                string uniqueSequence = "";
                string boltcardId = "";

                if (isBoltcard)
                {
                    _logger.LogInformation("Pre-processing Boltcard invoice - Amount: {AmountSats} sats, Memo: '{Memo}'", amountSats, memo);

                    // Extract Boltcard ID from memo
                    boltcardId = _boltcardService.ExtractBoltcardId(memo);
                    _logger.LogInformation("Extracted Boltcard ID: {BoltcardId}", boltcardId);

                    // Generate unique sequence for precise correlation
                    uniqueSequence = _boltcardService.GenerateUniqueSequence();
                    _logger.LogInformation("Generated unique sequence: {UniqueSequence}", uniqueSequence);

                    // Create enhanced memo with sequence for precise matching
                    finalMemo = _boltcardService.CreateEnhancedMemo(memo, boltcardId, uniqueSequence, amountSats);
                    _logger.LogInformation("Enhanced memo for correlation: {FinalMemo}", finalMemo);
                }

                // Create the invoice through Flash API
                var invoice = await CreateFlashInvoice(walletInfo, amountSats, finalMemo, cancellation);

                // Set up enhanced Boltcard tracking if this was identified as a Boltcard invoice
                if (isBoltcard)
                {
                    _logger.LogInformation("Setting up enhanced tracking for Boltcard invoice - Amount: {AmountSats} sats, Card: {BoltcardId}, Sequence: {UniqueSequence}",
                        amountSats, boltcardId, uniqueSequence);

                    // Start enhanced tracking in background with error handling
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            _logger.LogInformation("Background task starting for {InvoiceId}", invoice.Id);
                            await _boltcardService.StartEnhancedTrackingAsync(invoice.Id, amountSats, boltcardId);
                            _logger.LogInformation("Background task completed for {InvoiceId}", invoice.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Background task failed for {InvoiceId}: {Message}", invoice.Id, ex.Message);
                        }
                    });
                }

                // Track this invoice for later status checks
                TrackPendingInvoice(invoice);

                return invoice;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Flash invoice");
                throw;
            }
        }

        public async Task<LightningInvoice> CreateInvoiceAsync(
            LightMoney amount,
            string description,
            TimeSpan expiry,
            CancellationToken cancellation = default)
        {
            // Create the params manually without using the constructor
            var parameters = typeof(CreateInvoiceParams).GetConstructors()[0].Invoke(new object[] { });
            var createParams = (CreateInvoiceParams)parameters;

            // Set properties manually
            typeof(CreateInvoiceParams).GetProperty("Amount")?.SetValue(createParams, amount);
            typeof(CreateInvoiceParams).GetProperty("Description")?.SetValue(createParams, description);
            typeof(CreateInvoiceParams).GetProperty("Expiry")?.SetValue(createParams, expiry);

            return await CreateInvoiceAsync(createParams, cancellation);
        }

        public async Task<LightningInvoice> GetInvoiceAsync(
            string invoiceId,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("[BOLTCARD] GetInvoiceAsync called for {InvoiceId}", invoiceId);

                // Occasionally clean up old pending invoices
                CleanupOldPendingInvoices();

                // First check if this is a pending invoice we're tracking
                if (_pendingInvoices.TryGetValue(invoiceId, out var pendingInvoice))
                {
                    _logger.LogInformation("[BOLTCARD] Found invoice {InvoiceId} in pending cache, Status: {Status}", 
                        invoiceId, pendingInvoice.Status);

                    // If it's been less than 10 seconds since creation, just return it as is
                    // This gives the API time to index the new transaction
                    var timeSinceCreation = DateTime.UtcNow - _invoiceCreationTimes[invoiceId];
                    if (timeSinceCreation.TotalSeconds < 10)
                    {
                        _logger.LogInformation("Invoice {InvoiceId} was recently created ({TimeSinceCreation:F1}s ago), returning cached status",
                            invoiceId, timeSinceCreation.TotalSeconds);
                        return pendingInvoice;
                    }
                }

                // Check if this might be a Boltcard/LNURL invoice that we haven't seen before
                if (!_pendingInvoices.ContainsKey(invoiceId))
                {
                    _logger.LogInformation("[BOLTCARD] Invoice {InvoiceId} not in cache - this is likely a Boltcard/LNURL invoice", invoiceId);
                    
                    // For Boltcard invoices, we need to aggressively check payment status
                    // because BTCPayServer expects immediate updates
                    
                    // First, check recent transactions to see if this was just paid
                    var recentTransactions = await _graphQLService.GetTransactionHistoryAsync(10, cancellation);
                    var matchingTx = recentTransactions.FirstOrDefault(t => 
                        t.Id == invoiceId || 
                        (t.Memo != null && t.Memo.Contains(invoiceId)) ||
                        (t.CreatedAt > DateTime.UtcNow.AddMinutes(-1) && Math.Abs(t.SettlementAmount ?? 0) < 1000));
                    
                    if (matchingTx != null && matchingTx.Status?.ToLowerInvariant() == "success")
                    {
                        _logger.LogInformation("[BOLTCARD] Found matching paid transaction for invoice {InvoiceId}! Amount: {Amount} sats", 
                            invoiceId, Math.Abs(matchingTx.SettlementAmount ?? 0));
                        
                        var paidInvoice = new LightningInvoice
                        {
                            Id = invoiceId,
                            PaymentHash = invoiceId,
                            Status = LightningInvoiceStatus.Paid,
                            Amount = LightMoney.Satoshis(Math.Abs(matchingTx.SettlementAmount ?? 0)),
                            AmountReceived = LightMoney.Satoshis(Math.Abs(matchingTx.SettlementAmount ?? 0)),
                            PaidAt = new DateTimeOffset(matchingTx.CreatedAt, TimeSpan.Zero),
                            ExpiresAt = DateTime.UtcNow.AddDays(1)
                        };
                        
                        // CRITICAL: Notify BTCPayServer immediately
                        lock (_invoiceTrackingLock)
                        {
                            _pendingInvoices[invoiceId] = paidInvoice;
                            _invoiceCreationTimes[invoiceId] = DateTime.UtcNow;
                        }
                        
                        await MarkInvoiceAsPaidAsync(invoiceId, Math.Abs(matchingTx.SettlementAmount ?? 0));
                        
                        return paidInvoice;
                    }
                    
                    // If not found in recent transactions, create a tracking entry
                    var potentialBoltcardInvoice = new LightningInvoice
                    {
                        Id = invoiceId,
                        PaymentHash = invoiceId,
                        Status = LightningInvoiceStatus.Unpaid,
                        Amount = LightMoney.Satoshis(1000), // Default amount
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                        BOLT11 = "" // We don't have the BOLT11 yet
                    };
                    
                    // Add to tracking
                    lock (_invoiceTrackingLock)
                    {
                        _pendingInvoices[invoiceId] = potentialBoltcardInvoice;
                        _invoiceCreationTimes[invoiceId] = DateTime.UtcNow;
                    }
                    
                    _logger.LogInformation("[BOLTCARD] Added potential Boltcard invoice {InvoiceId} to tracking for monitoring", invoiceId);
                    
                    // Start aggressive monitoring for this invoice
                    _ = Task.Run(async () => 
                    {
                        for (int i = 0; i < 10; i++) // Check for 20 seconds
                        {
                            await Task.Delay(2000);
                            
                            var status = await _graphQLService.GetInvoiceStatusAsync(invoiceId, cancellation);
                            if (status != null && status.IsPaid)
                            {
                                _logger.LogInformation("[BOLTCARD] Invoice {InvoiceId} detected as PAID in monitoring loop!", invoiceId);
                                await MarkInvoiceAsPaidAsync(invoiceId, status.AmountReceived ?? 1000);
                                break;
                            }
                            
                            // Also check transaction history
                            var txs = await _graphQLService.GetTransactionHistoryAsync(5, cancellation);
                            var paidTx = txs.FirstOrDefault(t => 
                                (t.Id == invoiceId || (t.Memo != null && t.Memo.Contains(invoiceId))) && 
                                t.Status?.ToLowerInvariant() == "success");
                                
                            if (paidTx != null)
                            {
                                _logger.LogInformation("[BOLTCARD] Found paid transaction for {InvoiceId} in monitoring loop!", invoiceId);
                                await MarkInvoiceAsPaidAsync(invoiceId, Math.Abs(paidTx.SettlementAmount ?? 0));
                                break;
                            }
                        }
                    });
                }

                // First, try to get invoice status directly by payment hash
                var invoiceStatus = await _graphQLService.GetInvoiceStatusAsync(invoiceId, cancellation);
                if (invoiceStatus != null && invoiceStatus.IsPaid)
                {
                    _logger.LogInformation("[BOLTCARD] Invoice {InvoiceId} is PAID according to Flash API (second check)", invoiceId);
                    
                    var paidInvoice = new LightningInvoice
                    {
                        Id = invoiceId,
                        PaymentHash = invoiceId,
                        Status = LightningInvoiceStatus.Paid,
                        Amount = pendingInvoice?.Amount,
                        AmountReceived = invoiceStatus.AmountReceived.HasValue 
                            ? LightMoney.Satoshis(Math.Abs(invoiceStatus.AmountReceived.Value))
                            : pendingInvoice?.Amount ?? LightMoney.Zero,
                        PaidAt = invoiceStatus.PaidAt.HasValue 
                            ? new DateTimeOffset(invoiceStatus.PaidAt.Value, TimeSpan.Zero)
                            : DateTimeOffset.UtcNow,
                        ExpiresAt = pendingInvoice?.ExpiresAt ?? DateTime.UtcNow.AddDays(1)
                    };

                    // Update BOLT11 from cache if available
                    if (pendingInvoice != null)
                    {
                        paidInvoice.BOLT11 = pendingInvoice.BOLT11;
                    }

                    // Update our cache
                    lock (_invoiceTrackingLock)
                    {
                        _pendingInvoices[invoiceId] = paidInvoice;
                    }

                    // CRITICAL: Mark the invoice as paid to trigger Boltcard credit
                    var amountSats = paidInvoice.AmountReceived != null 
                        ? (long)(paidInvoice.AmountReceived.MilliSatoshi / 1000) 
                        : 0;
                    await MarkInvoiceAsPaidAsync(invoiceId, amountSats);

                    return paidInvoice;
                }

                // Fallback to transaction history search
                var walletInfo = await _graphQLService.GetWalletInfoAsync(cancellation);
                if (walletInfo == null)
                {
                    _logger.LogWarning("Cannot get invoice status: No wallet found");

                    // If we have a pending invoice, return that
                    if (pendingInvoice != null)
                    {
                        _logger.LogInformation("Returning cached invoice {InvoiceId} since no wallet was found", invoiceId);
                        return pendingInvoice;
                    }

                    return CreateDefaultUnpaidInvoice(invoiceId);
                }

                // Get transaction history to find matching invoice
                var transactions = await _graphQLService.GetTransactionHistoryAsync(50, cancellation);

                // Find the transaction matching our ID or containing it in the memo
                var matchingTransaction = transactions.FirstOrDefault(t =>
                    t.Id == invoiceId ||
                    (t.Memo != null && t.Memo.Contains(invoiceId)));

                if (matchingTransaction != null)
                {
                    var invoice = CreateInvoiceFromTransaction(invoiceId, matchingTransaction);

                    // If we have a pending invoice, update BOLT11 from our cache
                    if (pendingInvoice != null)
                    {
                        invoice.BOLT11 = pendingInvoice.BOLT11;

                        // Update our cache with the latest status
                        if (invoice.Status != pendingInvoice.Status)
                        {
                            _logger.LogInformation("Updating cached invoice {InvoiceId} status from {OldStatus} to {NewStatus}",
                                invoiceId, pendingInvoice.Status, invoice.Status);

                            lock (_invoiceTrackingLock)
                            {
                                _pendingInvoices[invoiceId] = invoice;
                            }
                        }
                    }

                    return invoice;
                }

                _logger.LogWarning("Transaction {InvoiceId} not found in Flash API", invoiceId);

                // Return our pending invoice if available
                if (pendingInvoice != null)
                {
                    _logger.LogInformation("Returning cached invoice {InvoiceId} because no matching transaction was found", invoiceId);
                    return pendingInvoice;
                }

                return CreateDefaultUnpaidInvoice(invoiceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoice {InvoiceId}", invoiceId);

                // If we have a pending invoice, return that in case of error
                if (_pendingInvoices.TryGetValue(invoiceId, out var pendingInvoice))
                {
                    _logger.LogInformation("Returning cached invoice {InvoiceId} due to error", invoiceId);
                    return pendingInvoice;
                }

                throw;
            }
        }

        public async Task<LightningInvoice> GetInvoiceAsync(
            uint256 invoiceId,
            CancellationToken cancellation = default)
        {
            return await GetInvoiceAsync(invoiceId.ToString(), cancellation);
        }

        public Task<LightningInvoice[]> ListInvoicesAsync(
            ListInvoicesParams? request = null,
            CancellationToken cancellation = default)
        {
            // For now, return empty array as Flash doesn't provide a direct invoice listing API
            // In the future, this could be implemented by querying transaction history
            return Task.FromResult(Array.Empty<LightningInvoice>());
        }

        public Task CancelInvoiceAsync(string invoiceId, CancellationToken cancellation = default)
        {
            throw new NotImplementedException("Invoice cancellation is not supported by Flash API");
        }

        public void TrackPendingInvoice(LightningInvoice invoice)
        {
            if (invoice != null && !string.IsNullOrEmpty(invoice.Id))
            {
                _logger.LogInformation("Starting to track pending invoice: {InvoiceId}", invoice.Id);
                _logger.LogInformation("Invoice details - Status: {Status}, Amount: {Amount}, PaymentHash: {PaymentHash}",
                    invoice.Status, invoice.Amount?.ToString() ?? "unknown", invoice.PaymentHash ?? "unknown");

                // Thread-safe access to shared static dictionaries
                lock (_invoiceTrackingLock)
                {
                    // Store a copy of the invoice
                    _pendingInvoices[invoice.Id] = invoice;
                    _invoiceCreationTimes[invoice.Id] = DateTime.UtcNow;

                    // Register it for tracking in the PollInvoices method
                    _logger.LogInformation("Added invoice {InvoiceId} to pending invoices dictionary (now contains {Count} invoices)",
                        invoice.Id, _pendingInvoices.Count);

                    // List all tracked invoice IDs for debugging
                    _logger.LogInformation("Currently tracking invoices: {InvoiceIds}", string.Join(", ", _pendingInvoices.Keys));
                }
            }
            else
            {
                _logger.LogWarning("Attempted to track null invoice or invoice with null ID");
            }
        }

        public async Task MarkInvoiceAsPaidAsync(string paymentHash, long amountSats, string? boltcardId = null)
        {
            try
            {
                // Update our internal tracking with thread safety
                LightningInvoice? paidInvoice = null;
                lock (_invoiceTrackingLock)
                {
                    if (_pendingInvoices.TryGetValue(paymentHash, out var invoice))
                    {
                        paidInvoice = new LightningInvoice
                        {
                            Id = invoice.Id,
                            PaymentHash = invoice.PaymentHash,
                            BOLT11 = invoice.BOLT11,
                            Status = LightningInvoiceStatus.Paid,
                            Amount = invoice.Amount,
                            AmountReceived = LightMoney.Satoshis(amountSats),
                            ExpiresAt = invoice.ExpiresAt,
                            PaidAt = DateTimeOffset.UtcNow
                        };

                        _pendingInvoices[paymentHash] = paidInvoice;
                        _logger.LogInformation("Marked invoice as paid: {PaymentHash}", paymentHash);
                    }
                }

                // 🎯 CRITICAL: Notify BTCPay Server's Lightning listener that invoice was paid
                // This is what actually credits the Boltcard!
                System.Threading.Channels.Channel<LightningInvoice>? listener = null;
                lock (_invoiceTrackingLock)
                {
                    listener = _currentInvoiceListener;
                }

                if (paidInvoice != null && listener != null)
                {
                    _logger.LogInformation("NOTIFYING BTCPAY SERVER: Invoice {PaymentHash} paid for {AmountSats} sats - This should credit the Boltcard!",
                        paymentHash, amountSats);

                    try
                    {
                        var notified = listener.Writer.TryWrite(paidInvoice);
                        if (notified)
                        {
                            _logger.LogInformation("SUCCESS: BTCPay Server notified about paid invoice {PaymentHash} - Boltcard should be credited!", paymentHash);
                        }
                        else
                        {
                            _logger.LogError("FAILED: Could not notify BTCPay Server about paid invoice {PaymentHash} - Boltcard will NOT be credited!", paymentHash);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ERROR: Failed to notify BTCPay Server about paid invoice {PaymentHash} - Boltcard will NOT be credited!", paymentHash);
                    }
                }
                else
                {
                    _logger.LogWarning("MISSING: No invoice listener available to notify BTCPay Server - Boltcard will NOT be credited! (paidInvoice: {HasInvoice}, listener: {HasListener})",
                        paidInvoice != null, listener != null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking invoice as paid: {PaymentHash}", paymentHash);
            }
        }

        /// <summary>
        /// Set the current invoice listener channel for notifications
        /// </summary>
        public static void SetInvoiceListener(Channel<LightningInvoice> channel)
        {
            lock (_invoiceTrackingLock)
            {
                _currentInvoiceListener = channel;
            }
        }

        /// <summary>
        /// Get pending invoices for monitoring
        /// </summary>
        public static Dictionary<string, LightningInvoice> GetPendingInvoices()
        {
            lock (_invoiceTrackingLock)
            {
                return new Dictionary<string, LightningInvoice>(_pendingInvoices);
            }
        }

        #region Private Helper Methods

        private string ProcessMemoForFlashApi(string memo)
        {
            // Simplify memo format for Flash API compatibility
            if (memo.StartsWith("[") && memo.Contains("Boltcard"))
            {
                memo = "Boltcard Top-Up";
                _logger.LogInformation("Simplified memo format for Flash API: '{Memo}'", memo);
            }
            else if (memo.Contains("[[") || memo.Contains("]]"))
            {
                // Handle any JSON array format by extracting plain text
                try
                {
                    var jsonArray = JsonConvert.DeserializeObject<string[][]>(memo);
                    if (jsonArray?.Length > 0 && jsonArray[0]?.Length > 1)
                    {
                        memo = jsonArray[0][1];
                        _logger.LogInformation("Extracted memo from JSON: '{Memo}'", memo);
                    }
                }
                catch
                {
                    // If JSON parsing fails, just use the original memo
                    _logger.LogInformation("Using original memo: '{Memo}'", memo);
                }
            }

            return memo;
        }

        private async Task<LightningInvoice> CreateFlashInvoice(
            WalletInfo walletInfo,
            long amountSats,
            string memo,
            CancellationToken cancellation)
        {
            if (walletInfo.Currency == "USD")
            {
                // Convert satoshis to USD cents
                decimal amountUsdCents = await _exchangeRateService.ConvertSatoshisToUsdCentsAsync(amountSats, cancellation);

                // Round to whole cents for Flash API compatibility
                amountUsdCents = Math.Round(amountUsdCents, 0, MidpointRounding.AwayFromZero);

                _logger.LogInformation("Converting {AmountSats} sats to {AmountUsdCents} USD cents for invoice creation using current exchange rate",
                    amountSats, amountUsdCents);

                // Ensure amount is an integer for Flash API and meets minimum requirements
                int wholeAmountCents = (int)Math.Round(amountUsdCents, 0, MidpointRounding.AwayFromZero);

                // Flash requires minimum 1 USD cent ($0.01)
                if (wholeAmountCents < FLASH_MINIMUM_CENTS)
                {
                    _logger.LogWarning("Amount {AmountCents} cents (${AmountDollars:F2}) is below Flash minimum of {MinCents} cent(s) (${MinDollars:F2}). Adjusting to minimum.", 
                        wholeAmountCents, wholeAmountCents / 100.0, FLASH_MINIMUM_CENTS, FLASH_MINIMUM_CENTS / 100.0);
                    wholeAmountCents = FLASH_MINIMUM_CENTS;
                }

                var query = @"
                mutation lnUsdInvoiceCreate($input: LnUsdInvoiceCreateInput!) {
                  lnUsdInvoiceCreate(input: $input) {
                    invoice {
                      paymentHash
                      paymentRequest
                      paymentSecret
                      satoshis
                    }
                    errors {
                      message
                    }
                  }
                }";

                var variables = new
                {
                    input = new
                    {
                        amount = wholeAmountCents,
                        memo = memo,
                        walletId = walletInfo.Id
                    }
                };

                var mutation = new GraphQLRequest
                {
                    Query = query,
                    OperationName = "lnUsdInvoiceCreate",
                    Variables = variables
                };

                _logger.LogInformation("Using amount: {AmountCents} cents (${AmountDollars:F2}) for Flash API",
                    wholeAmountCents, wholeAmountCents / 100.0);

                var response = await _graphQLService.SendMutationAsync<UsdInvoiceResponse>(mutation, cancellation);

                return ProcessInvoiceCreationResponse(response, amountSats);
            }
            else if (walletInfo.Currency == "BTC")
            {
                throw new Exception("Flash does not support BTC wallets for lightning invoices.");
            }
            else
            {
                throw new Exception($"Unsupported wallet currency: {walletInfo.Currency}");
            }
        }

        private LightningInvoice ProcessInvoiceCreationResponse(GraphQLResponse<UsdInvoiceResponse> response, long amountSats)
        {
            // Enhanced error logging for debugging
            _logger.LogInformation("Flash API response received. Has errors: {HasErrors}, Has data: {HasData}",
                response.Errors?.Length > 0, response.Data != null);

            if (response.Errors != null && response.Errors.Length > 0)
            {
                string errorMessage = string.Join(", ", response.Errors.Select(e => e.Message));
                _logger.LogError("GraphQL errors: {ErrorMessage}", errorMessage);
                throw new Exception($"Failed to create invoice: {errorMessage}");
            }

            if (response.Data?.lnUsdInvoiceCreate?.errors != null && response.Data.lnUsdInvoiceCreate.errors.Any())
            {
                string errorMessage = string.Join(", ", response.Data.lnUsdInvoiceCreate.errors.Select(e => e.message));
                _logger.LogError("Flash business logic errors: {ErrorMessage}", errorMessage);

                // Check for specific error patterns that might help us understand the issue
                if (errorMessage.ToLowerInvariant().Contains("minimum") || errorMessage.ToLowerInvariant().Contains("amount"))
                {
                    _logger.LogError("This appears to be an amount-related error. Consider increasing minimum amounts.");
                }

                throw new Exception($"Failed to create invoice: {errorMessage}");
            }

            var invoice = response.Data?.lnUsdInvoiceCreate?.invoice;
            if (invoice == null)
            {
                _logger.LogError("Response contained no invoice data");
                throw new Exception("Failed to create invoice: No invoice data returned");
            }

            _logger.LogInformation("Successfully created invoice with hash: {PaymentHash}", invoice.paymentHash);

            var lightningInvoice = new LightningInvoice
            {
                Id = invoice.paymentHash,
                PaymentHash = invoice.paymentHash, // Ensure PaymentHash is set
                BOLT11 = invoice.paymentRequest,
                Amount = new LightMoney(invoice.satoshis ?? amountSats, LightMoneyUnit.Satoshi),
                ExpiresAt = DateTime.UtcNow.AddHours(24), // Default expiry
                Status = LightningInvoiceStatus.Unpaid,
                AmountReceived = LightMoney.Zero,
            };

            // CRITICAL DEBUG: Log the actual BOLT11 that should be paid
            _logger.LogInformation("*** IMPORTANT *** Flash invoice BOLT11 to pay: {BOLT11}", invoice.paymentRequest);
            _logger.LogInformation("*** IMPORTANT *** This BOLT11 should be used for QR code, paying this credits Flash wallet {WalletId}",
                "wallet-id-placeholder");

            return lightningInvoice;
        }

        private LightningInvoice CreateInvoiceFromTransaction(string invoiceId, TransactionInfo transaction)
        {
            var amount = transaction.SettlementAmount != null
                ? new LightMoney(Math.Abs(transaction.SettlementAmount.Value), LightMoneyUnit.Satoshi)
                : LightMoney.Satoshis(0);

            var status = transaction.Status?.ToLowerInvariant() switch
            {
                "success" => LightningInvoiceStatus.Paid,
                "complete" => LightningInvoiceStatus.Paid,
                "pending" => LightningInvoiceStatus.Unpaid,
                "expired" => LightningInvoiceStatus.Expired,
                "cancelled" => LightningInvoiceStatus.Expired,
                _ => LightningInvoiceStatus.Unpaid
            };

            var invoice = new LightningInvoice
            {
                Id = invoiceId,
                PaymentHash = invoiceId, // Use invoiceId as PaymentHash
                Status = status,
                Amount = amount,
                ExpiresAt = transaction.CreatedAt.AddDays(1)
            };

            // Set AmountReceived if the invoice is paid
            if (status == LightningInvoiceStatus.Paid)
            {
                _logger.LogInformation("Setting AmountReceived for paid invoice: {InvoiceId}", invoiceId);
                invoice.AmountReceived = amount;
            }

            return invoice;
        }

        private LightningInvoice CreateDefaultUnpaidInvoice(string invoiceId)
        {
            // Return unpaid status as default when we can't determine status
            return new LightningInvoice
            {
                Id = invoiceId,
                PaymentHash = invoiceId,
                Status = LightningInvoiceStatus.Unpaid,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            };
        }

        private void CleanupOldPendingInvoices()
        {
            var now = DateTime.UtcNow;

            // Get keys to remove with thread safety
            List<string> keysToRemove;
            lock (_invoiceTrackingLock)
            {
                keysToRemove = _invoiceCreationTimes
                    .Where(kvp => (now - kvp.Value).TotalHours > 24)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }

            // Remove old invoices with thread safety
            if (keysToRemove.Count > 0)
            {
                lock (_invoiceTrackingLock)
                {
                    foreach (var key in keysToRemove)
                    {
                        _invoiceCreationTimes.Remove(key);
                        _pendingInvoices.Remove(key);
                    }
                }

                _logger.LogInformation("Cleaned up {Count} old pending invoices", keysToRemove.Count);
            }
        }

        #endregion

        #region Response Classes

        private class UsdInvoiceResponse
        {
            public InvoiceCreateData lnUsdInvoiceCreate { get; set; } = null!;

            public class InvoiceCreateData
            {
                public List<ErrorData>? errors { get; set; }
                public InvoiceData? invoice { get; set; }
            }

            public class ErrorData
            {
                public string message { get; set; } = null!;
            }

            public class InvoiceData
            {
                public string paymentHash { get; set; } = null!;
                public string paymentRequest { get; set; } = null!;
                public string paymentSecret { get; set; } = null!;
                public long? satoshis { get; set; }
            }
        }

        #endregion
    }
}