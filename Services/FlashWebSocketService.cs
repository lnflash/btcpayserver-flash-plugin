#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
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
    /// Implementation of WebSocket service for real-time Flash API updates
    /// </summary>
    public class FlashWebSocketService : IFlashWebSocketService
    {
        private readonly ILogger<FlashWebSocketService> _logger;
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _disconnectTokenSource;
        private Task? _receiveLoopTask;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1);
        private readonly HashSet<string> _subscribedInvoices = new HashSet<string>();
        private string? _bearerToken;
        private Uri? _wsEndpoint;
        private readonly Dictionary<string, TaskCompletionSource<bool>> _pendingOperations = new();
        private int _messageId = 0;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;
        public event EventHandler<InvoiceUpdateEventArgs>? InvoiceUpdated;

        public FlashWebSocketService(ILogger<FlashWebSocketService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ConnectAsync(string bearerToken, Uri websocketEndpoint, CancellationToken cancellation = default)
        {
            if (IsConnected)
            {
                _logger.LogWarning("WebSocket already connected");
                return;
            }

            _bearerToken = bearerToken;
            _wsEndpoint = websocketEndpoint;

            try
            {
                _webSocket = new ClientWebSocket();
                _webSocket.Options.AddSubProtocol("graphql-ws");
                _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                
                // Add Authorization header to the WebSocket handshake
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
                
                // Try different protocols - Flash might use graphql-transport-ws instead
                _webSocket.Options.AddSubProtocol("graphql-transport-ws");

                _logger.LogInformation("Connecting to Flash WebSocket at {Endpoint} with Authorization header and multiple protocols", websocketEndpoint);
                await _webSocket.ConnectAsync(websocketEndpoint, cancellation);
                
                // Verify connection is established
                if (_webSocket.State != WebSocketState.Open)
                {
                    throw new InvalidOperationException($"WebSocket failed to connect. State: {_webSocket.State}");
                }
                
                _logger.LogInformation("WebSocket connection established (State: {State}), starting handshake", _webSocket.State);

                _disconnectTokenSource = new CancellationTokenSource();
                _receiveLoopTask = Task.Run(() => ReceiveLoop(_disconnectTokenSource.Token));

                // Wait a moment for the connection to stabilize
                await Task.Delay(100, cancellation);

                // Send connection_init message as required by graphql-ws protocol
                await SendConnectionInitMessage(cancellation);
                
                // Wait for connection_ack
                var ackReceived = await WaitForConnectionAck();
                if (ackReceived)
                {
                    _logger.LogInformation("✅ Successfully connected to Flash WebSocket with protocol: {Protocol}", _webSocket?.SubProtocol ?? "none");
                }
                else
                {
                    _logger.LogWarning("Did not receive connection acknowledgment from server within timeout");
                    throw new InvalidOperationException("Did not receive connection acknowledgment from server");
                }
            }
            catch (System.Net.WebSockets.WebSocketException wsEx) when (wsEx.WebSocketErrorCode == WebSocketError.NotAWebSocket)
            {
                _logger.LogInformation("WebSocket endpoint does not support WebSocket protocol. This is normal - Flash may not have WebSocket support enabled.");
                await CleanupConnection();
                throw;
            }
            catch (System.Net.WebSockets.WebSocketException wsEx) when (wsEx.Message.Contains("503"))
            {
                _logger.LogInformation("Flash WebSocket service temporarily unavailable (503). This is normal - using polling instead.");
                await CleanupConnection();
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to Flash WebSocket: {ErrorType} - {Message}. Falling back to polling.", 
                    ex.GetType().Name, ex.Message);
                await CleanupConnection();
                throw;
            }
        }

        private async Task SendConnectionInitMessage(CancellationToken cancellation)
        {
            // Check which protocol was actually negotiated
            var negotiatedProtocol = _webSocket?.SubProtocol;
            _logger.LogInformation("WebSocket negotiated protocol: {Protocol}", negotiatedProtocol ?? "none");
            
            object initMessage;
            
            if (negotiatedProtocol == "graphql-transport-ws")
            {
                // New graphql-transport-ws protocol format
                initMessage = new
                {
                    type = "connection_init",
                    payload = new
                    {
                        Authorization = $"Bearer {_bearerToken}"
                    }
                };
            }
            else
            {
                // Legacy graphql-ws protocol format
                initMessage = new
                {
                    id = Guid.NewGuid().ToString(),
                    type = "connection_init",
                    payload = new
                    {
                        Authorization = $"Bearer {_bearerToken}"
                    }
                };
            }

            var messageJson = JsonConvert.SerializeObject(initMessage);
            _logger.LogInformation("Sending connection_init with protocol {Protocol}: {Message}", negotiatedProtocol, messageJson);
            await SendMessageAsync(messageJson, cancellation);
        }

        private async Task<bool> WaitForConnectionAck()
        {
            var tcs = new TaskCompletionSource<bool>();
            _pendingOperations["connection_ack"] = tcs;
            
            // Wait for up to 5 seconds for connection_ack
            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            
            _pendingOperations.Remove("connection_ack");
            
            return completedTask == tcs.Task && await tcs.Task;
        }

        public async Task SubscribeToInvoiceUpdatesAsync(string invoiceId, CancellationToken cancellation = default)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot subscribe to invoice updates: WebSocket not connected");
                return;
            }

            if (_subscribedInvoices.Contains(invoiceId))
            {
                _logger.LogDebug("Already subscribed to invoice {InvoiceId}", invoiceId);
                return;
            }

            var subscriptionId = $"invoice_{invoiceId}_{++_messageId}";
            
            // Create subscription for lnInvoicePaymentStatus
            var subscribeMessage = new
            {
                id = subscriptionId,
                type = "subscribe",
                payload = new
                {
                    query = @"
                        subscription InvoicePaymentStatus($input: LnInvoicePaymentStatusInput!) {
                            lnInvoicePaymentStatus(input: $input) {
                                status
                                errors {
                                    message
                                }
                            }
                        }",
                    variables = new
                    {
                        input = new
                        {
                            paymentRequest = invoiceId
                        }
                    }
                }
            };

            var messageJson = JsonConvert.SerializeObject(subscribeMessage);
            _logger.LogDebug("Subscribing to invoice updates: {Message}", messageJson);
            await SendMessageAsync(messageJson, cancellation);
            _subscribedInvoices.Add(invoiceId);

            _logger.LogInformation("Subscribed to updates for invoice {InvoiceId}", invoiceId);
        }

        public async Task UnsubscribeFromInvoiceUpdatesAsync(string invoiceId, CancellationToken cancellation = default)
        {
            if (!IsConnected || !_subscribedInvoices.Contains(invoiceId))
            {
                return;
            }

            var unsubscribeMessage = new
            {
                id = Guid.NewGuid().ToString(),
                type = "unsubscribe",
                payload = new { invoiceId }
            };

            await SendMessageAsync(JsonConvert.SerializeObject(unsubscribeMessage), cancellation);
            _subscribedInvoices.Remove(invoiceId);

            _logger.LogInformation("Unsubscribed from updates for invoice {InvoiceId}", invoiceId);
        }

        private async Task SendMessageAsync(string message, CancellationToken cancellation)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not connected");
            }

            await _sendLock.WaitAsync(cancellation);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation);
                _logger.LogDebug("Sent WebSocket message: {Message}", message);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoop(CancellationToken cancellation)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            var messageBuilder = new StringBuilder();

            try
            {
                while (!cancellation.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;
                    messageBuilder.Clear();

                    do
                    {
                        result = await _webSocket.ReceiveAsync(buffer, cancellation);
                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            messageBuilder.Append(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, result.Count));
                        }
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = messageBuilder.ToString();
                        _logger.LogDebug("Received WebSocket message: {Message}", message);
                        ProcessMessage(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("WebSocket closed by server");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("WebSocket receive loop cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebSocket receive loop");
            }

            // Attempt reconnection if not intentionally disconnected
            if (!cancellation.IsCancellationRequested)
            {
                _logger.LogInformation("Attempting to reconnect WebSocket");
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    if (_bearerToken != null && _wsEndpoint != null)
                    {
                        try
                        {
                            await ConnectAsync(_bearerToken, _wsEndpoint);

                            // Re-subscribe to all previously subscribed invoices
                            foreach (var invoiceId in _subscribedInvoices.ToList())
                            {
                                await SubscribeToInvoiceUpdatesAsync(invoiceId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to reconnect WebSocket");
                        }
                    }
                });
            }
        }

        private void ProcessMessage(string message)
        {
            try
            {
                var json = JObject.Parse(message);
                var messageType = json["type"]?.ToString();
                var messageId = json["id"]?.ToString();

                _logger.LogDebug("Processing message type: {Type}, id: {Id}", messageType, messageId);

                switch (messageType)
                {
                    case "connection_ack":
                        _logger.LogInformation("Received connection_ack from Flash WebSocket");
                        if (_pendingOperations.TryGetValue("connection_ack", out var ackTcs))
                        {
                            ackTcs.SetResult(true);
                        }
                        break;

                    case "next":
                        ProcessSubscriptionData(json);
                        break;

                    case "error":
                        var errors = json["payload"]?["errors"] ?? json["payload"];
                        _logger.LogError("WebSocket error: {Error}", errors?.ToString());
                        break;

                    case "complete":
                        _logger.LogInformation("Subscription completed for id: {Id}", messageId);
                        break;

                    case "ping":
                        // Send pong response
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await SendMessageAsync(JsonConvert.SerializeObject(new { type = "pong" }), CancellationToken.None);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to send pong");
                            }
                        });
                        break;

                    default:
                        _logger.LogDebug("Received unknown message type: {Type}", messageType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket message");
            }
        }

        private void ProcessSubscriptionData(JObject message)
        {
            try
            {
                var payload = message["payload"]?["data"]?["lnInvoicePaymentStatus"];
                if (payload == null)
                {
                    _logger.LogWarning("No invoice payment status data in subscription message");
                    return;
                }

                var status = payload["status"]?.ToString();
                var errors = payload["errors"]?.ToObject<List<JObject>>();

                if (errors != null && errors.Any())
                {
                    var errorMessages = string.Join(", ", errors.Select(e => e["message"]?.ToString()));
                    _logger.LogError("Invoice status subscription error: {Errors}", errorMessages);
                    return;
                }

                if (!string.IsNullOrEmpty(status))
                {
                    _logger.LogInformation("Invoice status update received: {Status}", status);
                    
                    // Extract invoice ID from message ID (format: invoice_{invoiceId}_{messageId})
                    var messageId = message["id"]?.ToString();
                    if (messageId?.StartsWith("invoice_") == true)
                    {
                        var parts = messageId.Split('_');
                        if (parts.Length >= 2)
                        {
                            var invoiceId = parts[1];
                            
                            InvoiceUpdated?.Invoke(this, new InvoiceUpdateEventArgs
                            {
                                InvoiceId = invoiceId,
                                Status = status,
                                PaidAt = status == "PAID" ? DateTime.UtcNow : null
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing subscription data");
            }
        }

        public async Task DisconnectAsync()
        {
            _logger.LogInformation("Disconnecting WebSocket");

            _disconnectTokenSource?.Cancel();

            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing WebSocket");
                }
            }

            if (_receiveLoopTask != null)
            {
                try
                {
                    await _receiveLoopTask;
                }
                catch { }
            }

            await CleanupConnection();
        }

        private async Task CleanupConnection()
        {
            _subscribedInvoices.Clear();
            _disconnectTokenSource?.Dispose();
            _disconnectTokenSource = null;
            _webSocket?.Dispose();
            _webSocket = null;
            _receiveLoopTask = null;
        }

        public void Dispose()
        {
            _ = DisconnectAsync();
            _sendLock?.Dispose();
        }
    }
}