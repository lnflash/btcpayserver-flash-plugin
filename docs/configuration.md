# Configuration

## Connection String Parameters

Required parameters:
- `type=flash` - Identifies this as a Flash Lightning connection
- `server=https://api.flashapp.me/graphql` - Flash API endpoint
- `token=fk_...` - A Flash API key from [console.flashapp.me](https://console.flashapp.me) (scopes `read_user`, `read_wallet`, `write_wallet`, `read_transactions`). Legacy `ory_st_...` session tokens also work but expire.

## Environment URLs

### Production
```
type=flash;server=https://api.flashapp.me/graphql;token=fk_...
```

### Test/Staging
```
type=flash;server=https://api.test.flashapp.me/graphql;token=fk_...
```

## Advanced Configuration

### Debug Mode

Enable debug logging by adding to `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "BTCPayServer.Plugins.Flash": "Debug"
    }
  }
}
```

### Log Locations
- **Linux**: `/var/log/btcpayserver/`
- **Docker**: `docker logs btcpayserver`
- **Windows**: `%APPDATA%\BTCPayServer\Logs`

## Features Configuration

### LNURL Support
LNURL features are automatically enabled when the plugin is installed:
- LNURL-pay: Accept payments via LNURL QR codes
- LNURL-withdraw: Enable withdrawals from your store
- Lightning Address: yourstore@domain.com support

### Boltcard NFC
Boltcard support is automatic when:
1. NFC plugin is installed
2. NFC reader is connected
3. Cards are properly programmed

### USD Wallet
USD wallet features:
- Automatic BTC/USD conversion at current exchange rates
- Stable value for accounting
- Minimum transaction: 1 cent USD

### WebSocket Updates
WebSocket connections are established automatically for:
- Real-time payment notifications
- Instant UI updates
- Connection health monitoring