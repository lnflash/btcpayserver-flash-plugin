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
            Console.WriteLine("=================== FLASH PLUGIN ASSEMBLY LOADED ===================");
            Console.WriteLine($"Flash Plugin: Assembly location: {typeof(FlashPluginInitializer).Assembly.Location}");

            try
            {
                // Check if manifest is embedded correctly
                var assembly = typeof(FlashPluginInitializer).Assembly;
                var manifestPath = "BTCPayServer.Plugins.Flash.manifest.json";

                using var stream = assembly.GetManifestResourceStream(manifestPath);
                if (stream != null)
                {
                    Console.WriteLine($"Flash Plugin: Successfully found manifest resource at {manifestPath}");

                    // Read the first 100 bytes of the manifest to verify content
                    var buffer = new byte[100];
                    var bytesRead = stream.Read(buffer, 0, buffer.Length);
                    var content = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    Console.WriteLine($"Flash Plugin: Manifest content preview: {content.Substring(0, Math.Min(50, content.Length))}...");
                }
                else
                {
                    Console.WriteLine($"Flash Plugin: ERROR - Could not find manifest resource at {manifestPath}");

                    // List all resources to help diagnose
                    var resources = assembly.GetManifestResourceNames();
                    Console.WriteLine($"Flash Plugin: Available resources ({resources.Length}):");
                    foreach (var resource in resources)
                    {
                        Console.WriteLine($"  - {resource}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Flash Plugin: ERROR in initializer - {ex.Message}");
                Console.WriteLine($"Flash Plugin: Stack trace - {ex.StackTrace}");
            }
        }
    }
}