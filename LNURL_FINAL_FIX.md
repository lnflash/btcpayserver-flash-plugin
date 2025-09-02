# Final LNURL Authorization Fix for Flash Plugin

## Problem Summary
The Flash plugin's LNURL-pay implementation was failing with "failed to fetch lnurl invoice" errors because the GraphQL HTTP requests were returning "Unauthorized" (401) errors, even though:
- The bearer token was valid
- WebSocket connections worked fine with the same token
- The token was being set in the HttpClient's DefaultRequestHeaders

## Root Cause Analysis

The GraphQL.Client library (v6.0.2) was not properly respecting the HttpClient's DefaultRequestHeaders when making requests. This is a known issue where the GraphQL client might override or not include headers that are set on the underlying HttpClient.

## The Solution

We implemented a two-part fix:

### 1. Custom Authorization Handler
Created `AuthorizationDelegatingHandler.cs` - a custom HTTP message handler that ensures the Authorization header is set on EVERY request:

```csharp
public class AuthorizationDelegatingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Always ensure the authorization header is set
        if (request.Headers.Authorization == null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
```

### 2. Updated FlashGraphQLService
Modified the service to use the authorization handler in the HTTP client pipeline:

```csharp
// Create a custom delegating handler that ensures authorization is always set
var authHandler = new AuthorizationDelegatingHandler(_bearerToken, _logger);
authHandler.InnerHandler = httpClientHandler;

// Use the auth handler when creating the HttpClient
_httpClient = new HttpClient(authHandler);
```

### 3. Updated FlashLNURLController
Modified the controller to use BTCPay Server's dependency injection:

```csharp
// Use the LightningClientFactoryService to create properly configured clients
var connectionString = $"type=flash;server={settings.ApiEndpoint};api-key={settings.BearerToken}";
var client = _lightningClientFactory.Create(connectionString, network);
```

## Why This Works

1. **AuthorizationDelegatingHandler**: Intercepts EVERY HTTP request before it's sent and ensures the Authorization header is present. This works at a lower level than the GraphQL client, so it can't be overridden.

2. **Handler Pipeline**: The request flows through: GraphQL Client → HttpClient → AuthorizationDelegatingHandler → HttpClientHandler → Network

3. **Logging**: The handler logs authorization header status for debugging, making it easier to diagnose future auth issues.

## Testing the Fix

After deploying:
1. Generate an LNURL for a flashcard
2. Scan with any LNURL-compatible wallet
3. The wallet should successfully:
   - Parse the LNURL discovery response
   - Request an invoice from the callback endpoint
   - Receive a properly generated invoice with valid authorization
   - Complete the payment

## Files Modified
- `/Controllers/FlashLNURLController.cs` - Updated to use dependency injection
- `/Services/FlashGraphQLService.cs` - Modified to use authorization handler
- `/Services/AuthorizationDelegatingHandler.cs` - New file to ensure auth headers

## Key Learnings

1. **GraphQL.Client v6 doesn't always respect HttpClient headers** - Need to use message handlers or interceptors to ensure headers are included
2. **WebSocket vs HTTP authentication** - These can behave differently even with the same token
3. **Delegating handlers are powerful** - They provide a reliable way to modify all HTTP requests
4. **Always use BTCPay's dependency injection** - The LightningClientFactoryService ensures proper initialization

## Debugging Tips

If authorization issues persist:
1. Check the logs for `[AuthHandler]` entries to see if headers are being set
2. Verify the token format and expiration
3. Ensure the Flash API endpoint is correct (prod vs test)
4. Check if the token has the necessary permissions for the requested operations