# BTCPayServer Flash Plugin Debugging Guide

## Plugin Not Loading Issue

If the Flash plugin is not showing up in the list of plugins in BTCPayServer logs, it suggests there might be an installation problem. The plugin should appear in the logs like this:

```
info: BTCPayServer.Plugins.PluginManager: Adding and executing plugin BTCPayServer.Plugins.Flash - 1.3.5
```

If it doesn't appear at all, follow these steps to diagnose and fix the issue:

## 1. Check Plugin Directory

BTCPayServer looks for plugins in the following directory:
```
/root/.btcpayserver/Plugins
```

Inside this directory, each plugin should have its own folder. For Flash, it should be:
```
/root/.btcpayserver/Plugins/BTCPayServer.Plugins.Flash/
```

## 2. Use Direct Installation

The `direct-install.sh` script in this repository will install the plugin directly into the correct directory in your BTCPayServer Docker container:

```bash
# Make sure you're in the plugin directory
cd /Users/dread/Documents/Island-Bitcoin/Flash/claude/BTCPayServer.Plugins.Flash

# Run the installation script
./direct-install.sh
```

This script will:
1. Find your running BTCPayServer container
2. Extract the plugin package
3. Copy the files directly to the correct plugin directory
4. Instruct you to restart the container

## 3. Check for Error Messages

After installation, restart BTCPayServer and check the logs for any error messages related to loading plugins:

```bash
# First get your container ID
docker ps | grep btcpayserver

# Then view the logs
docker logs <container-id> | grep -A 10 -B 10 "Loading plugins"
```

Look for any errors that might indicate why the Flash plugin isn't loading.

## 4. Check Plugin Files

Verify that all required files are present in the plugin directory:

```bash
docker exec <container-id> ls -la /root/.btcpayserver/Plugins/BTCPayServer.Plugins.Flash
```

The directory should contain:
- BTCPayServer.Plugins.Flash.dll
- manifest.json
- Views directory with all view files
- Other supporting DLLs

## 5. Check Plugin Compatibility

Make sure the plugin is compatible with your version of BTCPayServer. This plugin is designed for BTCPayServer 2.0.0 and later.

## 6. Manual Installation Option

If all else fails, you can try installing the plugin manually:

1. Stop the BTCPayServer container
2. Create a bind mount to access the container's file system
3. Extract the plugin package and copy the files directly
4. Restart the container

## Contact for Support

If you continue to experience issues with the Flash plugin not loading, please reach out for support with the following information:

1. Your BTCPayServer version
2. Docker setup details
3. Complete logs showing the plugin loading attempt
4. Any error messages from the logs

This information will help troubleshoot the installation issues more effectively.