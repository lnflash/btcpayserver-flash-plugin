using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace BTCPayServer.Plugins.Flash
{
    // This class attempts to write directly to the file system
    // to diagnose issues when standard logging doesn't appear
    public static class FlashPluginLogger
    {
        private static readonly string LogFilePath = "/tmp/flash-plugin.log";

        static FlashPluginLogger()
        {
            try
            {
                var logMessage = new StringBuilder();
                logMessage.AppendLine("=============== FLASH PLUGIN DIAGNOSTIC LOG ===============");
                logMessage.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");

                // Assembly info
                var assembly = typeof(FlashPluginLogger).Assembly;
                logMessage.AppendLine($"Assembly: {assembly.FullName}");
                logMessage.AppendLine($"Location: {assembly.Location}");

                // Check for manifest
                logMessage.AppendLine("Checking for manifest resources:");
                var resources = assembly.GetManifestResourceNames();
                foreach (var resource in resources)
                {
                    logMessage.AppendLine($"  - {resource}");
                }

                // Check for manifest.json specifically
                var manifestPath = "BTCPayServer.Plugins.Flash.manifest.json";
                using var stream = assembly.GetManifestResourceStream(manifestPath);
                if (stream != null)
                {
                    logMessage.AppendLine($"Found manifest at {manifestPath}");

                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();
                    logMessage.AppendLine($"Manifest content: {content}");
                }
                else
                {
                    logMessage.AppendLine($"ERROR: Manifest not found at {manifestPath}");
                }

                // Write to filesystem
                File.WriteAllText(LogFilePath, logMessage.ToString());
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(LogFilePath, $"ERROR in FlashPluginLogger: {ex}");
                }
                catch
                {
                    // Last resort - nothing we can do if we can't write to the filesystem
                }
            }
        }

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogFilePath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}\n");
            }
            catch
            {
                // Ignore failures to avoid crashing the plugin
            }
        }
    }
}