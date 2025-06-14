# Domain Compatibility Summary - v1.4.2

## Issue Resolved

The Flash plugin v1.4.1 and earlier contained hardcoded domains that prevented it from working properly on BTCPay Server instances other than the test environment. This has been fully resolved in v1.4.2.

## What Was Fixed

### 1. ✅ WebSocket Endpoints
**Before**: Hardcoded to `ws.test.flashapp.me` or `ws.flashapp.me`
**After**: Dynamically derived from API endpoint configuration

### 2. ✅ External Links
**Before**: Hardcoded to `https://flashapp.me`
**After**: Dynamically derived from connection string

### 3. ✅ Domain Detection Logic
**Before**: Simple test/production switch
**After**: Intelligent domain transformation:
- `api.domain.com` → `ws.domain.com`
- Supports localhost and IP addresses
- Handles custom domains properly

## How It Works Now

When you configure a connection string:
```
type=flash;server=https://api.mycustomflash.com/graphql;token=mytoken
```

The plugin automatically derives:
- **WebSocket**: `wss://ws.mycustomflash.com/graphql`
- **External Link**: `https://mycustomflash.com`

## Testing Scenarios

The plugin now works correctly in all these scenarios:

### ✅ Standard Flash Deployment
- API: `https://api.flashapp.me/graphql`
- WebSocket: `wss://ws.flashapp.me/graphql`
- External: `https://flashapp.me`

### ✅ Custom Domain
- API: `https://api.mycompany.com/graphql`
- WebSocket: `wss://ws.mycompany.com/graphql`
- External: `https://mycompany.com`

### ✅ Development (Localhost)
- API: `http://localhost:4000/graphql`
- WebSocket: `ws://localhost:4000/graphql`
- External: `http://localhost:4000`

### ✅ IP Address
- API: `http://192.168.1.100:4000/graphql`
- WebSocket: `ws://192.168.1.100:4000/graphql`
- External: `http://192.168.1.100:4000`

### ✅ Behind Reverse Proxy
- Works with any domain configuration
- Respects HTTPS/HTTP schemes
- Handles non-standard ports

## Version Information

- **Fixed in**: v1.4.2
- **Release Date**: June 13, 2025
- **Type**: Critical patch release

## Action Required

All users should upgrade to v1.4.2, especially if:
- Running BTCPay Server on a custom domain
- Using a self-hosted Flash instance
- Experiencing WebSocket connection failures
- External links pointing to wrong domain

## Installation

1. Download `BTCPayServer.Plugins.Flash-v1.4.2.btcpay`
2. Upload via BTCPay Server plugin manager
3. Restart BTCPay Server
4. Verify WebSocket connections work properly

## Verification

After upgrading, check the logs for:
```
Connecting to Flash WebSocket at wss://[your-domain]/graphql
```

The domain should match your Flash API configuration, not a hardcoded value.

## Support

If you experience any domain-related issues after upgrading:
1. Verify your connection string is correct
2. Check BTCPay Server logs for WebSocket connection messages
3. Report issues at: https://github.com/lnflash/btcpayserver-flash-plugin/issues