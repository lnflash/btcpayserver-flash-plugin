using System.Text.Json;
using BTCPayServer.Lightning;
using System.Threading.Tasks;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.Extensions.Logging;
using BTCPayServer.Plugins.Flash.Models;
using System.Net.Http.Headers;
using System;
using System.Net.Http;
using System.Threading;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace BTCPayServer.Plugins.Flash.Services;

public class FlashClient : ILightningClient
{
    private readonly ILogger _logger;
    private readonly GraphQLHttpClient _graphQLClient;
    private readonly string _apiUrl;
    private readonly string _bearerToken;

    public FlashClient(string apiUrl, string bearerToken, ILogger logger)
    {
        _logger = logger;
        _apiUrl = apiUrl;
        _bearerToken = bearerToken;

        // Configure GraphQL client with authentication
        var options = new GraphQLHttpClientOptions
        {
            EndPoint = new Uri(apiUrl)
        };

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        _graphQLClient = new GraphQLHttpClient(
            options,
            new SystemTextJsonSerializer(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }),
            httpClient
        );
    }

    #region Wallet Information

    public async Task<LightningNodeInformation> GetInfo(CancellationToken cancellation = default)
    {
        try
        {
            var query = new GraphQLHttpRequest
            {
                Query = @"
                    query {
                      wallet {
                        balance
                        currency
                      }
                    }"
            };

            var response = await _graphQLClient.SendQueryAsync<WalletInfoResponse>(query, cancellation);
            
            if (response.Errors != null && response.Errors.Length > 0)
            {
                throw new LightningClientException($"Error getting wallet info: {response.Errors[0].Message}");
            }

            // Convert to BTCPay's Lightning node info format
            return new LightningNodeInformation
            {
                BlockHeight = 0, // Not provided by Flash API
                NodeURIs = Array.Empty<string>(), // Not provided by Flash API
                Version = "Flash", 
                Alias = "Flash Wallet",
                Color = "#FF7A00", // Flash brand color
                PeersCount = 0, // Not provided by Flash API
                ActiveChannelsCount = 0, // Not provided by Flash API
                InactiveChannelsCount = 0, // Not provided by Flash API
                PendingChannelsCount = 0 // Not provided by Flash API
            };
        }
        catch (Exception ex) when (ex is not LightningClientException)
        {
            _logger.LogError(ex, "Error getting Flash wallet info");
            throw new LightningClientException($"Error getting Flash wallet info: {ex.Message}", ex);
        }
    }

    #endregion

    #region Invoice Operations

    public async Task<LightningInvoice> CreateInvoice(LightningInvoiceCreateRequest request, CancellationToken cancellation = default)
    {
        try
        {
            var mutation = new GraphQLHttpRequest
            {
                Query = @"
                    mutation CreateInvoice($amount: SatAmount!, $memo: String) {
                      lnInvoiceCreate(input: {
                        amount: $amount,
                        memo: $memo
                      }) {
                        invoice {
                          paymentRequest
                          paymentHash
                          paymentSecret
                          amount
                          satAmount
                          expiresAt
                        }
                      }
                    }",
                Variables = new
                {
                    amount = request.Amount.Satoshi,
                    memo = request.Description ?? "BTCPay Invoice"
                }
            };

            var response = await _graphQLClient.SendMutationAsync<CreateInvoiceResponse>(mutation, cancellation);
            
            if (response.Errors != null && response.Errors.Length > 0)
            {
                throw new LightningClientException($"Error creating invoice: {response.Errors[0].Message}");
            }

            if (response.Data?.LnInvoiceCreate?.Invoice == null)
            {
                throw new LightningClientException("No invoice data returned from Flash");
            }

            var invoice = response.Data.LnInvoiceCreate.Invoice;
            var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(invoice.ExpiresAt).DateTime;

            return new LightningInvoice
            {
                Id = invoice.PaymentHash,
                BOLT11 = invoice.PaymentRequest,
                Amount = new LightMoney(invoice.SatAmount, LightMoneyUnit.Satoshi),
                Status = LightningInvoiceStatus.Unpaid,
                ExpiresAt = expiresAt,
                PaymentHash = invoice.PaymentHash
            };
        }
        catch (Exception ex) when (ex is not LightningClientException)
        {
            _logger.LogError(ex, "Error creating Flash invoice");
            throw new LightningClientException($"Error creating Flash invoice: {ex.Message}", ex);
        }
    }

    public async Task<LightningInvoice> GetInvoice(string invoiceId, CancellationToken cancellation = default)
    {
        try
        {
            var query = new GraphQLHttpRequest
            {
                Query = @"
                    query GetInvoice($paymentHash: PaymentHash!) {
                      lnInvoicePaymentStatus(input: {
                        paymentHash: $paymentHash
                      }) {
                        status
                        errors {
                          message
                        }
                      }
                    }",
                Variables = new
                {
                    paymentHash = invoiceId
                }
            };

            var response = await _graphQLClient.SendQueryAsync<GetInvoiceResponse>(query, cancellation);
            
            if (response.Errors != null && response.Errors.Length > 0)
            {
                throw new LightningClientException($"Error getting invoice: {response.Errors[0].Message}");
            }

            if (response.Data?.LnInvoicePaymentStatus == null)
            {
                throw new LightningClientException("No invoice data returned from Flash");
            }

            var status = response.Data.LnInvoicePaymentStatus.Status;
            var lightningStatus = status switch
            {
                "SUCCESS" => LightningInvoiceStatus.Paid,
                "PENDING" => LightningInvoiceStatus.Unpaid,
                "EXPIRED" => LightningInvoiceStatus.Expired,
                _ => LightningInvoiceStatus.Unpaid
            };

            // We don't have all invoice details from this endpoint,
            // but we can return the status which is often what's needed
            return new LightningInvoice
            {
                Id = invoiceId,
                Status = lightningStatus,
                PaymentHash = invoiceId
            };
        }
        catch (Exception ex) when (ex is not LightningClientException)
        {
            _logger.LogError(ex, "Error getting Flash invoice");
            throw new LightningClientException($"Error getting Flash invoice: {ex.Message}", ex);
        }
    }

    public async Task<LightningPayment> Pay(string bolt11, LightningPayRequest request, CancellationToken cancellation = default)
    {
        try
        {
            var mutation = new GraphQLHttpRequest
            {
                Query = @"
                    mutation PayInvoice($invoice: LnInvoice!) {
                      lnInvoicePaymentSend(input: {
                        paymentRequest: $invoice
                      }) {
                        status
                        errors {
                          message
                        }
                        payment {
                          id
                          fee
                          amount
                          paymentHash
                          paymentPreimage
                        }
                      }
                    }",
                Variables = new
                {
                    invoice = bolt11
                }
            };

            var response = await _graphQLClient.SendMutationAsync<PayInvoiceResponse>(mutation, cancellation);
            
            if (response.Errors != null && response.Errors.Length > 0)
            {
                throw new LightningClientException($"Error paying invoice: {response.Errors[0].Message}");
            }

            if (response.Data?.LnInvoicePaymentSend == null)
            {
                throw new LightningClientException("No payment data returned from Flash");
            }

            var paymentResult = response.Data.LnInvoicePaymentSend;
            
            if (paymentResult.Status != "SUCCESS")
            {
                var errorMessage = paymentResult.Errors?.FirstOrDefault()?.Message ?? "Unknown error";
                throw new LightningClientException($"Payment failed: {errorMessage}");
            }

            if (paymentResult.Payment == null)
            {
                throw new LightningClientException("Payment succeeded but no payment details returned");
            }

            var payment = paymentResult.Payment;

            return new LightningPayment
            {
                Id = payment.Id,
                PaymentHash = payment.PaymentHash,
                Preimage = payment.PaymentPreimage,
                Amount = new LightMoney(payment.Amount, LightMoneyUnit.Satoshi),
                Fee = new LightMoney(payment.Fee, LightMoneyUnit.Satoshi),
                Status = LightningPaymentStatus.Complete
            };
        }
        catch (Exception ex) when (ex is not LightningClientException)
        {
            _logger.LogError(ex, "Error paying Flash invoice");
            throw new LightningClientException($"Error paying Flash invoice: {ex.Message}", ex);
        }
    }

    #endregion

    #region Not Implemented Operations

    public Task<ILightningChannelAcceptor?> CreateChannelAcceptor(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ILightningChannelAcceptor?>(null);
    }

    public Task<LightningChannel[]> GetChannels(CancellationToken cancellation = default)
    {
        return Task.FromResult(Array.Empty<LightningChannel>());
    }

    public Task<LightningInvoice[]> GetInvoices(CancellationToken cancellation = default)
    {
        return Task.FromResult(Array.Empty<LightningInvoice>());
    }

    public Task<LightningNodeBalance> GetBalance(CancellationToken cancellation = default)
    {
        return Task.FromResult(new LightningNodeBalance());
    }

    public Task<LightningPayment[]> GetPayments(CancellationToken cancellation = default)
    {
        return Task.FromResult(Array.Empty<LightningPayment>());
    }

    public Task<LightningChannel> OpenChannel(OpenChannelRequest request, CancellationToken cancellation = default)
    {
        throw new NotImplementedException("Channel management is not supported by the Flash plugin");
    }

    public Task<LightningChannel[]> GetPendingChannels(CancellationToken cancellation = default)
    {
        return Task.FromResult(Array.Empty<LightningChannel>());
    }

    public Task CancelInvoice(string invoiceId, CancellationToken cancellation = default)
    {
        throw new NotImplementedException("Cancelling invoices is not supported by the Flash plugin");
    }

    public Task<LightningPayment> GetPayment(string paymentHash, CancellationToken cancellation = default)
    {
        throw new NotImplementedException("Getting payments by hash is not supported by the Flash plugin");
    }

    public Task CloseChannel(CloseChannelRequest request, CancellationToken cancellation = default)
    {
        throw new NotImplementedException("Channel management is not supported by the Flash plugin");
    }

    public Task<LightningPayment> SendAsync(Lightning.SendPayment payment, CancellationToken cancellation)
    {
        throw new NotImplementedException("Custom payments are not supported by the Flash plugin");
    }

    public async Task<LightningNodeInformation.PendingChannelinfo?> GetPendingChannelInfo()
    {
        return new LightningNodeInformation.PendingChannelinfo
        {
            PendingClosingChannelsCount = 0,
            PendingForceClosingChannelsCount = 0,
            PendingOpenChannelsCount = 0,
            WaitingCloseChannelsCount = 0
        };
    }

    public void Dispose()
    {
        _graphQLClient?.Dispose();
    }

    #endregion

    #region Response Classes

    private class WalletInfoResponse
    {
        public WalletInfo? Wallet { get; set; }
    }

    private class WalletInfo
    {
        public long Balance { get; set; }
        public string? Currency { get; set; }
    }

    private class CreateInvoiceResponse
    {
        public LnInvoiceCreateResult? LnInvoiceCreate { get; set; }
    }

    private class LnInvoiceCreateResult
    {
        public InvoiceData? Invoice { get; set; }
    }

    private class InvoiceData
    {
        public string PaymentRequest { get; set; } = string.Empty;
        public string PaymentHash { get; set; } = string.Empty;
        public string PaymentSecret { get; set; } = string.Empty;
        public long Amount { get; set; }
        public long SatAmount { get; set; }
        public long ExpiresAt { get; set; }
    }

    private class GetInvoiceResponse
    {
        public InvoicePaymentStatus? LnInvoicePaymentStatus { get; set; }
    }

    private class InvoicePaymentStatus
    {
        public string Status { get; set; } = string.Empty;
        public ErrorMessage[]? Errors { get; set; }
    }

    private class PayInvoiceResponse
    {
        public PaymentResult? LnInvoicePaymentSend { get; set; }
    }

    private class PaymentResult
    {
        public string Status { get; set; } = string.Empty;
        public ErrorMessage[]? Errors { get; set; }
        public PaymentData? Payment { get; set; }
    }

    private class PaymentData
    {
        public string Id { get; set; } = string.Empty;
        public long Fee { get; set; }
        public long Amount { get; set; }
        public string PaymentHash { get; set; } = string.Empty;
        public string PaymentPreimage { get; set; } = string.Empty;
    }

    private class ErrorMessage
    {
        public string Message { get; set; } = string.Empty;
    }

    #endregion
}

public class FlashClientProvider
{
    private readonly ILogger<FlashClientProvider> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FlashClientProvider(
        ILogger<FlashClientProvider> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public FlashClient? GetClient(FlashSettings settings)
    {
        if (string.IsNullOrEmpty(settings.BearerToken) || string.IsNullOrEmpty(settings.ApiUrl))
        {
            return null;
        }

        return new FlashClient(settings.ApiUrl, settings.BearerToken, _logger);
    }
}