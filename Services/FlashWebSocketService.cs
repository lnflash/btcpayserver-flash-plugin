#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.Flash.Models;
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
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1);
        private readonly HashSet<string> _subscribedInvoices = new HashSet<string>();
        private readonly Dictionary<string, string> _subscriptionToPaymentRequest = new();
        private string? _bearerToken;
        private Uri? _wsEndpoint;
        private readonly Dictionary<string, object> _pendingOperations = new();
        private int _messageId = 0;
        private int _reconnectAttempt = 0;
        private readonly WebSocketRetryPolicy _retryPolicy;
        private readonly WebSocketHealthMetrics _healthMetrics;
        private WebSocketConnectionState _connectionState = WebSocketConnectionState.Disconnected;
        private Timer? _pingTimer;
        private DateTime _lastPongReceived = DateTime.UtcNow;

        public bool IsConnected => _connectionState == WebSocketConnectionState.Connected;
        public WebSocketConnectionState ConnectionState => _connectionState;
        public WebSocketHealthMetrics HealthMetrics => _healthMetrics;
        
        public event EventHandler<InvoiceUpdateEventArgs>? InvoiceUpdated;
        public event EventHandler<PaymentReceivedEventArgs>? PaymentReceived;
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

        public FlashWebSocketService(ILogger<FlashWebSocketService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retryPolicy = new WebSocketRetryPolicy
            {
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromMinutes(2),
                BackoffMultiplier = 2.0,
                MaxJitter = TimeSpan.FromSeconds(3),
                MaxRetryAttempts = 10
            };
            _healthMetrics = new WebSocketHealthMetrics();
        }

        private void SetConnectionState(WebSocketConnectionState newState, string? reason = null, Exception? exception = null)
        {
            if (_connectionState == newState) return;
            
            var previousState = _connectionState;
            _connectionState = newState;
            
            _logger.LogInformation("WebSocket connection state changed from {PreviousState} to {CurrentState}. Reason: {Reason}", 
                previousState, newState, reason ?? "State transition");
            
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(
                previousState, newState, reason, exception));
        }

        public async Task ConnectAsync(string bearerToken, Uri websocketEndpoint, CancellationToken cancellation = default)
        {
            await _connectionLock.WaitAsync(cancellation);
            try
            {
                if (_connectionState == WebSocketConnectionState.Connected)
                {
                    _logger.LogDebug("WebSocket already connected");
                    return;
                }
                
                if (_connectionState == WebSocketConnectionState.Connecting || 
                    _connectionState == WebSocketConnectionState.Reconnecting)
                {
                    _logger.LogWarning("Connection attempt already in progress");
                    return;
                }

                _bearerToken = bearerToken;
                _wsEndpoint = websocketEndpoint;
                
                await ConnectInternalAsync(cancellation);
            }
            finally
            {
                _connectionLock.Release();
            }
        }
        
        private async Task ConnectInternalAsync(CancellationToken cancellation = default)
        {

            SetConnectionState(WebSocketConnectionState.Connecting, "Initiating connection");
            
            try
            {
                _webSocket = new ClientWebSocket();
                _webSocket.Options.AddSubProtocol("graphql-ws");
                _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                
                // For development/testing environments, bypass SSL certificate validation
                // This should only be used for testing with test.flashapp.me
                if (_wsEndpoint?.Host?.Contains("test.flashapp.me") == true)
                {
                    _logger.LogWarning("[WebSocket Init] Development environment detected - bypassing SSL certificate validation for {Host}", _wsEndpoint.Host);
                    _webSocket.Options.RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                }
                
                // Add Authorization header to the WebSocket handshake
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_bearerToken}");
                
                // Try different protocols - Flash might use graphql-transport-ws instead
                _webSocket.Options.AddSubProtocol("graphql-transport-ws");

                _logger.LogInformation("Connecting to Flash WebSocket at {Endpoint} (attempt #{Attempt})", 
                    _wsEndpoint, _reconnectAttempt + 1);
                    
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                timeoutCts.CancelAfter(_retryPolicy.ConnectionTimeout);
                
                await _webSocket.ConnectAsync(_wsEndpoint!, timeoutCts.Token);
                
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
                    _logger.LogInformation("Successfully connected to Flash WebSocket with protocol: {Protocol}", 
                        _webSocket?.SubProtocol ?? "none");
                    
                    // Connection successful - reset retry counter and update state
                    _reconnectAttempt = 0;
                    SetConnectionState(WebSocketConnectionState.Connected, "Connection established");
                    _healthMetrics.RecordConnectionEstablished();
                    
                    // Start ping timer
                    StartPingTimer();
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
                SetConnectionState(WebSocketConnectionState.Failed, "WebSocket protocol not supported", wsEx);
                throw;
            }
            catch (System.Net.WebSockets.WebSocketException wsEx) when (wsEx.Message.Contains("503"))
            {
                _logger.LogInformation("Flash WebSocket service temporarily unavailable (503). This is normal - using polling instead.");
                _healthMetrics.RecordError();
                await CleanupConnection();
                await HandleReconnectAsync("Service unavailable", wsEx, cancellation);
                throw;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                _logger.LogDebug("Connection attempt cancelled");
                SetConnectionState(WebSocketConnectionState.Disconnected, "Connection cancelled");
                await CleanupConnection();
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to Flash WebSocket: {ErrorType} - {Message}", 
                    ex.GetType().Name, ex.Message);
                _healthMetrics.RecordError();
                await CleanupConnection();
                await HandleReconnectAsync($"Connection failed: {ex.Message}", ex, cancellation);
                throw;
            }
        }
        
        private async Task HandleReconnectAsync(string reason, Exception? exception, CancellationToken cancellation)
        {
            if (cancellation.IsCancellationRequested)
            {
                SetConnectionState(WebSocketConnectionState.Disconnected, "Reconnection cancelled");
                return;
            }
            
            _reconnectAttempt++;
            
            if (_retryPolicy.MaxRetryAttempts > 0 && _reconnectAttempt > _retryPolicy.MaxRetryAttempts)
            {
                _logger.LogError("Maximum reconnection attempts ({MaxAttempts}) exceeded. Giving up.", 
                    _retryPolicy.MaxRetryAttempts);
                SetConnectionState(WebSocketConnectionState.Failed, "Maximum reconnection attempts exceeded", exception);
                return;
            }
            
            var delay = _retryPolicy.CalculateDelay(_reconnectAttempt);
            _logger.LogInformation("Scheduling reconnection attempt #{Attempt} after {Delay:F1} seconds. Reason: {Reason}", 
                _reconnectAttempt, delay.TotalSeconds, reason);
            
            SetConnectionState(WebSocketConnectionState.Reconnecting, reason, exception);
            _healthMetrics.RecordReconnect();
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, cancellation);
                    if (!cancellation.IsCancellationRequested)
                    {
                        await ConnectAsync(_bearerToken!, _wsEndpoint!, cancellation);
                        
                        // Re-subscribe to all previously subscribed invoices
                        var invoicesToResubscribe = _subscribedInvoices.ToList();
                        foreach (var invoiceId in invoicesToResubscribe)
                        {
                            try
                            {
                                await SubscribeToInvoiceUpdatesAsync(invoiceId, cancellation);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to re-subscribe to invoice {InvoiceId}", invoiceId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reconnection attempt failed");
                }
            }, cancellation);
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
            
            // Log connection_init without exposing the full token
            var safeMessage = messageJson;
            if (messageJson.Contains("Bearer"))
            {
                // Mask the token for logging - show only first 10 chars
                safeMessage = System.Text.RegularExpressions.Regex.Replace(
                    messageJson, 
                    @"Bearer\s+([\w\-_]+)", 
                    m => {
                        var token = m.Groups[1].Value;
                        return $"Bearer {token.Substring(0, Math.Min(10, token.Length))}...";
                    }
                );
            }
            _logger.LogInformation("Sending connection_init with protocol {Protocol}: {Message}", negotiatedProtocol, safeMessage);
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

        public async Task SubscribeToInvoiceUpdatesAsync(string paymentRequest, CancellationToken cancellation = default)
        {
            if (!IsConnected)
            {
                _logger.LogWarning("Cannot subscribe to invoice updates: WebSocket not connected");
                return;
            }

            if (_subscribedInvoices.Contains(paymentRequest))
            {
                _logger.LogDebug("Already subscribed to payment request {PaymentRequest}", paymentRequest);
                return;
            }

            var subscriptionId = $"payment_{++_messageId}";
            
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
                            paymentRequest = paymentRequest
                        }
                    }
                }
            };

            var messageJson = JsonConvert.SerializeObject(subscribeMessage);
            _logger.LogDebug("Subscribing to invoice updates: {Message}", messageJson);
            await SendMessageAsync(messageJson, cancellation);
            
            // Track the subscription
            _subscribedInvoices.Add(paymentRequest);
            _subscriptionToPaymentRequest[subscriptionId] = paymentRequest;

            _logger.LogInformation("Subscribed to updates for payment request {PaymentRequest} with subscription ID {SubscriptionId}", paymentRequest, subscriptionId);
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
        
        public async Task<InvoiceCreationResult?> CreateInvoiceAsync(long amountSats, string description, CancellationToken cancellation = default)
        {
            if (!IsConnected)
            {
                _logger.LogError("[WebSocket] Cannot create invoice: WebSocket not connected");
                return new InvoiceCreationResult 
                { 
                    Success = false, 
                    ErrorMessage = "WebSocket not connected" 
                };
            }

            try
            {
                _logger.LogInformation("[WebSocket] Creating invoice via WebSocket: {AmountSats} sats, Description: {Description}", 
                    amountSats, description);

                var mutationId = $"mutation_{++_messageId}";
                var tcs = new TaskCompletionSource<InvoiceCreationResult>();
                _pendingOperations[mutationId] = tcs;

                // Build the GraphQL mutation message for WebSocket
                var mutationMessage = new
                {
                    id = mutationId,
                    type = "subscribe", // GraphQL over WebSocket uses "subscribe" type even for mutations
                    payload = new
                    {
                        query = @"
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
                            }",
                        variables = new
                        {
                            input = new
                            {
                                amount = amountSats,
                                memo = description ?? "Payment"
                            }
                        }
                    }
                };

                var messageJson = JsonConvert.SerializeObject(mutationMessage);
                _logger.LogDebug("[WebSocket] Sending invoice creation mutation: {Message}", messageJson);
                
                await SendMessageAsync(messageJson, cancellation);
                
                // Wait for response with timeout
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                
                try
                {
                    var result = await tcs.Task.WaitAsync(cts.Token);
                    _logger.LogInformation("[WebSocket] Invoice created successfully: PaymentHash={PaymentHash}, Success={Success}", 
                        result.PaymentHash, result.Success);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogError("[WebSocket] Invoice creation timed out");
                    return new InvoiceCreationResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Request timed out" 
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WebSocket] Error creating invoice via WebSocket");
                return new InvoiceCreationResult 
                { 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
            finally
            {
                _pendingOperations.Remove($"mutation_{_messageId}");
            }
        }

        private void StartPingTimer()
        {
            _pingTimer?.Dispose();
            _pingTimer = new Timer(async _ =>
            {
                try
                {
                    if (IsConnected)
                    {
                        // Check if we've received a pong recently
                        var timeSinceLastPong = DateTime.UtcNow - _lastPongReceived;
                        if (timeSinceLastPong > TimeSpan.FromMinutes(2))
                        {
                            _logger.LogWarning("No pong received for {Minutes:F1} minutes. Connection may be stale.", 
                                timeSinceLastPong.TotalMinutes);
                        }
                        
                        // Send ping
                        await SendMessageAsync(JsonConvert.SerializeObject(new { type = "ping" }), CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send ping");
                }
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
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
                _healthMetrics.RecordMessageSent();
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
            var closeReason = "Unknown";
            Exception? closeException = null;

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
                        _healthMetrics.RecordMessageReceived();
                        ProcessMessage(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        closeReason = $"Server closed connection: {result.CloseStatus} - {result.CloseStatusDescription}";
                        _logger.LogInformation(closeReason);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                closeReason = "Receive loop cancelled";
                _logger.LogDebug(closeReason);
            }
            catch (WebSocketException wsEx) when (wsEx.Message.Contains("remote party closed"))
            {
                closeReason = "Remote party closed connection without proper handshake";
                closeException = wsEx;
                _logger.LogWarning(wsEx, closeReason);
                _healthMetrics.RecordError();
            }
            catch (Exception ex)
            {
                closeReason = $"Error in receive loop: {ex.Message}";
                closeException = ex;
                _logger.LogError(ex, "Error in WebSocket receive loop");
                _healthMetrics.RecordError();
            }

            // Handle disconnection
            if (_connectionState == WebSocketConnectionState.Connected)
            {
                if (cancellation.IsCancellationRequested)
                {
                    SetConnectionState(WebSocketConnectionState.Disconnecting, closeReason);
                }
                else
                {
                    // Unintentional disconnect - attempt reconnection
                    await HandleReconnectAsync(closeReason, closeException, CancellationToken.None);
                }
            }
        }

        private void ProcessMessage(string message)
        {
            try
            {
                var json = JObject.Parse(message);
                var messageType = json["type"]?.ToString();
                var messageId = json["id"]?.ToString();

                _logger.LogDebug("Processing message type: {Type}, id: {Id}, full message: {Message}", 
                    messageType, messageId, message);

                switch (messageType)
                {
                    case "connection_ack":
                        _logger.LogInformation("Received connection_ack from Flash WebSocket");
                        if (_pendingOperations.TryGetValue("connection_ack", out var ackTcsObj))
                        {
                            var ackTcs = ackTcsObj as TaskCompletionSource<bool>;
                            ackTcs?.SetResult(true);
                        }
                        break;

                    case "next":
                        // Check if this is a mutation response
                        if (messageId != null && messageId.StartsWith("mutation_"))
                        {
                            _logger.LogDebug("[WebSocket] Processing mutation response for {MessageId}: {Response}", 
                                messageId, json.ToString());
                            ProcessMutationResponse(json);
                        }
                        else
                        {
                            ProcessSubscriptionData(json);
                        }
                        break;

                    case "error":
                        var errorPayload = json["payload"];
                        if (errorPayload != null)
                        {
                            // Handle both array and object error formats
                            JToken errors = null;
                            if (errorPayload.Type == JTokenType.Array)
                            {
                                // If payload is an array, it's likely an array of errors
                                errors = errorPayload;
                            }
                            else if (errorPayload.Type == JTokenType.Object)
                            {
                                // If payload is an object, look for errors property
                                errors = errorPayload["errors"] ?? errorPayload;
                            }
                            else
                            {
                                errors = errorPayload;
                            }
                            
                            _logger.LogError("WebSocket error: {Error}", errors?.ToString());
                            
                            // Check if this is a mutation error response
                            if (messageId != null && messageId.StartsWith("mutation_") && 
                                _pendingOperations.TryGetValue(messageId, out var tcsObj))
                            {
                                var tcs = tcsObj as TaskCompletionSource<InvoiceCreationResult>;
                                var errorMessage = errors?.ToString() ?? "Unknown WebSocket error";
                                tcs?.SetResult(new InvoiceCreationResult
                                {
                                    Success = false,
                                    ErrorMessage = errorMessage
                                });
                            }
                        }
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
                                _logger.LogDebug("Sent pong response");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to send pong");
                            }
                        });
                        break;
                        
                    case "pong":
                        _lastPongReceived = DateTime.UtcNow;
                        _logger.LogDebug("Received pong response");
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

        private void ProcessMutationResponse(JObject message)
        {
            try
            {
                var messageId = message["id"]?.ToString();
                if (messageId == null || !_pendingOperations.TryGetValue(messageId, out var tcsObj))
                {
                    _logger.LogWarning("[WebSocket] Received mutation response for unknown ID: {MessageId}", messageId);
                    return;
                }

                var tcs = tcsObj as TaskCompletionSource<InvoiceCreationResult>;
                if (tcs == null)
                {
                    _logger.LogWarning("[WebSocket] Pending operation is not an InvoiceCreationResult TCS");
                    return;
                }

                // Check for GraphQL errors first
                var payloadErrors = message["payload"]?["errors"];
                if (payloadErrors != null && payloadErrors.HasValues)
                {
                    var errorMessage = payloadErrors[0]?["message"]?.ToString() ?? "Unknown GraphQL error";
                    _logger.LogError("[WebSocket] GraphQL error in mutation response: {Error}", errorMessage);
                    tcs.SetResult(new InvoiceCreationResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage
                    });
                    return;
                }

                // Now check the mutation response structure
                var data = message["payload"]?["data"]?["lnInvoiceCreate"];
                if (data == null)
                {
                    _logger.LogError("[WebSocket] No lnInvoiceCreate data in mutation response");
                    tcs.SetResult(new InvoiceCreationResult
                    {
                        Success = false,
                        ErrorMessage = "No lnInvoiceCreate data in response"
                    });
                    return;
                }
                
                // Check for application-level errors in the response
                var applicationErrors = data["errors"];
                if (applicationErrors != null && applicationErrors.HasValues)
                {
                    var errorMessage = applicationErrors[0]?["message"]?.ToString() ?? "Unknown application error";
                    _logger.LogError("[WebSocket] Application error in invoice creation: {Error}", errorMessage);
                    tcs.SetResult(new InvoiceCreationResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage
                    });
                    return;
                }

                // Extract the invoice data
                var invoiceData = data["invoice"];
                if (invoiceData == null)
                {
                    _logger.LogError("[WebSocket] No invoice data in lnInvoiceCreate response");
                    tcs.SetResult(new InvoiceCreationResult
                    {
                        Success = false,
                        ErrorMessage = "No invoice data in response"
                    });
                    return;
                }

                var result = new InvoiceCreationResult
                {
                    PaymentHash = invoiceData["paymentHash"]?.ToString() ?? string.Empty,
                    PaymentRequest = invoiceData["paymentRequest"]?.ToString() ?? string.Empty,
                    PaymentSecret = invoiceData["paymentSecret"]?.ToString(),
                    Satoshis = invoiceData["satoshis"]?.Value<long>() ?? 0,
                    Success = true
                };

                _logger.LogInformation("[WebSocket] Invoice created successfully via mutation: PaymentHash={PaymentHash}, Amount={Amount} sats", 
                    result.PaymentHash, result.Satoshis);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[WebSocket] Error processing mutation response: {Message}", message.ToString());
                
                var messageId = message["id"]?.ToString();
                if (messageId != null && _pendingOperations.TryGetValue(messageId, out var tcsObj))
                {
                    var tcs = tcsObj as TaskCompletionSource<InvoiceCreationResult>;
                    tcs?.SetResult(new InvoiceCreationResult
                    {
                        Success = false,
                        ErrorMessage = $"Error processing response: {ex.Message}"
                    });
                }
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
                    
                    // Get payment request from subscription mapping
                    var messageId = message["id"]?.ToString();
                    if (messageId != null && _subscriptionToPaymentRequest.TryGetValue(messageId, out var paymentRequest))
                    {
                        _logger.LogInformation("Payment status update for payment request: {PaymentRequest}, Status: {Status}", paymentRequest, status);
                        
                        InvoiceUpdated?.Invoke(this, new InvoiceUpdateEventArgs
                        {
                            InvoiceId = paymentRequest, // This will be the BOLT11 payment request
                            Status = status,
                            PaidAt = status == "PAID" ? DateTime.UtcNow : null
                        });
                    }
                    else
                    {
                        _logger.LogWarning("Received payment status update for unknown subscription ID: {MessageId}", messageId);
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
            await _connectionLock.WaitAsync();
            try
            {
                if (_connectionState == WebSocketConnectionState.Disconnected || 
                    _connectionState == WebSocketConnectionState.Failed)
                {
                    return;
                }
                
                _logger.LogInformation("Disconnecting WebSocket");
                SetConnectionState(WebSocketConnectionState.Disconnecting, "User requested disconnect");

                // Cancel the receive loop
                _disconnectTokenSource?.Cancel();
                
                // Stop ping timer
                _pingTimer?.Dispose();
                _pingTimer = null;

                // Close the WebSocket connection gracefully
                if (_webSocket?.State == WebSocketState.Open)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", cts.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error closing WebSocket gracefully");
                    }
                }

                // Wait for receive loop to complete
                if (_receiveLoopTask != null)
                {
                    try
                    {
                        await _receiveLoopTask.ConfigureAwait(false);
                    }
                    catch { }
                }

                await CleanupConnection();
                SetConnectionState(WebSocketConnectionState.Disconnected, "Disconnected successfully");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task CleanupConnection()
        {
            try
            {
                // Clear pending operations
                foreach (var operation in _pendingOperations.Values)
                {
                    if (operation is TaskCompletionSource<bool> boolTcs)
                    {
                        boolTcs.TrySetCanceled();
                    }
                    else if (operation is TaskCompletionSource<InvoiceCreationResult> invoiceTcs)
                    {
                        invoiceTcs.TrySetCanceled();
                    }
                }
                _pendingOperations.Clear();
                
                // Clear subscriptions
                _subscribedInvoices.Clear();
                _subscriptionToPaymentRequest.Clear();
                
                // Dispose resources
                _disconnectTokenSource?.Dispose();
                _disconnectTokenSource = null;
                
                _pingTimer?.Dispose();
                _pingTimer = null;
                
                if (_webSocket != null)
                {
                    _webSocket.Dispose();
                    _webSocket = null;
                }
                
                _receiveLoopTask = null;
                
                // Reset message ID counter for next connection
                _messageId = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection cleanup");
            }
        }

        public void Dispose()
        {
            try
            {
                // Use synchronous disconnect for disposal
                if (_connectionState != WebSocketConnectionState.Disconnected)
                {
                    DisconnectAsync().GetAwaiter().GetResult();
                }
                
                _sendLock?.Dispose();
                _connectionLock?.Dispose();
                _pingTimer?.Dispose();
                _webSocket?.Dispose();
                _disconnectTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }
        }
    }
}