# Flash WebSocket Mutation Issue

## Problem Summary
The Flash plugin cannot create invoices due to authentication limitations:

1. **HTTP Requests Fail**: Ory session tokens (`ory_st_*`) return 401 Unauthorized for HTTP GraphQL requests
2. **WebSocket Limitation**: Flash WebSocket only accepts subscriptions, not mutations
   - Error: "Only subscription operations are supported"
3. **No API Token Support**: Flash only provides Ory session tokens, not API tokens

## Current Status
- WebSocket connects successfully with Ory token
- WebSocket can receive subscription updates
- WebSocket CANNOT send mutations (including `lnInvoiceCreate`)
- HTTP requests with Ory tokens are blocked by nginx (401)

## Technical Details
From the logs:
```
info: BTCPayServer.Plugins.Flash.Services.FlashWebSocketService: Successfully connected to Flash WebSocket
fail: BTCPayServer.Plugins.Flash.Services.FlashWebSocketService: WebSocket error: [
        {
          "message": "Only subscription operations are supported"
        }
      ]
```

## Potential Solutions

### 1. Server-Side Changes Required (Flash Backend)
The Flash backend would need to:
- Enable mutations over WebSocket
- OR provide API token generation endpoint
- OR accept Ory tokens for HTTP requests

### 2. Alternative Approaches

#### Option A: Use Flash Mobile App's Approach
The Flash mobile app uses Breez SDK directly for Lightning operations, not the Flash GraphQL API.
This would require significant refactoring.

#### Option B: Proxy Through Flash Backend
Create a server-side proxy that:
1. Accepts requests with Ory tokens
2. Has its own API token for Flash
3. Forwards requests to Flash API

#### Option C: Request Flash API Changes
Contact Flash team to:
1. Enable mutations over WebSocket for authenticated connections
2. OR provide an API token generation endpoint accessible with Ory tokens
3. OR configure nginx to accept Ory tokens for GraphQL HTTP requests

## Testing Notes
- The same boltcard can be used for testing without reprogramming
- The issue occurs during invoice creation, not card reading
- WebSocket connection is stable and authenticated

## Recommendation
The most straightforward solution would be for the Flash backend to:
1. Accept GraphQL mutations over WebSocket (like Apollo Server typically does)
2. OR provide a way to exchange an Ory session token for an API token

Without server-side changes, the plugin cannot create invoices with the current authentication model.