# Troubleshooting

## Common Issues

### Connection Failed
**Error**: "Failed to connect to Flash API"

**Solutions**:
1. Verify API token is correct
2. Check internet connectivity
3. Ensure Flash API is accessible
4. Try regenerating API token in Flash app

### Payment Not Detected
**Error**: "Invoice shows unpaid after payment"

**Solutions**:
1. Check WebSocket connection status in logs
2. Verify wallet has sufficient balance
3. Ensure invoice hasn't expired
4. Check Flash app for payment status

### Boltcard Not Working
**Error**: "Card tap not recognized"

**Solutions**:
1. Ensure NFC plugin is installed
2. Verify card is properly programmed
3. Check NFC reader compatibility
4. Test with Flash mobile app

### USD Conversion Issues
**Error**: "Invalid amount" or conversion errors

**Solutions**:
1. Ensure USD wallet exists in Flash account
2. Check minimum amount (1 cent USD)
3. Verify exchange rate service is running
4. Check for API rate limits

### WebSocket Disconnections
**Error**: "WebSocket connection lost"

**Solutions**:
1. Check network stability
2. Review firewall settings
3. Verify WebSocket endpoints are accessible
4. Check for proxy/reverse proxy configuration issues

## Debug Mode

Enable detailed logging:

1. Edit `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "BTCPayServer.Plugins.Flash": "Debug"
    }
  }
}
```

2. Restart BTCPayServer

3. Check logs for detailed error messages

## Log Analysis

### Key Log Patterns

**Successful connection**:
```
Flash Lightning client connected successfully
WebSocket connection established
```

**Payment received**:
```
Invoice [hash] marked as paid
Payment of [amount] sats received
```

**Connection issues**:
```
Failed to connect to Flash API: [error]
WebSocket reconnection attempt [n]
```

## Performance Issues

### Slow Payment Detection
1. Check WebSocket connection health
2. Verify server resources (CPU, RAM)
3. Review network latency to Flash API
4. Check for rate limiting

### High Memory Usage
1. Review log file sizes
2. Check for memory leaks in logs
3. Restart BTCPayServer service
4. Monitor WebSocket connection count

## Getting Help

1. **Enable debug logging** (see above)
2. **Collect logs** from the past hour
3. **Include details**:
   - BTCPayServer version
   - Plugin version
   - Error messages
   - Steps to reproduce
4. **Report issue** on [GitHub](https://github.com/Island-Bitcoin/BTCPayServer.Plugins.Flash/issues)