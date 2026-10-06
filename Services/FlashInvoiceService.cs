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
            var createParams = new CreateInvoiceParams(amount, description, expiry);
            return await CreateInvoiceAsync(createParams, cancellation);
        }

        public async Task<LightningInvoice> GetInvoiceAsync(
            string invoiceId,
            CancellationToken cancellation = default)
        {
            CleanupOldPendingInvoices();

            var verified = await GetVerifiedInvoiceAsync(invoiceId, cancellation);
            if (verified != null)
                return verified;

            // Without the invoice's payment request we cannot ask Flash about it, and guessing
            // from recent transactions can credit the wrong invoice. Report it as unpaid.
            LightningInvoice? cached;
            lock (_invoiceTrackingLock)
            {
                _pendingInvoices.TryGetValue(invoiceId, out cached);
            }

            if (cached == null)
            {
                _logger.LogInformation("[INVOICE STATUS] Invoice {InvoiceId} was not created by this plugin instance; reporting unpaid", invoiceId);
                return CreateDefaultUnpaidInvoice(invoiceId);
            }

            return CopyInvoice(cached, cached.Status == LightningInvoiceStatus.Paid ? LightningInvoiceStatus.Unpaid : cached.Status, null);
        }

        public async Task<LightningInvoice?> GetVerifiedInvoiceAsync(string invoiceId, CancellationToken cancellation = default)
        {
            LightningInvoice? cached;
            lock (_invoiceTrackingLock)
            {
                _pendingInvoices.TryGetValue(invoiceId, out cached);
            }

            if (cached == null || string.IsNullOrEmpty(cached.BOLT11))
                return null;

            var status = await _graphQLService.GetInvoiceStatusAsync(cached.BOLT11, cancellation);
            if (status == null)
                return null;

            LightningInvoice result;
            switch (status.Status.ToUpperInvariant())
            {
                case "PAID":
                    var received = GetBolt11Amount(cached.BOLT11) ?? cached.Amount;
                    result = CopyInvoice(cached, LightningInvoiceStatus.Paid, received);
                    result.PaidAt = cached.PaidAt ?? DateTimeOffset.UtcNow;
                    break;
                case "EXPIRED":
                    result = CopyInvoice(cached, LightningInvoiceStatus.Expired, null);
                    break;
                default:
                    result = CopyInvoice(cached, LightningInvoiceStatus.Unpaid, null);
                    break;
            }

            if (result.Status != cached.Status)
            {
                _logger.LogInformation("[INVOICE STATUS] Invoice {InvoiceId} is {Status} according to Flash (amount received: {Received})",
                    invoiceId, result.Status, result.AmountReceived?.ToString() ?? "-");
                lock (_invoiceTrackingLock)
                {
                    _pendingInvoices[invoiceId] = result;
                }
            }

            return result;
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
            // Callers detect "probably paid" from transaction history or outgoing payments, which
            // cannot identify the invoice. Only record the payment once Flash confirms this invoice.
            try
            {
                var verified = await GetVerifiedInvoiceAsync(paymentHash, CancellationToken.None);
                if (verified?.Status == LightningInvoiceStatus.Paid)
                {
                    _logger.LogInformation("Invoice {PaymentHash} confirmed paid by Flash ({Received})", paymentHash, verified.AmountReceived);
                }
                else
                {
                    _logger.LogInformation("Ignoring unconfirmed paid signal for invoice {PaymentHash} (Flash status: {Status})",
                        paymentHash, verified?.Status.ToString() ?? "unknown");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming invoice payment: {PaymentHash}", paymentHash);
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
                // Round up so the invoice is never worth less than BTCPay asked for; rounding down
                // would leave the BTCPay invoice underpaid and never settled.
                amountUsdCents = Math.Ceiling(amountUsdCents);

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

        private static LightningInvoice CopyInvoice(LightningInvoice source, LightningInvoiceStatus status, LightMoney? amountReceived)
        {
            return new LightningInvoice
            {
                Id = source.Id,
                PaymentHash = source.PaymentHash ?? source.Id,
                BOLT11 = source.BOLT11,
                Amount = source.Amount,
                ExpiresAt = source.ExpiresAt,
                Status = status,
                AmountReceived = amountReceived ?? LightMoney.Zero,
                PaidAt = status == LightningInvoiceStatus.Paid ? source.PaidAt : null
            };
        }

        private LightMoney? GetBolt11Amount(string bolt11)
        {
            if (Bolt11.TryParse(bolt11, out _, out var amount))
                return amount;

            _logger.LogWarning("Could not read the amount from invoice {Bolt11Prefix}...", bolt11.Substring(0, Math.Min(20, bolt11.Length)));
            return null;
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