# Flash Plugin Authentication Issue

## Problem
The Flash plugin is configured with a token that doesn't work for HTTP requests, though it works for WebSocket connections.

## Evidence
1. **Token**: `ory_st_SxyR8GFA7QI7cTOOV2Inr7SWUO3pIswX` (39 characters)
2. **WebSocket**: Successfully connects and authenticates
3. **HTTP Requests**: All fail with "401 Unauthorized"

## Analysis
The token appears to be an Ory session token (prefix `ory_st_`). This type of token may:
- Only work for WebSocket connections
- Require a different authentication flow for HTTP
- Need to be exchanged for an API token
- Be expired for HTTP but maintained for existing WebSocket connections

## Current Behavior
When LNURL requests come in:
1. Our fix correctly intercepts the invoice creation
2. `FlashSimpleInvoiceService` is called (confirmed by logs)
3. The HTTP request to Flash API fails with 401
4. Falls back to `FlashInvoiceService` which also fails with 401

## Solution Required
The store owner needs to:
1. **Get a valid API token** from Flash that works for HTTP requests
2. **Update the Flash Lightning configuration** in BTCPay Server with the correct token
3. The token should be a proper API token, not a session token

## How to Get a Valid Token
1. Log into Flash dashboard
2. Go to API settings or Developer settings
3. Generate an API token (not a session token)
4. The token should likely start with a different prefix than `ory_st_`
5. Update the Flash Lightning connection string in BTCPay Server

## Temporary Workaround
Until a valid HTTP token is obtained, the Flash plugin cannot create invoices via LNURL. The WebSocket connection works but is only useful for receiving updates, not creating invoices.

## Code Status
The LNURL fix is working correctly - it's successfully calling our simplified invoice service. The issue is purely an authentication/token problem.