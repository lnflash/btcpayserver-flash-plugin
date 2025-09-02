using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Flash.Tests
{
    /// <summary>
    /// Test to demonstrate the LNURL metadata format fix
    /// </summary>
    public class LnurlMetadataTest
    {
        public static void TestMetadataFormat()
        {
            var cardId = "test-card-123";
            
            // OLD IMPLEMENTATION (WRONG - Double serialization)
            Console.WriteLine("=== OLD IMPLEMENTATION (BROKEN) ===");
            var oldMetadata = JsonConvert.SerializeObject(new[] { new[] { "text/plain", $"Flashcard {cardId} top-up" } });
            var oldResponse = new
            {
                callback = "https://example.com/callback",
                maxSendable = 100000000L,
                minSendable = 1000L,
                metadata = oldMetadata,
                tag = "payRequest"
            };
            
            var oldJson = JsonConvert.SerializeObject(oldResponse, Formatting.Indented);
            Console.WriteLine(oldJson);
            Console.WriteLine("\nOld metadata value (double-serialized):");
            Console.WriteLine($"Type: {oldResponse.metadata.GetType().Name}");
            Console.WriteLine($"Value: {oldResponse.metadata}");
            
            // When this gets serialized to JSON, the metadata becomes a string with escaped quotes
            var parsedOld = JObject.Parse(oldJson);
            Console.WriteLine($"\nParsed metadata type: {parsedOld["metadata"].Type}");
            Console.WriteLine($"Parsed metadata value: {parsedOld["metadata"]}");
            
            Console.WriteLine("\n=== NEW IMPLEMENTATION (FIXED) ===");
            
            // NEW IMPLEMENTATION (CORRECT)
            var metadataArray = new[] { new[] { "text/plain", $"Flashcard {cardId} top-up" } };
            var metadataJson = JsonConvert.SerializeObject(metadataArray);
            
            var newResponse = new
            {
                callback = "https://example.com/callback",
                maxSendable = 100000000L,
                minSendable = 1000L,
                metadata = metadataJson,
                tag = "payRequest"
            };
            
            var newJson = JsonConvert.SerializeObject(newResponse, Formatting.Indented);
            Console.WriteLine(newJson);
            Console.WriteLine("\nNew metadata value (correct):");
            Console.WriteLine($"Type: {newResponse.metadata.GetType().Name}");
            Console.WriteLine($"Value: {newResponse.metadata}");
            
            var parsedNew = JObject.Parse(newJson);
            Console.WriteLine($"\nParsed metadata type: {parsedNew["metadata"].Type}");
            Console.WriteLine($"Parsed metadata value: {parsedNew["metadata"]}");
            
            // Test that wallets can parse the metadata
            Console.WriteLine("\n=== WALLET PARSING TEST ===");
            try
            {
                // This is how a wallet would parse the metadata
                var metadataString = parsedNew["metadata"].ToString();
                var metadataParsed = JsonConvert.DeserializeObject<string[][]>(metadataString);
                Console.WriteLine("✅ New format: Wallet can parse metadata successfully!");
                Console.WriteLine($"   Extracted description: {metadataParsed[0][1]}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to parse metadata: {ex.Message}");
            }
            
            // Also show what happens with the old format
            try
            {
                var oldMetadataString = parsedOld["metadata"].ToString();
                var oldMetadataParsed = JsonConvert.DeserializeObject<string[][]>(oldMetadataString);
                Console.WriteLine("Old format: Wallet can parse metadata");
            }
            catch (Exception)
            {
                Console.WriteLine("❌ Old format: Wallet CANNOT parse metadata (expected failure)");
            }
        }
        
        public static void Main(string[] args)
        {
            TestMetadataFormat();
        }
    }
}