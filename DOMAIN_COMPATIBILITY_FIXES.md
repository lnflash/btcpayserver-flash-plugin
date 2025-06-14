# Domain Compatibility Fixes Applied

## ✅ Fixed Issues

### 1. WebSocket Endpoint Dynamic Resolution
**File**: `FlashLightningClient.cs`
**Fix Applied**: WebSocket endpoints are now dynamically derived from the API endpoint:
- `api.domain.com` → `ws.domain.com`
- `localhost` or IP addresses keep the same host
- Other domains get `ws.` prefix
- Scheme is `wss` for HTTPS APIs, `ws` for HTTP

### 2. External Link Dynamic Generation
**File**: `FlashLightningConnectionStringHandler.cs`
**Fix Applied**: External links are now derived from the connection string:
- Extracts domain from API endpoint
- Removes `api.` prefix if present
- Falls back to default only if parsing fails

## ⚠️ Remaining Considerations

### 1. Default API Endpoints
While these are configurable, the defaults still point to `https://api.flashapp.me/graphql` in:
- `FlashPluginSettings.cs`
- `BoltcardTopupController.cs`
- `FlashController.cs`
- `UIFlashBoltcardController.cs`

**Recommendation**: These defaults are acceptable as they can be overridden via configuration.

### 2. Documentation Links
External documentation links in views point to:
- `https://flashapp.me/`
- `https://docs.flashapp.me/api`
- GitHub repository links

**Recommendation**: These are fine as they're documentation resources, not operational endpoints.

## ✅ Domain-Agnostic Features

### 1. LNURL Generation
- Uses current BTCPay Server domain from HTTP context
- No hardcoded domains found in LNURL logic

### 2. Callback URLs
- All internal routes use relative paths
- No absolute URLs in controller actions

### 3. Asset References
- All CSS/JS references use proper ASP.NET Core helpers
- No hardcoded asset domains

## 🧪 Testing Checklist

To ensure the plugin works on any domain:

1. **Test WebSocket Connection**
   - [ ] Verify WebSocket connects with custom API domain
   - [ ] Test with localhost development
   - [ ] Test with IP address
   - [ ] Test with subdomain setup

2. **Test External Links**
   - [ ] Verify "View on Flash" link uses correct domain
   - [ ] Check that it derives correctly from API endpoint

3. **Test LNURL Features**
   - [ ] Boltcard topup generates correct LNURL
   - [ ] Pull payment LNURL uses BTCPay domain
   - [ ] Lightning Address resolution works

4. **Test Different Configurations**
   - [ ] Custom Flash API instance
   - [ ] BTCPay behind reverse proxy
   - [ ] HTTPS and HTTP environments
   - [ ] Non-standard ports

## 📝 Configuration Guide

For custom Flash deployments, configure:

```json
{
  "ConnectionString": "type=flash;server=https://api.yourcustomflash.com/graphql;token=your-token"
}
```

The plugin will automatically derive:
- WebSocket endpoint: `wss://ws.yourcustomflash.com/graphql`
- External link: `https://yourcustomflash.com`

## ✅ Summary

The Flash plugin is now domain-agnostic and will work on any BTCPay Server instance with proper configuration. The critical hardcoded domain issues have been resolved, and the plugin will dynamically adapt to the configured Flash API endpoint.