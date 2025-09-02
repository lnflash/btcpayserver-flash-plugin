# LNURL Authorization Fix for Flash Plugin

## Problem Identified
The "failed to fetch lnurl invoice" error was caused by an **authorization failure** when the LNURL callback tried to create an invoice. The logs showed:
```
GraphQL.Client.Http.GraphQLHttpRequestException: The HTTP request failed with status code Unauthorized
```

## Root Cause
The `FlashLNURLController` was creating a new `FlashLightningClient` instance directly in the callback method, which:
1. Didn't use BTCPay Server's dependency injection system
2. Created a client that wasn't properly initialized with the store's Lightning configuration
3. Failed to authenticate when trying to query wallet information from the Flash API

## The Fix

### Changes Made to FlashLNURLController.cs:

1. **Added proper dependency injection:**
   - Added `LightningClientFactoryService` to create properly configured Lightning clients
   - Added `BTCPayNetworkProvider` to get the network configuration

2. **Updated the callback method to use the factory:**
   ```csharp
   // Build the Flash Lightning connection string from settings
   var connectionString = $"type=flash;server={settings.ApiEndpoint};api-key={settings.BearerToken}";
   
   // Create the Lightning client using the factory with the connection string
   var client = _lightningClientFactory.Create(connectionString, network);
   ```

3. **Removed direct instantiation:**
   - No longer creating `FlashLightningClient` directly
   - Using BTCPay Server's factory service which ensures proper initialization

## Why This Works

The `LightningClientFactoryService`:
- Creates clients with proper authentication headers
- Ensures the client is initialized with the correct wallet configuration
- Uses the Flash connection string handler to properly construct the client
- Handles all the initialization that was missing when creating the client directly

## Testing
After deploying this fix:
1. Generate an LNURL for a flashcard
2. Scan with any LNURL-compatible wallet
3. The wallet should successfully:
   - Parse the LNURL discovery response
   - Request an invoice from the callback endpoint
   - Receive a properly generated invoice
   - Complete the payment

## Files Modified
- `/Controllers/FlashLNURLController.cs` - Updated to use dependency injection and factory service

## Key Learnings
- Always use BTCPay Server's dependency injection system for Lightning clients
- The `LightningClientFactoryService` handles proper client initialization
- Direct instantiation of Lightning clients bypasses important setup steps
- The Flash plugin requires proper authentication for all GraphQL API calls