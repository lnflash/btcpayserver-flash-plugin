# Flash Plugin v1.4.2 Release Notes

## Release Date: June 13, 2025

## Overview

This critical patch release makes the Flash plugin fully domain-agnostic, ensuring it works correctly on any BTCPay Server instance regardless of domain configuration.

## 🚨 Critical Fixes

### Domain Compatibility
- **Fixed**: Removed hardcoded WebSocket endpoints that were preventing the plugin from working with custom Flash API deployments
- **Fixed**: External link generation now dynamically derives from the configured connection string
- **Fixed**: Made the plugin fully domain-agnostic to work on any BTCPay Server instance

## 🔧 Technical Improvements

### WebSocket Connection Logic
The WebSocket endpoint is now intelligently derived from the API configuration:
- `api.domain.com` → `ws.domain.com`
- Localhost and IP addresses maintain the same host
- Automatic protocol selection (wss/ws) based on API scheme
- Supports custom Flash API deployments

### External Link Generation
- Links to Flash now derive from the connection string configuration
- No longer hardcoded to flashapp.me
- Properly handles various domain configurations

## 📦 Installation

1. Download `BTCPayServer.Plugins.Flash-v1.4.2.btcpay`
2. In BTCPay Server, go to Server Settings > Plugins
3. Click "Upload Plugin" and select the downloaded file
4. Restart BTCPay Server

## ⚙️ Configuration

For custom Flash deployments, configure your connection string:

```
type=flash;server=https://api.yourcustomflash.com/graphql;token=your-token
```

The plugin will automatically derive:
- WebSocket endpoint: `wss://ws.yourcustomflash.com/graphql`
- External link: `https://yourcustomflash.com`

## 🧪 Testing

This version has been tested with:
- Standard Flash API (api.flashapp.me)
- Custom domain deployments
- Localhost development environments
- BTCPay Server behind reverse proxies

## 📝 Changelog

### Fixed
- Critical: Removed hardcoded WebSocket endpoints
- External link generation now uses configured Flash instance
- Made plugin domain-agnostic for universal compatibility

### Changed
- WebSocket endpoint logic now dynamically derives from API configuration
- External links now derived from connection string configuration

### Technical Improvements
- Better support for custom Flash API deployments
- Improved domain detection and URL generation
- Enhanced compatibility with various hosting configurations

## 🙏 Acknowledgments

Thank you to the BTCPay Server community for testing and providing feedback on domain compatibility issues.

## 📞 Support

For issues or questions:
- GitHub Issues: https://github.com/lnflash/btcpayserver-flash-plugin/issues
- Flash Support: support@flashapp.me

---

**Note**: This release is critical for anyone running BTCPay Server on custom domains or using custom Flash API deployments. All users are encouraged to upgrade to ensure proper functionality.