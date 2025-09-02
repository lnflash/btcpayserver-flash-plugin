using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Flash.Services
{
    /// <summary>
    /// Simple invoice creation service that directly calls Flash API without GraphQL client
    /// </summary>
    public class FlashSimpleInvoiceService
    {
        private readonly string _bearerToken;
        private readonly Uri _endpoint;
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;

        public FlashSimpleInvoiceService(string bearerToken, Uri endpoint, ILogger logger)
        {
            _bearerToken = bearerToken ?? throw new ArgumentNullException(nameof(bearerToken));
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _httpClient = new HttpClient();
            
            // Log the token details for debugging
            _logger.LogInformation("[SimpleInvoice] Configuring HTTP client with token: {Token} (length: {Length})", 
                _bearerToken.Length > 10 ? _bearerToken.Substring(0, 10) + "..." : _bearerToken,
                _bearerToken.Length);
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BTCPayServer-Flash-Plugin/1.0");
            
            // Log all headers being set (mask authorization token)
            var headers = _httpClient.DefaultRequestHeaders.Select(h => 
            {
                if (h.Key == "Authorization" && h.Value.Any())
                {
                    var authValue = h.Value.First();
                    if (authValue.StartsWith("Bearer "))
                    {
                        var token = authValue.Substring(7);
                        return $"{h.Key}=Bearer {token.Substring(0, Math.Min(10, token.Length))}...";
                    }
                }
                return $"{h.Key}={string.Join(",", h.Value)}";
            });
            _logger.LogInformation("[SimpleInvoice] Headers configured: {Headers}", 
                string.Join(", ", headers));
        }

        public async Task<LightningInvoice> CreateInvoiceAsync(
            long amountSats, 
            string description, 
            CancellationToken cancellation = default)
        {
            _logger.LogInformation("=== FlashSimpleInvoiceService.CreateInvoiceAsync CALLED ===");
            _logger.LogInformation($"Amount: {amountSats} sats, Description: {description}");
            
            try
            {
                // Build the GraphQL mutation for creating an invoice
                var mutation = @"
                mutation LnInvoiceCreate($input: LnInvoiceCreateInput!) {
                    lnInvoiceCreate(input: $input) {
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

                // For Flash, we need to specify the wallet (USD wallet)
                // We'll use a hardcoded USD wallet ID or try without specifying
                var variables = new
                {
                    input = new
                    {
                        amount = amountSats,
                        memo = description ?? "Payment",
                        // Try without specifying walletId - Flash might use default
                    }
                };

                var request = new
                {
                    query = mutation,
                    variables = variables
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation("[SimpleInvoice] Creating invoice: {AmountSats} sats, Description: {Description}", 
                    amountSats, description);
                _logger.LogInformation("[SimpleInvoice] Request URL: {Endpoint}", _endpoint);
                _logger.LogInformation("[SimpleInvoice] Request Body: {Request}", json);
                // Log authorization header with masked token
                var authHeader = _httpClient.DefaultRequestHeaders.Authorization?.ToString() ?? "NOT SET";
                if (authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring(7);
                    authHeader = $"Bearer {token.Substring(0, Math.Min(10, token.Length))}...";
                }
                _logger.LogInformation("[SimpleInvoice] Authorization Header: {Auth}", authHeader);

                var response = await _httpClient.PostAsync(_endpoint, content, cancellation);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("[SimpleInvoice] Response Status: {Status}, Content: {Content}", 
                    response.StatusCode, responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[SimpleInvoice] Failed to create invoice. Status: {Status}, Response: {Response}", 
                        response.StatusCode, responseContent);
                    throw new Exception($"Failed to create invoice: {response.StatusCode}");
                }

                var jsonResponse = JObject.Parse(responseContent);
                var data = jsonResponse["data"]?["lnInvoiceCreate"];
                var errors = data?["errors"];

                if (errors != null && errors.HasValues)
                {
                    var errorMessage = errors[0]?["message"]?.ToString() ?? "Unknown error";
                    throw new Exception($"Flash API error: {errorMessage}");
                }

                var invoiceData = data?["invoice"];
                if (invoiceData == null)
                {
                    throw new Exception("No invoice data in response");
                }

                var invoice = new LightningInvoice
                {
                    Id = invoiceData["paymentHash"]?.ToString() ?? Guid.NewGuid().ToString(),
                    PaymentHash = invoiceData["paymentHash"]?.ToString(),
                    Preimage = invoiceData["paymentSecret"]?.ToString(),
                    BOLT11 = invoiceData["paymentRequest"]?.ToString(),
                    Status = LightningInvoiceStatus.Unpaid,
                    Amount = LightMoney.Satoshis(amountSats),
                    AmountReceived = LightMoney.Zero,
                    PaidAt = null,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
                };

                _logger.LogInformation("[SimpleInvoice] Created invoice: {InvoiceId}, PaymentHash: {PaymentHash}", 
                    invoice.Id, invoice.PaymentHash);

                return invoice;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SimpleInvoice] Error creating invoice");
                throw;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}