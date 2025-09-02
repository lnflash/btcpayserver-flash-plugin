# WebSocket Invoice Creation Solution for Ory Session Tokens

## Problem
Flash API only uses Ory session tokens (prefixed with `ory_st_`), which work for WebSocket connections but not for HTTP requests. This causes LNURL invoice creation to fail with 401 Unauthorized errors.

## Solution
Send GraphQL mutations over the existing WebSocket connection instead of using HTTP requests. Since the WebSocket connection authenticates successfully with Ory session tokens, we can leverage it for invoice creation.

## Implementation Details

### 1. Extended IFlashWebSocketService Interface
Added a new method for creating invoices via WebSocket:
```csharp
Task<InvoiceCreationResult?> CreateInvoiceAsync(long amountSats, string description, CancellationToken cancellation = default);
```

### 2. Modified FlashWebSocketService
- Implemented `CreateInvoiceAsync` to send GraphQL mutations over WebSocket
- Added `ProcessMutationResponse` to handle mutation responses
- Updated message processing to differentiate between subscription and mutation responses

### 3. Updated FlashLightningClient
Both `CreateInvoice` overloads now:
1. **First attempt**: Use WebSocket for invoice creation (works with Ory tokens)
2. **Second attempt**: Fall back to HTTP with FlashSimpleInvoiceService
3. **Final fallback**: Use the standard GraphQL service

## How It Works

1. When an invoice is requested, the client first checks if the WebSocket is connected
2. If not connected, it establishes a WebSocket connection using the Ory session token
3. The GraphQL mutation is sent over the WebSocket using the "subscribe" message type
4. The response is processed and converted to a LightningInvoice object
5. If WebSocket fails, it falls back to HTTP attempts (which likely won't work with Ory tokens)

## Key Files Modified

- `/Services/IFlashWebSocketService.cs` - Added invoice creation method and result class
- `/Services/FlashWebSocketService.cs` - Implemented WebSocket invoice creation
- `/FlashLightningClient.cs` - Updated both CreateInvoice methods to use WebSocket first

## Testing

After deploying, monitor logs for:
```
=== Attempting to use WebSocket for invoice creation ===
=== WebSocket not connected, connecting now ===
=== Creating invoice via WebSocket ===
[WebSocket] Invoice created successfully: PaymentHash=...
=== Successfully created invoice via WebSocket: ... ===
```

## Why This Works

1. **Ory Session Tokens**: These tokens are designed for real-time connections (WebSocket) not REST APIs
2. **GraphQL over WebSocket**: The GraphQL protocol supports sending all operations (queries, mutations, subscriptions) over WebSocket
3. **Existing Infrastructure**: We're leveraging the already-working WebSocket authentication mechanism

## Deployment

1. Build the package:
   ```bash
   ./build-package.sh
   ```

2. The package is located at:
   ```
   bin/Release/BTCPayServer.Plugins.Flash.btcpay
   ```

3. Upload to BTCPay Server and restart

## Expected Behavior

When LNURL invoice creation is triggered:
1. WebSocket connection is established with Ory token
2. Invoice is created via WebSocket mutation
3. Invoice is returned successfully to the LNURL client
4. Flashcard top-up completes successfully

## Limitations

- Requires a stable WebSocket connection
- Adds slight latency for initial connection establishment
- Falls back to HTTP which won't work with Ory tokens (but maintains backward compatibility)

## Future Improvements

Consider implementing:
1. Connection pooling for WebSocket connections
2. Keep-alive mechanism to maintain persistent connections
3. Request batching for multiple invoice creations
4. Caching of WebSocket connections per bearer token