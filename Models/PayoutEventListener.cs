using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Plugins.Flash.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Flash.Models
{
    /// <summary>
    /// Listens for payout events and tracks them in our database
    /// </summary>
    public class PayoutEventListener : IPluginHookFilter
    {
        private readonly ILogger<PayoutEventListener> _logger;
        private readonly IServiceProvider _serviceProvider;

        public PayoutEventListener(
            ILogger<PayoutEventListener> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public string Hook => "payout-created";

        public async Task<object> Execute(object args)
        {
            try
            {
                _logger.LogInformation($"Payout event triggered: {args?.GetType().Name}");

                if (args == null)
                {
                    return args;
                }

                // Extract payout information using reflection
                var payoutType = args.GetType();
                
                var storeIdProp = payoutType.GetProperty("StoreId");
                var pullPaymentIdProp = payoutType.GetProperty("PullPaymentId");
                var amountProp = payoutType.GetProperty("Amount");
                var payoutIdProp = payoutType.GetProperty("Id") ?? payoutType.GetProperty("PayoutId");
                var destinationProp = payoutType.GetProperty("Destination");
                var paymentMethodProp = payoutType.GetProperty("PaymentMethodId") ?? payoutType.GetProperty("PayoutMethodId");

                if (storeIdProp == null || pullPaymentIdProp == null || amountProp == null)
                {
                    _logger.LogWarning("Payout event missing required properties");
                    return args;
                }

                var storeId = storeIdProp.GetValue(args)?.ToString();
                var pullPaymentId = pullPaymentIdProp.GetValue(args)?.ToString();
                var amount = amountProp.GetValue(args);
                var payoutId = payoutIdProp?.GetValue(args)?.ToString();
                var destination = destinationProp?.GetValue(args)?.ToString();
                var paymentMethod = paymentMethodProp?.GetValue(args)?.ToString();

                // Only track Lightning payouts
                if (paymentMethod == null || !paymentMethod.Contains("Lightning"))
                {
                    _logger.LogDebug($"Skipping non-Lightning payout: {paymentMethod}");
                    return args;
                }

                // Convert amount to satoshis
                long amountSats = 0;
                if (amount != null)
                {
                    // Handle different amount types (decimal, LightMoney, etc)
                    if (amount is decimal decimalAmount)
                    {
                        amountSats = (long)(decimalAmount * 100_000_000);
                    }
                    else if (amount.GetType().Name == "LightMoney")
                    {
                        var satoshisProp = amount.GetType().GetProperty("Satoshi");
                        if (satoshisProp != null)
                        {
                            amountSats = (long)satoshisProp.GetValue(amount);
                        }
                    }
                    else
                    {
                        // Try to parse as string
                        if (long.TryParse(amount.ToString(), out var parsed))
                        {
                            amountSats = parsed;
                        }
                    }
                }

                _logger.LogInformation($"Tracking Lightning payout: Store={storeId}, PullPayment={pullPaymentId}, Amount={amountSats} sats");

                // Track the payout
                using (var scope = _serviceProvider.CreateScope())
                {
                    var trackingService = scope.ServiceProvider.GetService<IFlashPayoutTrackingService>();
                    if (trackingService != null)
                    {
                        var payout = await trackingService.TrackPayoutAsync(
                            storeId,
                            pullPaymentId,
                            amountSats,
                            $"Payout {payoutId}");

                        // Try to extract Boltcard ID from destination if it's LNURL
                        if (!string.IsNullOrEmpty(destination))
                        {
                            var boltcardId = await trackingService.ExtractBoltcardIdFromLnurlAsync(destination);
                            if (!string.IsNullOrEmpty(boltcardId))
                            {
                                await trackingService.AssociateBoltcardAsync(payout.Id, boltcardId);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("FlashPayoutTrackingService not available");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking payout event");
            }

            return args;
        }
    }

    /// <summary>
    /// Listens for payout state changes
    /// </summary>
    public class PayoutStateChangeListener : IPluginHookFilter
    {
        private readonly ILogger<PayoutStateChangeListener> _logger;
        private readonly IServiceProvider _serviceProvider;

        public PayoutStateChangeListener(
            ILogger<PayoutStateChangeListener> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public string Hook => "payout-state-changed";

        public async Task<object> Execute(object args)
        {
            try
            {
                _logger.LogInformation($"Payout state change event: {args?.GetType().Name}");

                if (args == null)
                {
                    return args;
                }

                // Extract state change information
                var eventType = args.GetType();
                
                var payoutIdProp = eventType.GetProperty("PayoutId") ?? eventType.GetProperty("Id");
                var newStateProp = eventType.GetProperty("NewState") ?? eventType.GetProperty("State");
                var paymentHashProp = eventType.GetProperty("PaymentHash") ?? eventType.GetProperty("TransactionId");

                if (payoutIdProp == null || newStateProp == null)
                {
                    return args;
                }

                var payoutId = payoutIdProp.GetValue(args)?.ToString();
                var newState = newStateProp.GetValue(args)?.ToString();
                var paymentHash = paymentHashProp?.GetValue(args)?.ToString();

                _logger.LogInformation($"Payout {payoutId} state changed to: {newState}");

                // Map state to our enum
                var status = newState?.ToLower() switch
                {
                    "completed" => PayoutStatus.Completed,
                    "failed" => PayoutStatus.Failed,
                    "inprogress" => PayoutStatus.Processing,
                    "awaitingpayment" => PayoutStatus.Processing,
                    _ => PayoutStatus.Pending
                };

                // Update our tracking
                using (var scope = _serviceProvider.CreateScope())
                {
                    var trackingService = scope.ServiceProvider.GetService<IFlashPayoutTrackingService>();
                    if (trackingService != null)
                    {
                        await trackingService.UpdatePayoutStatusAsync(payoutId, status, paymentHash);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling payout state change");
            }

            return args;
        }
    }
}