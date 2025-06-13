# BTCPayServer.Plugins.Flash - Versioning Guide

## Understanding Plugin Versioning in BTCPayServer

The Flash plugin version is defined in multiple places, and all must be kept in sync for proper version reporting:

1. **FlashPlugin.cs**: 
   ```csharp
   public override Version Version => new Version(1, 3, 5);
   ```

2. **manifest.json**:
   ```json
   {
     "version": "1.3.5",
     ...
   }
   ```

3. **BTCPayServer.Plugins.Flash.csproj**:
   ```xml
   <PropertyGroup>
     <Version>1.3.5</Version>
     ...
   </PropertyGroup>
   ```

## The Critical Version Problem

**Most important**: The version in the `.csproj` file controls the actual assembly version that BTCPayServer uses to identify the plugin. If this doesn't match the other version numbers, the plugin will report the wrong version.

## Versioning Checklist

Before each release, ensure all three locations are updated:

1. **FlashPlugin.cs**: Update the Version property with the new version numbers.
2. **manifest.json**: Update the version string.
3. **BTCPayServer.Plugins.Flash.csproj**: Update the Version property in the PropertyGroup.

After updating all three locations, rebuild the plugin with:

```bash
./build-package.sh
```

## Testing Version Display

After installing the plugin, check the BTCPayServer logs for a line like:
```
info: BTCPayServer.Plugins.PluginManager: Adding and executing plugin BTCPayServer.Plugins.Flash - 1.3.5
```

If it shows an incorrect version (like 1.3.4 when you're releasing 1.3.5), check all three locations again to make sure they're consistent.

## Remember:

Always update all three version numbers together:
1. **FlashPlugin.cs**
2. **manifest.json**
3. **BTCPayServer.Plugins.Flash.csproj**

This ensures the plugin reports the correct version to BTCPayServer and in the logs.