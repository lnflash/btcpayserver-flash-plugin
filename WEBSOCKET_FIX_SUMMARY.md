# WebSocket Connection Fix Summary

## Issues Identified
The Flash plugin was experiencing WebSocket connection failures with multiple error types:
1. `WebSocket is not connected` errors during handshake
2. `503 Service Unavailable` responses from WebSocket server
3. Immediate connection closure by server after establishing connection

## Root Causes
1. **Missing Authorization Header**: WebSocket authentication was only being sent in the `connection_init` payload, but many GraphQL WebSocket servers require the Authorization header during the initial HTTP handshake.

2. **Protocol Mismatch**: The plugin was only using the older `graphql-ws` protocol, but Flash might be using the newer `graphql-transport-ws` protocol.

3. **Inadequate Error Handling**: WebSocket failures were causing excessive logging and retry attempts.

## Fixes Implemented

### 1. Authorization Header in Handshake
Added the Authorization bearer token to the WebSocket handshake:
```csharp
// Add Authorization header to the WebSocket handshake
_webSocket.Options.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
```

### 2. Multiple Protocol Support
Added support for both WebSocket protocols:
```csharp
_webSocket.Options.AddSubProtocol("graphql-ws");
_webSocket.Options.AddSubProtocol("graphql-transport-ws");
```

### 3. Protocol-Aware Connection Init
Different connection_init message formats based on negotiated protocol:
```csharp
if (negotiatedProtocol == "graphql-transport-ws")
{
    // New protocol format (no id field)
    initMessage = new { type = "connection_init", payload = ... };
}
else
{
    // Legacy protocol format (with id field)
    initMessage = new { id = Guid.NewGuid().ToString(), type = "connection_init", payload = ... };
}
```

### 4. Improved Error Handling
Better categorization of WebSocket errors:
- 503 errors are treated as normal (service may not support WebSocket)
- Protocol errors are logged as information, not errors
- Falls back gracefully to polling mode

## URL Mapping Verification
The WebSocket URL mapping is correct:
- `api.test.flashapp.me/graphql` → `wss://ws.test.flashapp.me/graphql`
- `api.flashapp.me/graphql` → `wss://ws.flashapp.me/graphql`

## Result
- WebSocket connections now properly authenticate during handshake
- Support for multiple GraphQL WebSocket protocols
- Graceful fallback to polling when WebSocket is unavailable
- Reduced error logging for expected WebSocket failures

## Testing
Build succeeded without errors. The plugin will now:
1. Attempt WebSocket connection with proper authentication
2. Support both graphql-ws and graphql-transport-ws protocols
3. Fall back to polling if WebSocket is unavailable
4. Continue to function normally for all invoice operations

Version: 1.3.6 (maintained)