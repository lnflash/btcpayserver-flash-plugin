using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BTCPayServer.Plugins.Flash
{
    internal static class FlashPluginInitializer
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            // Module initialization happens before logging is available
            // Keep minimal diagnostic output for troubleshooting plugin loading issues
            Debug.WriteLine("Flash Plugin: Assembly loaded");
            
            try
            {
                // Verify manifest is embedded correctly
                var assembly = typeof(FlashPluginInitializer).Assembly;
                var manifestPath = "BTCPayServer.Plugins.Flash.manifest.json";
                
                using var stream = assembly.GetManifestResourceStream(manifestPath);
                if (stream == null)
                {
                    Debug.WriteLine($"Flash Plugin: WARNING - Could not find manifest resource at {manifestPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Flash Plugin: ERROR in initializer - {ex.Message}");
            }
        }
    }
}