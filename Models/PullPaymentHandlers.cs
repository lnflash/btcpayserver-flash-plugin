using System;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Lightning;
using BTCPayServer.Payouts;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace BTCPayServer.Plugins.Flash.Models
{
    /// <summary>
    /// Validates if a Lightning destination is valid for a Pull Payment
    /// </summary>
    public class PullPaymentDestinationValidator : IPluginHookFilter
    {
        private readonly ILogger<PullPaymentDestinationValidator> _logger;

        public PullPaymentDestinationValidator(ILogger<PullPaymentDestinationValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Hook => "pull-payment-destination-validate";

        public async Task<object> Execute(object args)
        {
            if (args is not string destination)
                return args;

            try
            {
                _logger.LogInformation($"Validating pull payment destination: {destination}");

                var lnurlHelper = new Models.FlashLnurlHelper(_logger);

                // Check if it's a valid Lightning address or LNURL
                var (isLnurl, _) = await lnurlHelper.CheckForLnurl(destination, CancellationToken.None);

                if (isLnurl)
                {
                    _logger.LogInformation($"Valid LNURL or Lightning address detected: {destination}");
                    return new
                    {
                        IsValid = true,
                        PaymentMethodId = "BTC_LightningLike"
                    };
                }

                // Check if it's a valid BOLT11 invoice
                if (destination.StartsWith("lnbc", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"Valid BOLT11 invoice detected");
                    return new
                    {
                        IsValid = true,
                        PaymentMethodId = "BTC_LightningLike"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating destination: {destination}");
            }

            return args;
        }
    }

    /// <summary>
    /// Processes LNURL or Lightning addresses for Pull Payment claims
    /// </summary>
    public class PullPaymentClaimProcessor : IPluginHookFilter
    {
        private readonly ILogger<PullPaymentClaimProcessor> _logger;
        private readonly FlashLightningClient _flashClient;

        public PullPaymentClaimProcessor(
    ILogger<PullPaymentClaimProcessor> logger,
    FlashLightningClient flashClient = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _flashClient = flashClient;

            if (_flashClient == null)
            {
                _logger.LogWarning("FlashLightningClient was not provided to PullPaymentClaimProcessor - Flash payments will not be available until configured");
            }
        }

        public string Hook => "pull-payment-claim-process";

        public async Task<object> Execute(object args)
        {
            // In BTCPayServer, this should be a ClaimRequest object
            if (args == null || !args.GetType().Name.Contains("ClaimRequest"))
            {
                return args;
            }

            try
            {
                _logger.LogInformation($"Processing pull payment claim: {args.GetType().Name}");

                // Use reflection to safely access properties without direct dependencies
                var claimRequestType = args.GetType();

                // Check if this is a Lightning claim
                var paymentMethodIdProp = claimRequestType.GetProperty("PayoutMethodId");
                object paymentMethodId = paymentMethodIdProp?.GetValue(args);

                // Check if this is a BTC_LightningLike payment
                if (paymentMethodId == null ||
                    !paymentMethodId.ToString().Contains("BTC_Lightning"))
                {
                    return args;
                }

                // Get the destination property
                var destinationProp = claimRequestType.GetProperty("Destination");
                object destination = destinationProp?.GetValue(args);

                if (destination == null)
                {
                    return args;
                }

                _logger.LogInformation($"Processing Lightning claim with destination: {destination}");

                // Check if it's an LNURL or Lightning address
                string destinationString = destination.ToString();
                var lnurlHelper = new Models.FlashLnurlHelper(_logger);

                var (isLnurl, _) = await lnurlHelper.CheckForLnurl(destinationString, CancellationToken.None);
                if (isLnurl)
                {
                    _logger.LogInformation($"Detected LNURL-compatible destination: {destinationString}");

                    // Create a wrapper object that simulates an LNURLPayClaimDestination
                    // First let's try to find the LNURLPayClaimDestination type in BTCPayServer
                    Type lnurlClaimType = null;

                    try
                    {
                        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                        foreach (var assembly in assemblies)
                        {
                            var type = assembly.GetTypes()
                                .FirstOrDefault(t => t.Name == "LNURLPayClaimDestination");

                            if (type != null)
                            {
                                lnurlClaimType = type;
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error finding LNURLPayClaimDestination type");
                    }

                    if (lnurlClaimType != null)
                    {
                        _logger.LogInformation($"Found LNURLPayClaimDestination type: {lnurlClaimType.FullName}");

                        try
                        {
                            // Try to create an instance with the destination
                            var lnurlClaimInstance = Activator.CreateInstance(lnurlClaimType, new[] { destinationString });

                            if (lnurlClaimInstance != null)
                            {
                                // Set the destination to our LNURL claim destination
                                destinationProp.SetValue(args, lnurlClaimInstance);
                                _logger.LogInformation("Successfully converted to LNURLPayClaimDestination");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error creating LNURLPayClaimDestination instance");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not find LNURLPayClaimDestination type");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PullPaymentClaimProcessor");
            }

            return args;
        }
    }

    /// <summary>
    /// Provides an LNURL-withdraw handler for Pull Payments
    /// </summary>
    public class LnurlWithdrawHandler : IPluginHookFilter
    {
        private readonly ILogger<LnurlWithdrawHandler> _logger;
        private readonly FlashLightningClient _flashClient;
        private readonly FlashPullPaymentHandler _pullPaymentHandler;

        public LnurlWithdrawHandler(
            ILogger<LnurlWithdrawHandler> logger,
            FlashLightningClient flashClient = null,
            FlashPullPaymentHandler pullPaymentHandler = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _flashClient = flashClient;
            _pullPaymentHandler = pullPaymentHandler;

            if (_flashClient == null)
            {
                _logger.LogWarning("FlashLightningClient was not provided to LnurlWithdrawHandler - Flash LNURL withdrawals will not be available until configured");
            }

            if (_pullPaymentHandler == null && _flashClient != null)
            {
                _logger.LogInformation("Creating FlashPullPaymentHandler with provided client");
                var loggerFactory = new Microsoft.Extensions.Logging.LoggerFactory();
                var handlerLogger = loggerFactory.CreateLogger<FlashPullPaymentHandler>();
                _pullPaymentHandler = new FlashPullPaymentHandler(handlerLogger, _flashClient);
            }
        }

        public string Hook => "lnurl-withdraw-process";

        public async Task<object> Execute(object args)
        {
            try
            {
                _logger.LogInformation("Processing LNURL withdraw request");

                // Check if we have the required services
                if (_flashClient == null || _pullPaymentHandler == null)
                {
                    _logger.LogWarning("Flash services not available - cannot process LNURL withdraw request");
                    return args;
                }

                // Extract arguments using reflection since we don't have direct access to BTCPayServer types
                if (args == null)
                {
                    return args;
                }

                var argsType = args.GetType();

                // Extract storeName for better invoice descriptions
                string storeName = null;
                var storeNameProp = argsType.GetProperty("StoreName");
                if (storeNameProp != null)
                {
                    storeName = storeNameProp.GetValue(args) as string;
                }

                // Extract the LNURL-withdraw endpoint
                var urlProp = argsType.GetProperty("LnurlEndpoint") ??
                              argsType.GetProperty("Url") ??
                              argsType.GetProperty("Endpoint");

                if (urlProp == null)
                {
                    _logger.LogWarning("Could not find LnurlEndpoint property in args");
                    return args;
                }

                object urlObj = urlProp.GetValue(args);
                string lnurlEndpoint = urlObj?.ToString();

                if (string.IsNullOrEmpty(lnurlEndpoint))
                {
                    _logger.LogWarning("LNURL endpoint is null or empty");
                    return args;
                }

                // Extract the amount (in satoshis)
                var amountProp = argsType.GetProperty("Amount") ??
                                argsType.GetProperty("AmountSats") ??
                                argsType.GetProperty("SatoshiAmount");

                if (amountProp == null)
                {
                    _logger.LogWarning("Could not find Amount property in args");
                    return args;
                }

                object amountObj = amountProp.GetValue(args);
                long amount = 0;

                if (amountObj != null)
                {
                    // Handle different amount types
                    if (amountObj is long longAmount)
                    {
                        amount = longAmount;
                    }
                    else if (amountObj is int intAmount)
                    {
                        amount = intAmount;
                    }
                    else if (amountObj is decimal decimalAmount)
                    {
                        amount = (long)decimalAmount;
                    }
                    else if (amountObj is double doubleAmount)
                    {
                        amount = (long)doubleAmount;
                    }
                    else
                    {
                        // Try to parse as string or other type
                        if (long.TryParse(amountObj.ToString(), out long parsedAmount))
                        {
                            amount = parsedAmount;
                        }
                    }
                }

                if (amount <= 0)
                {
                    _logger.LogWarning($"Invalid amount: {amountObj}");
                    return args;
                }

                _logger.LogInformation($"Creating invoice for LNURL-withdraw: {lnurlEndpoint}, Amount: {amount} sats, Store: {storeName ?? "Unknown"}");

                // Create an invoice using FlashPullPaymentHandler
                var (bolt11, error) = await _pullPaymentHandler.ProcessLNURLWithdraw(
                    lnurlEndpoint,
                    amount,
                    storeName,
                    CancellationToken.None);

                if (string.IsNullOrEmpty(bolt11))
                {
                    _logger.LogError($"Failed to create invoice: {error}");
                    return args;
                }

                // Try to set the bolt11 property on the return object
                var bolt11Prop = argsType.GetProperty("Bolt11") ??
                                argsType.GetProperty("Invoice") ??
                                argsType.GetProperty("PaymentRequest");

                if (bolt11Prop != null && bolt11Prop.CanWrite)
                {
                    _logger.LogInformation($"Setting BOLT11 invoice on return object: {bolt11.Substring(0, Math.Min(bolt11.Length, 30))}...");
                    bolt11Prop.SetValue(args, bolt11);
                }
                else
                {
                    _logger.LogWarning("Could not find writable Bolt11 property in args");
                }

                return args;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LnurlWithdrawHandler");
                return args;
            }
        }
    }
}