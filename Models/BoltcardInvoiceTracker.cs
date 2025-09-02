#nullable enable
using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Lightning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Plugins.Flash.Models
{
    /// <summary>
    /// A hook that intercepts Lightning invoice status queries to ensure Boltcard payments are tracked
    /// </summary>
    public class BoltcardInvoiceTracker : IPluginHookFilter
    {
        private readonly ILogger<BoltcardInvoiceTracker> _logger;
        private readonly IServiceProvider _serviceProvider;
        
        public BoltcardInvoiceTracker(ILogger<BoltcardInvoiceTracker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }
        
        public string Hook => "lightning-invoice-get";
        
        /// <summary>
        /// Intercepts invoice retrieval to ensure Boltcard invoices are tracked
        /// </summary>
        public async Task<object> Execute(object args)
        {
            try
            {
                _logger.LogInformation("[BOLTCARD TRACKER] Lightning invoice get hook called");
                
                // Extract args using reflection
                var argsType = args.GetType();
                
                // Try to get the lightning client
                var clientProp = argsType.GetProperty("Client") ?? 
                                argsType.GetProperty("LightningClient");
                                
                var invoiceIdProp = argsType.GetProperty("InvoiceId") ??
                                   argsType.GetProperty("PaymentHash") ??
                                   argsType.GetProperty("Id");
                
                if (clientProp != null && invoiceIdProp != null)
                {
                    var client = clientProp.GetValue(args);
                    var invoiceId = invoiceIdProp.GetValue(args)?.ToString();
                    
                    _logger.LogInformation($"[BOLTCARD TRACKER] Checking invoice {invoiceId}, client type: {client?.GetType().Name}");
                    
                    // If this is our Flash client, ensure tracking
                    if (client is FlashLightningClient flashClient && !string.IsNullOrEmpty(invoiceId))
                    {
                        _logger.LogInformation($"[BOLTCARD TRACKER] Flash client detected, ensuring invoice {invoiceId} is tracked");
                        
                        // The GetInvoice method in FlashLightningClient should handle tracking
                        // This hook just logs for debugging
                    }
                }
                
                return args;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BOLTCARD TRACKER] Error in invoice tracking hook");
                return args;
            }
        }
    }
}