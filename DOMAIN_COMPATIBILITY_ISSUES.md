# Domain Compatibility Issues & Fixes

## Critical Issues Found

### 1. ❌ Hardcoded WebSocket Endpoints
**Location**: `FlashLightningClient.cs` (lines 229, 233)
```csharp
wsEndpointBuilder.Host = "ws.test.flashapp.me";  // Test environment
wsEndpointBuilder.Host = "ws.flashapp.me";       // Production environment
```
**Problem**: WebSocket endpoints are hardcoded and won't work with other Flash API instances.

### 2. ⚠️ Default API Endpoints
Multiple files have hardcoded defaults:
- `FlashPluginSettings.cs`: `https://api.flashapp.me/graphql`
- `BoltcardTopupController.cs`: `https://api.flashapp.me/graphql`
- `FlashController.cs`: `https://api.flashapp.me/graphql`
- `UIFlashBoltcardController.cs`: `https://api.flashapp.me/graphql`

**Problem**: While overridable, these assume a specific Flash API server.

### 3. ❌ Hardcoded External Link
**Location**: `FlashLightningConnectionStringHandler.cs` (line 165)
```csharp
return "https://flashapp.me";
```
**Problem**: Returns a hardcoded link that won't be relevant for other deployments.

### 4. ⚠️ Test-Specific Comments
**Location**: `FlashLightningClient.cs` (line 3418)
```csharp
// This is for the specific test case at btcpay.test.flashapp.me
```
**Problem**: Indicates potential test-specific code.

## Required Fixes

### Fix 1: Dynamic WebSocket Endpoint Configuration

```csharp
// In FlashLightningClient.cs, replace hardcoded WebSocket logic with:

private string GetWebSocketEndpoint(string apiEndpoint)
{
    var uri = new Uri(apiEndpoint);
    var wsScheme = uri.Scheme == "https" ? "wss" : "ws";
    
    // Extract subdomain and domain from API endpoint
    var host = uri.Host;
    
    // Transform api.domain.com to ws.domain.com
    if (host.StartsWith("api."))
    {
        host = "ws." + host.Substring(4);
    }
    else
    {
        // Fallback: prepend ws. to the domain
        host = "ws." + host;
    }
    
    return $"{wsScheme}://{host}";
}
```

### Fix 2: Configuration-Based API Endpoints

```csharp
// Add to FlashPluginSettings.cs
public class FlashPluginSettings
{
    public string ApiEndpoint { get; set; } = "https://api.flashapp.me/graphql";
    public string WebSocketEndpoint { get; set; } // Auto-derived if null
    public string ExternalLinkUrl { get; set; } = "https://flashapp.me";
    
    public string GetWebSocketEndpoint()
    {
        if (!string.IsNullOrEmpty(WebSocketEndpoint))
            return WebSocketEndpoint;
            
        // Derive from API endpoint
        return DeriveWebSocketEndpoint(ApiEndpoint);
    }
}
```

### Fix 3: Dynamic LNURL Generation

```csharp
// Ensure LNURL generation uses the current BTCPay instance domain
public string GenerateLNURL(HttpContext context, string path)
{
    var request = context.Request;
    var scheme = request.IsHttps ? "https" : "http";
    var host = request.Host.Value;
    
    return $"{scheme}://{host}/{path}";
}
```

### Fix 4: Remove Test-Specific Code

Remove or make configurable any code that's specific to test environments.

## Implementation Checklist

- [ ] Update FlashLightningClient.cs to use dynamic WebSocket endpoints
- [ ] Add WebSocket endpoint configuration to settings
- [ ] Update all controllers to use configured API endpoints
- [ ] Make external link URL configurable
- [ ] Remove test-specific references
- [ ] Add domain detection utilities
- [ ] Update documentation with configuration instructions
- [ ] Test on multiple BTCPay Server instances

## Configuration Template

```json
{
  "Flash": {
    "ApiEndpoint": "https://api.yourflash.com/graphql",
    "WebSocketEndpoint": "wss://ws.yourflash.com",
    "ExternalLinkUrl": "https://yourflash.com"
  }
}
```

## Testing Requirements

1. Test on different domains:
   - Local development (http://localhost)
   - Test server with subdomain
   - Production server with custom domain
   - Server behind reverse proxy

2. Verify all features work:
   - Lightning payments
   - WebSocket connections
   - LNURL generation
   - Boltcard functionality
   - Pull payments

3. Check for any hardcoded references in:
   - JavaScript files
   - View files
   - Configuration files