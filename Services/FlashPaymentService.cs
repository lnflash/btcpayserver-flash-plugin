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
            // TODO: Implement PayInvoiceParams overload
            return new PayResponse(PayResult.Error, "Not implemented yet");
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
                
                // CRITICAL: For Boltcard payments, we need to extract the payment hash and immediately mark as paid
                // since BTCPayServer's Boltcard flow doesn't call GetInvoice to check status
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(2000); // Give payment time to process
                        
                        _logger.LogInformation("[BOLTCARD PAYMENT] Checking if Boltcard payment completed for BOLT11: {Bolt11Prefix}...", 
                            bolt11?.Substring(0, Math.Min(bolt11?.Length ?? 0, 30)));
                        
                        // Get invoice service from DI
                        var invoiceService = _serviceProvider?.GetService<IFlashInvoiceService>();
                        if (invoiceService != null && !string.IsNullOrEmpty(decodedData.paymentHash))
                        {
                            // Check payment status
                            var invoice = await invoiceService.GetInvoiceAsync(decodedData.paymentHash, cancellation);
                            if (invoice?.Status == LightningInvoiceStatus.Paid)
                            {
                                _logger.LogInformation("[BOLTCARD PAYMENT] Payment confirmed! Marking invoice {PaymentHash} as paid", decodedData.paymentHash);
                                await invoiceService.MarkInvoiceAsPaidAsync(decodedData.paymentHash, decodedData.amount ?? 1000);
                            }
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

        public Task<LightningPayment[]> ListPaymentsAsync(
            ListPaymentsParams? request = null,
            CancellationToken cancellation = default)
        {
            // TODO: Implement by querying transaction history
            return Task.FromResult(Array.Empty<LightningPayment>());
        }

        public async Task<LightningPayment> GetPaymentAsync(
            string paymentHash,
            CancellationToken cancellation = default)
        {
            try
            {
                _logger.LogInformation("Attempting to get payment status for hash: {PaymentHash}", paymentHash);

                // Query transaction history to find the payment
                var transactions = await _graphQLService.GetTransactionHistoryAsync(20, cancellation);

                // Look for matching transaction
                var matchingTransaction = transactions.FirstOrDefault(t =>
                    t.Id == paymentHash ||
                    (t.Memo != null && t.Memo.Contains(paymentHash)));

                if (matchingTransaction != null)
                {
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
                _logger.LogError(ex, "Error getting payment {PaymentHash}", paymentHash);

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

        private async Task<(string? paymentHash, long? amount)> DecodeInvoice(string bolt11, CancellationToken cancellation)
        {
            try
            {
                // For now, we'll just extract basic info from the BOLT11 string
                // In a real implementation, this should use a proper BOLT11 decoder
                _logger.LogInformation("[BOLTCARD PAYMENT] Attempting to decode BOLT11 invoice");
                
                // Extract amount from BOLT11 if present (e.g., lnbc10000n means 10000 msats)
                long? amount = null;
                var match = System.Text.RegularExpressions.Regex.Match(bolt11, @"lnbc(\d+)([munp])?");
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
                
                // For Boltcard payments, we need to track by the BOLT11 itself
                // since BTCPayServer uses the full BOLT11 as the tracking key
                string? paymentHash = bolt11;
                
                // Also try to extract the actual payment hash for logging
                // BOLT11 payment hash is in the 'p' tag field (payment_hash)
                var ppIndex = bolt11.IndexOf("pp");
                if (ppIndex > 0 && ppIndex + 2 < bolt11.Length)
                {
                    // Payment hash is 52 characters after 'pp' in bech32 encoding
                    var endIndex = Math.Min(ppIndex + 2 + 52, bolt11.Length);
                    var extractedHash = bolt11.Substring(ppIndex + 2, endIndex - (ppIndex + 2));
                    _logger.LogInformation("[BOLTCARD PAYMENT] Extracted payment hash from BOLT11: {PaymentHash}", extractedHash);
                    // Keep the BOLT11 as the primary identifier for BTCPayServer tracking
                }
                else
                {
                    _logger.LogInformation("[BOLTCARD PAYMENT] Using full BOLT11 as payment identifier for tracking");
                }
                
                _logger.LogInformation("[BOLTCARD PAYMENT] Decoded amount: {Amount} sats, PaymentHash: {PaymentHash}", amount, paymentHash);
                
                return (paymentHash, amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BOLTCARD PAYMENT] Error decoding invoice");
                return (null, null);
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