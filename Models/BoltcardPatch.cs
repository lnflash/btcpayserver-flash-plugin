#nullable enable
using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Lightning;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Flash.Models
{
    /// <summary>
    /// A hook that patches LNURL amount validation for Boltcard compatibility
    /// </summary>
    public class BoltcardPatch : IPluginHookFilter
    {
        private readonly ILogger<BoltcardPatch> _logger;
        
        public BoltcardPatch(ILogger<BoltcardPatch> logger)
        {
            _logger = logger;
        }
        
        public string Hook => "lnurl-withdraw-request-amount-verify";
        
        /// <summary>
        /// Accepts any amount for Boltcard transactions
        /// </summary>
        public Task<object> Execute(object args)
        {
            try
            {
                _logger.LogInformation("[BOLTCARD] Checking for Boltcard transaction");
                
                // Extract args using reflection
                var argsType = args.GetType();
                
                // Try to get amount property
                var requestedAmountProp = argsType.GetProperty("RequestedAmount") ?? 
                                        argsType.GetProperty("ExpectedAmount") ??
                                        argsType.GetProperty("Amount");
                                        
                var actualAmountProp = argsType.GetProperty("ActualAmount") ??
                                      argsType.GetProperty("InvoiceAmount");
                                      
                var descriptionProp = argsType.GetProperty("Description") ??
                                     argsType.GetProperty("Comment") ??
                                     argsType.GetProperty("Memo");
                                     
                var resultProp = argsType.GetProperty("IsValid") ??
                                argsType.GetProperty("Result");
                
                // If we can't extract properties, return as-is
                if (requestedAmountProp == null || resultProp == null || !resultProp.CanWrite)
                {
                    return Task.FromResult(args);
                }
                
                // Get values
                long requestedAmount = 0;
                if (requestedAmountProp.GetValue(args) is long reqLong)
                {
                    requestedAmount = reqLong;
                }
                else if (int.TryParse(requestedAmountProp.GetValue(args)?.ToString(), out int reqInt))
                {
                    requestedAmount = reqInt;
                }
                
                long actualAmount = 0;
                if (actualAmountProp?.GetValue(args) is long actLong)
                {
                    actualAmount = actLong;
                }
                else if (actualAmountProp != null && int.TryParse(actualAmountProp.GetValue(args)?.ToString(), out int actInt))
                {
                    actualAmount = actInt;
                }
                
                string? description = descriptionProp?.GetValue(args)?.ToString();
                
                // Check if this is likely a Boltcard transaction
                bool isBoltcard = !string.IsNullOrEmpty(description) &&
                    (description.Contains("Boltcard", StringComparison.OrdinalIgnoreCase) ||
                     description.Contains("Top-Up", StringComparison.OrdinalIgnoreCase) ||
                     description.Contains("topup", StringComparison.OrdinalIgnoreCase));
                     
                // Also identify by the classic 969 sats amount
                if (requestedAmount == 969 || requestedAmount == 969000)
                {
                    isBoltcard = true;
                }
                
                if (isBoltcard)
                {
                    _logger.LogInformation($"[BOLTCARD] Detected Boltcard transaction: r={requestedAmount}, a={actualAmount}");
                    
                    // Always return true for Boltcard transactions
                    resultProp.SetValue(args, true);
                    
                    _logger.LogInformation("[BOLTCARD] Allowing Boltcard transaction regardless of amount");
                }
                else
                {
                    // Log but don't modify result for non-Boltcard transactions
                    _logger.LogInformation($"Not a Boltcard transaction: {description}, amount={requestedAmount}");
                }
                
                return Task.FromResult(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BOLTCARD] Error in Boltcard patch");
                return Task.FromResult(args);
            }
        }
    }
}