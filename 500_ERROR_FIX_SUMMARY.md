# Flash Plugin 500 Error Fix Summary

## Issue Description
The user was experiencing a 500 error when accessing:
`https://btcpay.test.flashapp.me/plugins/flash/5H7rrG4zM7T7fpuDSPzaWH42hkctsA72NG1sntRVRAX7/payouts/dashboard`

## Root Causes Identified

### 1. Incorrect View Path Resolution (FIXED)
**Problem**: The FlashPayoutController was trying to return a view with an incorrect path:
```csharp
return View("~/Plugins/Flash/Views/PayoutDashboard.cshtml", viewModel);
```

**Solution**: Changed to use the standard view resolution:
```csharp
return View("PayoutDashboard", viewModel);
```

### 2. Missing ViewImports in Views Directory (FIXED)
**Problem**: The Views directory was missing a `_ViewImports.cshtml` file which could cause namespace resolution issues.

**Solution**: Created `/Views/_ViewImports.cshtml` with proper namespace imports.

### 3. Database Configuration Issue (FIXED)
**Problem**: The plugin was hardcoded to use an in-memory database which wouldn't persist data between restarts:
```csharp
options.UseInMemoryDatabase("FlashPlugin");
```

**Solution**: Updated to use BTCPay Server's database configuration:
```csharp
var dbContextFactory = provider.GetService<ApplicationDbContextFactory>();
if (dbContextFactory != null)
{
    dbContextFactory.ConfigureBuilder(options);
}
```

## Files Modified

1. `/Controllers/FlashPayoutController.cs` - Fixed view path
2. `/Views/_ViewImports.cshtml` - Created with proper imports
3. `/FlashPlugin.cs` - Updated database configuration

## Additional Recommendations

1. **Check BTCPay Server Logs**: The actual error details should be in the BTCPay Server logs. Check:
   - Docker logs: `docker logs btcpayserver_btcpayserver_1`
   - Or system logs depending on your deployment

2. **Verify Database Migration**: Ensure the database migration has run:
   - The FlashPayouts table should exist in the database
   - Check if there are any migration errors in the logs

3. **Verify Controller Registration**: The controller is registered in the DI container, but ensure:
   - The route is accessible
   - Authentication/authorization is working correctly

4. **Test the Fix**: After deploying these changes:
   - Restart BTCPay Server
   - Check if the database migration runs successfully
   - Try accessing the dashboard URL again

## Deployment Steps

1. Build the plugin package:
   ```bash
   ./build-package.sh
   ```

2. Upload the new plugin version to BTCPay Server

3. Restart BTCPay Server to apply changes

4. Monitor logs during startup for any errors

## Expected Behavior After Fix

- The dashboard should load without a 500 error
- The view should render with payout statistics and charts
- Data should persist between server restarts (no longer using in-memory DB)