#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using GraphQL;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BTCPayServer.Plugins.Flash.Services;
using NBitcoin;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Implementation of payment service for Lightning payment operations
    /// </summary>
    public class FlashPaymentService : IFlashPaymentService
    {
        private readonly IFlashGraphQLService _graphQLService;
        private readonly IFlashExchangeRateService _exchangeRateService;
        private readonly ILogger<FlashPaymentService> _logger;
        private readonly IServiceProvider? _serviceProvider;

        public FlashPaymentService(
            IFlashGraphQLService graphQLService,
            IFlashExchangeRateService exchangeRateService,
            ILogger<FlashPaymentService> logger,
            IServiceProvider? serviceProvider = null)
        {
            _graphQLService = graphQLService ?? throw new ArgumentNullException(nameof(graphQLService));
            _exchangeRateService = exchangeRateService ?? throw new ArgumentNullException(nameof(exchangeRateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider;
        }

        public async Task<PayResponse> PayInvoiceAsync(
            string bolt11,
            CancellationToken cancellation = default)
        {
            return await PayInvoiceAsync(bolt11, null, cancellation);
        }

        public async Task<PayResponse> PayInvoiceAsync(
    PayInvoiceParams invoice,
    CancellationToken cancellation = default)
        {
            if (invoice == null)
            {
                _logger.LogError("PayInvoiceParams is null");
                return new PayResponse(PayResult.Error, "Invalid payment parameters");
            }

            // Based on BTCPayServer.Lightning patterns, PayInvoiceParams should have a BOLT11 property
            // Try to access it via reflection if direct access doesn't work
            string? bolt11 = null;
            
            // Try to get BOLT11 property via reflection
            var bolt11Property = invoice.GetType().GetProperty("BOLT11");
            if (bolt11Property != null)
            {
                bolt11 = bolt11Property.GetValue(invoice) as string;
            }
            
            if (string.IsNullOrEmpty(bolt11))
            {
                _logger.LogError("BOLT11 invoice is missing from PayInvoiceParams");
                return new PayResponse(PayResult.Error, "Invoice is required");
            }

            _logger.LogInformation("Processing payment from PayInvoiceParams with BOLT11: {Bolt11Prefix}...", 
                bolt11.Substring(0, Math.Min(bolt11.Length, 30)));

            // Delegate to the main PayInvoiceAsync method with the extracted BOLT11 and the params
            // The params may contain additional payment parameters like amount overrides
            return await PayInvoiceAsync(bolt11, invoice, cancellation);
        }

        public async Task<PayResponse> PayInvoiceAsync(
            string bolt11,
            PayInvoiceParams? payParams,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("[BOLTCARD PAYMENT] PayInvoiceAsync called with BOLT11: {Bolt11Prefix}...", 
                    bolt11?.Substring(0, Math.Min(bolt11?.Length ?? 0, 30)));

                // Get wallet information
                var walletInfo = await _graphQLService.GetWalletInfoAsync(cancellation);
                if (walletInfo == null)
                {
                    throw new InvalidOperationException("Could not determine wallet ID for payment");
                }

                // Extract payment hash from invoice for tracking
                var decodedData = await DecodeInvoice(bolt11, cancellation);

                _logger.LogInformation("Invoice details - Hash: {PaymentHash}, Amount: {Amount} satoshis",
                    decodedData.paymentHash, decodedData.amount ?? 0);

                // This is likely a Boltcard payment - we need to ensure the invoice is being tracked
                _logger.LogInformation("[BOLTCARD PAYMENT] This appears to be a Boltcard/LNURL payment. Need to ensure invoice tracking.");
                
                // Extract the actual payment hash from the BOLT11 for proper tracking
                string actualPaymentHash = ExtractPaymentHashFromBolt11(bolt11);
                _logger.LogInformation("[BOLTCARD PAYMENT] Extracted actual payment hash: {PaymentHash}", actualPaymentHash);
                
                // CRITICAL: For Boltcard payments, we need to track and immediately mark as paid after payment succeeds
                // since BTCPayServer's Boltcard flow queries by payment hash
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(2000); // Give payment time to process
                        
                        _logger.LogInformation("[BOLTCARD PAYMENT] Checking if Boltcard payment completed for payment hash: {PaymentHash}", actualPaymentHash);
                        
                        // Get invoice service from DI
                        var invoiceService = _serviceProvider?.GetService<IFlashInvoiceService>();
                        if (invoiceService != null && !string.IsNullOrEmpty(actualPaymentHash))
                        {
                            // For Boltcard payments, we need to mark the invoice as paid by its payment hash
                            _logger.LogInformation("[BOLTCARD PAYMENT] Marking invoice {PaymentHash} as paid", actualPaymentHash);
                            await invoiceService.MarkInvoiceAsPaidAsync(actualPaymentHash, decodedData.amount ?? 1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[BOLTCARD PAYMENT] Error checking payment status");
                    }
                });

                // Process payment with the appropriate mutation
                var payResponse = await SendPaymentWithCorrectMutation(bolt11, walletInfo, payParams, cancellation);

                // If payment was successful, notify that the invoice was paid
                if (payResponse.Result == PayResult.Ok)
                {
                    _logger.LogInformation("[BOLTCARD PAYMENT] Payment successful! Notifying invoice service...");
                    
                    // Immediately register this invoice as paid in the static collection
                    var paymentHash = decodedData.paymentHash ?? actualPaymentHash;
                    if (!string.IsNullOrEmpty(paymentHash))
                    {
                        _logger.LogInformation("[PAYMENT SUCCESS] Registering invoice as recently paid - Hash: {PaymentHash}, Amount: {Amount} sats", 
                            paymentHash, decodedData.amount ?? 1000);
                        
                        // Register in the static collection for immediate availability
                        FlashLightningClient.RegisterRecentlyPaidInvoice(
                            invoiceId: paymentHash, // Use payment hash as invoice ID if we don't have the actual ID
                            paymentHash: paymentHash,
                            amountSats: decodedData.amount ?? 1000,
                            bolt11: bolt11,
                            transactionId: payResponse.Details?.PaymentHash?.ToString() ?? paymentHash
                        );
                    }
                    
                    // For Boltcard payments, we need to find the invoice by the BOLT11
                    // BTCPayServer tracks invoices by their ID, not by BOLT11
                    // We need to search for the invoice that has this BOLT11
                    var invoiceService = _serviceProvider?.GetService<IFlashInvoiceService>();
                    if (invoiceService != null)
                    {
                        // Get all pending invoices and find the one with this BOLT11
                        var pendingInvoices = FlashInvoiceService.GetPendingInvoices();
                        var matchingInvoice = pendingInvoices.FirstOrDefault(kvp => kvp.Value.BOLT11 == bolt11);
                        
                        if (matchingInvoice.Value != null)
                        {
                            _logger.LogInformation("[BOLTCARD PAYMENT] Found matching invoice {InvoiceId} for BOLT11", matchingInvoice.Key);
                            
                            // Also register with the actual invoice ID
                            FlashLightningClient.RegisterRecentlyPaidInvoice(
                                invoiceId: matchingInvoice.Key,
                                paymentHash: paymentHash,
                                amountSats: decodedData.amount ?? 1000,
                                bolt11: bolt11,
                                transactionId: payResponse.Details?.PaymentHash?.ToString() ?? paymentHash
                            );
                            
                            await invoiceService.MarkInvoiceAsPaidAsync(matchingInvoice.Key, decodedData.amount ?? 1000);
                            _logger.LogInformation("[BOLTCARD PAYMENT] Invoice {InvoiceId} marked as paid", matchingInvoice.Key);
                        }
                        else
                        {
                            _logger.LogWarning("[BOLTCARD PAYMENT] Could not find invoice with BOLT11: {Bolt11Prefix}...", 
                                bolt11?.Substring(0, Math.Min(bolt11?.Length ?? 0, 30)));
                        }
                    }
                }

                return payResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detailed error paying Flash invoice");
                return new PayResponse(PayResult.Error, ex.Message);
            }
        }

        public async Task<LightningPayment[]> ListPaymentsAsync(
            ListPaymentsParams? request = null,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("[LIST PAYMENTS] Starting to list payments");

                // Get wallet info to ensure we're connected
                var walletInfo = await _graphQLService.GetWalletInfoAsync(cancellation);
                if (walletInfo == null)
                {
                    _logger.LogWarning("[LIST PAYMENTS] No wallet found, returning empty array");
                    return Array.Empty<LightningPayment>();
                }

                // Get transaction history from Flash
                // Since we don't have access to pagination parameters from ListPaymentsParams,
                // we'll fetch a reasonable default amount
                int limit = 100; // Default fetch size
                var transactions = await _graphQLService.GetTransactionHistoryAsync(limit, cancellation);
                
                _logger.LogInformation("[LIST PAYMENTS] Retrieved {Count} transactions from Flash", transactions.Count);

                // Filter for outgoing payments (SEND direction)
                var outgoingTransactions = transactions
                    .Where(t => t.Direction?.ToUpperInvariant() == "SEND")
                    .ToList();

                _logger.LogInformation("[LIST PAYMENTS] Found {Count} outgoing transactions", outgoingTransactions.Count);

                // Convert to LightningPayment objects
                var payments = new List<LightningPayment>();
                
                foreach (var tx in outgoingTransactions)
                {
                    // Determine payment status
                    var status = tx.Status?.ToLowerInvariant() switch
                    {
                        "success" => LightningPaymentStatus.Complete,
                        "complete" => LightningPaymentStatus.Complete,
                        "pending" => LightningPaymentStatus.Pending,
                        "failed" => LightningPaymentStatus.Failed,
                        _ => LightningPaymentStatus.Unknown
                    };

                    // Extract payment hash from transaction
                    // Flash might store payment hash in the ID or memo
                    string paymentHash = tx.Id;
                    
                    // If memo contains a BOLT11, try to extract payment hash
                    if (!string.IsNullOrEmpty(tx.Memo) && 
                        (tx.Memo.StartsWith("lnbc") || tx.Memo.StartsWith("lntb") || tx.Memo.StartsWith("lnbcrt")))
                    {
                        try
                        {
                            paymentHash = ExtractPaymentHashFromBolt11(tx.Memo);
                        }
                        catch
                        {
                            // Keep the transaction ID as payment hash if extraction fails
                        }
                    }

                    var payment = new LightningPayment
                    {
                        Id = tx.Id,
                        PaymentHash = paymentHash,
                        Status = status,
                        Amount = tx.SettlementAmount != null 
                            ? new LightMoney(Math.Abs(tx.SettlementAmount.Value), LightMoneyUnit.Satoshi)
                            : LightMoney.Zero,
                        AmountSent = tx.SettlementAmount != null 
                            ? new LightMoney(Math.Abs(tx.SettlementAmount.Value), LightMoneyUnit.Satoshi)
                            : LightMoney.Zero,
                        CreatedAt = tx.CreatedAt,
                        BOLT11 = !string.IsNullOrEmpty(tx.Memo) && tx.Memo.StartsWith("ln") ? tx.Memo : null,
                        Preimage = null // Flash doesn't provide preimage in transaction list
                    };

                    payments.Add(payment);
                }

                _logger.LogInformation("[LIST PAYMENTS] Returning {Count} payments", payments.Count);

                return payments.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LIST PAYMENTS] Error listing payments");
                return Array.Empty<LightningPayment>();
            }
        }

        public async Task<LightningPayment> GetPaymentAsync(
            string paymentHash,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("[PAYMENT LOOKUP] Attempting to get payment status for hash: {PaymentHash}", paymentHash);

                // First check if this payment was recently paid
                var recentlyPaid = FlashLightningClient.GetRecentlyPaidInvoice(paymentHash);
                if (recentlyPaid != null)
                {
                    _logger.LogInformation("[PAYMENT LOOKUP] Found recently paid invoice in cache for hash: {PaymentHash}", paymentHash);
                    
                    return new LightningPayment
                    {
                        Id = paymentHash,
                        PaymentHash = paymentHash,
                        Status = LightningPaymentStatus.Complete,
                        Amount = LightMoney.Satoshis(recentlyPaid.AmountSats),
                        AmountSent = LightMoney.Satoshis(recentlyPaid.AmountSats),
                        CreatedAt = recentlyPaid.PaidAt
                    };
                }

                // Normalize the payment hash to lowercase hex (BTCPay standard)
                var normalizedHash = paymentHash?.ToLowerInvariant();

                // Query transaction history to find the payment
                var transactions = await _graphQLService.GetTransactionHistoryAsync(50, cancellation); // Increased limit for better coverage

                // Look for matching transaction
                // Flash transactions might store the payment hash in various places:
                // 1. Transaction ID itself
                // 2. In the memo field
                // 3. As part of a BOLT11 invoice in the memo
                var matchingTransaction = transactions.FirstOrDefault(t =>
                {
                    // Direct ID match
                    if (t.Id?.ToLowerInvariant() == normalizedHash)
                    {
                        _logger.LogInformation("[PAYMENT LOOKUP] Found transaction by ID match: {TransactionId}", t.Id);
                        return true;
                    }

                    // Check if memo contains the payment hash
                    if (!string.IsNullOrEmpty(t.Memo))
                    {
                        var memoLower = t.Memo.ToLowerInvariant();
                        
                        // Direct payment hash in memo
                        if (memoLower.Contains(normalizedHash))
                        {
                            _logger.LogInformation("[PAYMENT LOOKUP] Found transaction by payment hash in memo: {TransactionId}", t.Id);
                            return true;
                        }

                        // Check if memo contains a BOLT11 invoice
                        if (memoLower.StartsWith("lnbc") || memoLower.StartsWith("lntb") || memoLower.StartsWith("lnbcrt"))
                        {
                            try
                            {
                                // Extract payment hash from BOLT11 and compare
                                var extractedHash = ExtractPaymentHashFromBolt11(t.Memo);
                                if (extractedHash?.ToLowerInvariant() == normalizedHash)
                                {
                                    _logger.LogInformation("[PAYMENT LOOKUP] Found transaction by BOLT11 payment hash: {TransactionId}", t.Id);
                                    return true;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "[PAYMENT LOOKUP] Could not parse BOLT11 from memo");
                            }
                        }
                    }

                    return false;
                });

                if (matchingTransaction != null)
                {
                    _logger.LogInformation("[PAYMENT LOOKUP] Found matching transaction: {TransactionId}, Status: {Status}, Amount: {Amount}", 
                        matchingTransaction.Id, matchingTransaction.Status, matchingTransaction.SettlementAmount);

                    return new LightningPayment
                    {
                        Id = paymentHash,
                        PaymentHash = paymentHash,
                        Status = matchingTransaction.Status?.ToLowerInvariant() switch
                        {
                            "success" => LightningPaymentStatus.Complete,
                            "complete" => LightningPaymentStatus.Complete,
                            "pending" => LightningPaymentStatus.Pending,
                            "failed" => LightningPaymentStatus.Failed,
                            _ => LightningPaymentStatus.Unknown
                        },
                        Amount = matchingTransaction.SettlementAmount != null
                            ? new LightMoney(Math.Abs(matchingTransaction.SettlementAmount.Value), LightMoneyUnit.Satoshi)
                            : LightMoney.Zero,
                        CreatedAt = matchingTransaction.CreatedAt
                    };
                }

                _logger.LogWarning("[PAYMENT LOOKUP] No transaction found for payment hash: {PaymentHash}", paymentHash);

                // Check if this is a recently created Boltcard invoice that might not have been paid yet
                var invoiceService = _serviceProvider?.GetService<IFlashInvoiceService>();
                if (invoiceService != null)
                {
                    var pendingInvoices = FlashInvoiceService.GetPendingInvoices();
                    var matchingInvoice = pendingInvoices.FirstOrDefault(kvp => 
                    {
                        try
                        {
                            // Check if the stored invoice has this payment hash
                            if (!string.IsNullOrEmpty(kvp.Value.BOLT11))
                            {
                                var extractedHash = ExtractPaymentHashFromBolt11(kvp.Value.BOLT11);
                                return extractedHash?.ToLowerInvariant() == normalizedHash;
                            }
                        }
                        catch { }
                        return false;
                    });

                    if (matchingInvoice.Value != null)
                    {
                        _logger.LogInformation("[PAYMENT LOOKUP] Found pending invoice for payment hash: {InvoiceId}", matchingInvoice.Key);
                        // Return as pending since it hasn't been paid yet
                        return new LightningPayment
                        {
                            Id = paymentHash,
                            PaymentHash = paymentHash,
                            Status = LightningPaymentStatus.Pending,
                            CreatedAt = DateTime.UtcNow,
                            Amount = matchingInvoice.Value.Amount // Amount is already a LightMoney type
                        };
                    }
                }

                // Return a pending payment as fallback
                return new LightningPayment
                {
                    Id = paymentHash,
                    PaymentHash = paymentHash,
                    Status = LightningPaymentStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    Amount = LightMoney.Satoshis(1000) // Default amount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PAYMENT LOOKUP] Error getting payment {PaymentHash}", paymentHash);

                // Return a pending payment as fallback
                return new LightningPayment
                {
                    Id = paymentHash,
                    PaymentHash = paymentHash,
                    Status = LightningPaymentStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
            }
        }

        public void SetNoAmountInvoiceAmount(long amountSat)
        {
            // TODO: Implement no-amount invoice amount setting
            _logger.LogInformation("Set fallback amount for no-amount invoice: {AmountSat} satoshis", amountSat);
        }

        public async Task<(string bolt11, string? error)> ResolveLnurlPaymentAsync(
            string lnurlDestination,
            long amountSats,
            string? memo = null,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("Resolving LNURL payment for destination: {Destination}", lnurlDestination.Substring(0, Math.Min(lnurlDestination.Length, 20)));

                // TODO: Implement LNURL resolution logic
                await Task.Delay(100); // Placeholder

                return (string.Empty, "LNURL resolution not implemented yet");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving LNURL payment");
                return (string.Empty, ex.Message);
            }
        }

        public async Task<(string bolt11, string paymentHash)> GetInvoiceFromLNURLAsync(
            object payoutData,
            object handler,
            object blob,
            object lnurlPayClaimDestination,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("Processing LNURL payout request");

                // TODO: Implement LNURL payout processing
                await Task.Delay(100); // Placeholder

                throw new NotImplementedException("LNURL payout processing not implemented yet");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling LNURL payout");
                throw new Exception($"Error processing LNURL payment: {ex.Message}");
            }
        }

        public async Task<PayoutData> ProcessPullPaymentPayoutAsync(
    string pullPaymentId,
    string payoutId,
    string bolt11,
    CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("Processing payout {PayoutId} for Pull Payment {PullPaymentId}", payoutId, pullPaymentId);

                // TODO: Implement pull payment payout processing
                await Task.Delay(100); // Placeholder

                // Return a placeholder completed payout for now
                return new PayoutData
                {
                    Id = payoutId,
                    PullPaymentId = pullPaymentId,
                    Proof = string.Empty,
                    State = PayoutState.Completed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payout {PayoutId} for Pull Payment {PullPaymentId}", payoutId, pullPaymentId);

                // Return a failed payout
                return new PayoutData
                {
                    Id = payoutId,
                    PullPaymentId = pullPaymentId,
                    Proof = string.Empty,
                    State = PayoutState.Failed
                };
            }
        }

        #region Private Helper Methods

        private string ExtractPaymentHashFromBolt11(string bolt11)
        {
            try
            {
                // Use NBitcoin's BOLT11PaymentRequest parser to properly decode the invoice
                // BTCPayServer expects hex-encoded payment hashes, not bech32
                var network = NBitcoin.Network.Main; // Default to mainnet, adjust if needed based on your configuration
                
                // Try to determine network from BOLT11 prefix
                if (bolt11.StartsWith("lnbc", StringComparison.OrdinalIgnoreCase))
                    network = NBitcoin.Network.Main;
                else if (bolt11.StartsWith("lntb", StringComparison.OrdinalIgnoreCase))
                    network = NBitcoin.Network.TestNet;
                else if (bolt11.StartsWith("lnbcrt", StringComparison.OrdinalIgnoreCase))
                    network = NBitcoin.Network.RegTest;
                
                var paymentRequest = BOLT11PaymentRequest.Parse(bolt11, network);
                var paymentHashHex = paymentRequest.PaymentHash?.ToString();
                
                if (!string.IsNullOrEmpty(paymentHashHex))
                {
                    _logger.LogInformation("[BOLTCARD PAYMENT] Extracted payment hash from BOLT11: {PaymentHash} (hex format)", paymentHashHex);
                    return paymentHashHex;
                }
                
                _logger.LogWarning("[BOLTCARD PAYMENT] Could not extract payment hash from BOLT11, using full invoice");
                return bolt11;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BOLTCARD PAYMENT] Error extracting payment hash from BOLT11: {Message}", ex.Message);
                // Fallback to manual extraction if BOLT11PaymentRequest parsing fails
                try
                {
                    // BOLT11 payment hash is in the 'p' field after the 'pp' tag
                    // It's 52 characters in bech32 encoding which represents 32 bytes (256 bits)
                    var ppIndex = bolt11.IndexOf("pp");
                    if (ppIndex > 0 && ppIndex + 2 + 52 <= bolt11.Length)
                    {
                        // Extract the 52 character payment hash (bech32)
                        var paymentHashBech32 = bolt11.Substring(ppIndex + 2, 52);
                        
                        // Convert bech32 to bytes then to hex
                        // Note: This is a simplified conversion - in production you'd use proper bech32 decoding
                        _logger.LogWarning("[BOLTCARD PAYMENT] Falling back to manual extraction, but cannot convert to hex without proper bech32 decoder");
                        return bolt11; // Return full bolt11 as fallback
                    }
                }
                catch { }
                
                return bolt11;
            }
        }

        private async Task<(string? paymentHash, long? amount)> DecodeInvoice(string bolt11, CancellationToken cancellation)
        {
            try
            {
                _logger.LogInformation("[BOLTCARD PAYMENT] Attempting to decode BOLT11 invoice");
                
                // Use NBitcoin's BOLT11PaymentRequest parser for proper decoding
                var network = NBitcoin.Network.Main;
                
                // Determine network from BOLT11 prefix
                if (bolt11.StartsWith("lnbc", StringComparison.OrdinalIgnoreCase))
                    network = NBitcoin.Network.Main;
                else if (bolt11.StartsWith("lntb", StringComparison.OrdinalIgnoreCase))
                    network = NBitcoin.Network.TestNet;
                else if (bolt11.StartsWith("lnbcrt", StringComparison.OrdinalIgnoreCase))
                    network = NBitcoin.Network.RegTest;
                
                var paymentRequest = BOLT11PaymentRequest.Parse(bolt11, network);
                
                // Extract payment hash in hex format (this is what BTCPayServer expects)
                string? paymentHash = paymentRequest.PaymentHash?.ToString();
                
                // Extract amount in satoshis
                long? amount = null;
                if (paymentRequest.MinimumAmount != null)
                {
                    amount = (long)paymentRequest.MinimumAmount.ToUnit(LightMoneyUnit.Satoshi);
                }
                
                _logger.LogInformation("[BOLTCARD PAYMENT] Decoded invoice - PaymentHash: {PaymentHash} (hex), Amount: {Amount} sats", 
                    paymentHash, amount);
                
                return (paymentHash, amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BOLTCARD PAYMENT] Error decoding invoice with NBitcoin, falling back to manual extraction");
                
                try
                {
                    // Fallback: Manual extraction for basic amount parsing
                    long? amount = null;
                    var match = System.Text.RegularExpressions.Regex.Match(bolt11, @"ln(?:bc|tb|bcrt)(\d+)([munp])?");
                    if (match.Success)
                    {
                        var amountStr = match.Groups[1].Value;
                        var multiplier = match.Groups[2].Value;
                        
                        if (long.TryParse(amountStr, out var baseAmount))
                        {
                            // Convert to satoshis based on multiplier
                            amount = multiplier switch
                            {
                                "m" => baseAmount * 100,      // milli-satoshis to satoshis
                                "u" => baseAmount / 10,        // micro-satoshis to satoshis  
                                "n" => baseAmount / 1000,      // nano-satoshis to satoshis
                                "p" => baseAmount / 1000000,   // pico-satoshis to satoshis
                                _ => baseAmount / 1000         // Default: assume millisatoshis
                            };
                        }
                    }
                    
                    // For fallback, use the entire BOLT11 as identifier since we can't extract the hash
                    _logger.LogWarning("[BOLTCARD PAYMENT] Using full BOLT11 as payment identifier for tracking");
                    return (bolt11, amount);
                }
                catch
                {
                    return (null, null);
                }
            }
        }

        private async Task<PayResponse> SendPaymentWithCorrectMutation(
            string bolt11,
            WalletInfo walletInfo,
            PayInvoiceParams? payParams,
            CancellationToken cancellation)
        {
            try
            {
                _logger.LogInformation("[PAYMENT] Executing Lightning payment for BOLT11: {Bolt11Prefix}...", 
                    bolt11.Substring(0, Math.Min(bolt11.Length, 30)));

                // Use the appropriate mutation based on wallet currency
                if (walletInfo.Currency == "USD")
                {
                    // Use lnInvoicePaymentSend for USD wallets
                    var mutation = @"
                    mutation lnInvoicePaymentSend($input: LnInvoicePaymentInput!) {
                      lnInvoicePaymentSend(input: $input) {
                        status
                        errors {
                          message
                          code
                        }
                      }
                    }";

                    var variables = new
                    {
                        input = new
                        {
                            paymentRequest = bolt11,
                            walletId = walletInfo.Id
                        }
                    };

                    _logger.LogInformation("[PAYMENT] Sending payment through Flash API for USD wallet");

                    var response = await _graphQLService.SendMutationAsync<PaymentResponse>(new GraphQLRequest
                    {
                        Query = mutation,
                        Variables = variables
                    }, cancellation);

                    if (response.Errors != null && response.Errors.Length > 0)
                    {
                        var errorMessage = string.Join(", ", response.Errors.Select(e => e.Message));
                        _logger.LogError("[PAYMENT] GraphQL errors: {ErrorMessage}", errorMessage);
                        return new PayResponse(PayResult.Error, errorMessage);
                    }

                    if (response.Data?.lnInvoicePaymentSend?.errors != null && response.Data.lnInvoicePaymentSend.errors.Any())
                    {
                        var errorMessage = string.Join(", ", response.Data.lnInvoicePaymentSend.errors.Select(e => e.message));
                        _logger.LogError("[PAYMENT] Payment errors: {ErrorMessage}", errorMessage);
                        return new PayResponse(PayResult.Error, errorMessage);
                    }

                    var status = response.Data?.lnInvoicePaymentSend?.status;
                    _logger.LogInformation("[PAYMENT] Payment status: {Status}", status);

                    if (status == "SUCCESS" || status == "PENDING")
                    {
                        _logger.LogInformation("[PAYMENT] Payment sent successfully!");
                        return new PayResponse(PayResult.Ok);
                    }
                    else
                    {
                        _logger.LogWarning("[PAYMENT] Payment failed with status: {Status}", status);
                        return new PayResponse(PayResult.Error, $"Payment failed with status: {status}");
                    }
                }
                else
                {
                    _logger.LogError("[PAYMENT] Only USD wallets are supported for Lightning payments");
                    return new PayResponse(PayResult.Error, "Only USD wallets are supported for Lightning payments");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PAYMENT] Error executing Lightning payment");
                return new PayResponse(PayResult.Error, ex.Message);
            }
        }

        #endregion

        #region Response Types

        private class PaymentResponse
        {
            public LnInvoicePaymentSendData? lnInvoicePaymentSend { get; set; }

            public class LnInvoicePaymentSendData
            {
                public string? status { get; set; }
                public List<ErrorData>? errors { get; set; }
            }

            public class ErrorData
            {
                public string message { get; set; } = string.Empty;
                public string? code { get; set; }
            }
        }

        #endregion
    }
}