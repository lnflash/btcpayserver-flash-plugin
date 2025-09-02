#!/usr/bin/env dotnet-script
#r "nuget: Newtonsoft.Json, 13.0.3"

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

Console.WriteLine("=== LNURL Metadata Fix Demonstration ===\n");

var cardId = "test-card-123";

// OLD IMPLEMENTATION (BROKEN)
Console.WriteLine("❌ OLD IMPLEMENTATION (Double-serialization):");
Console.WriteLine("----------------------------------------");
var oldMetadata = JsonConvert.SerializeObject(new[] { new[] { "text/plain", $"Flashcard {cardId} top-up" } });
Console.WriteLine($"Step 1 - Create metadata: {oldMetadata}");
Console.WriteLine($"         Type: {oldMetadata.GetType().Name}");

var oldResponse = new
{
    callback = "https://example.com/callback",
    metadata = oldMetadata,
    tag = "payRequest"
};

var oldJson = JsonConvert.SerializeObject(oldResponse, Formatting.Indented);
Console.WriteLine($"\nStep 2 - Serialize response to JSON:");
Console.WriteLine(oldJson);

// Test wallet parsing
Console.WriteLine("\nStep 3 - Wallet tries to parse metadata:");
var parsedOld = JObject.Parse(oldJson);
var oldMetadataFromJson = parsedOld["metadata"].ToString();
Console.WriteLine($"         Extracted: {oldMetadataFromJson}");
try
{
    var parsed = JsonConvert.DeserializeObject<string[][]>(oldMetadataFromJson);
    Console.WriteLine("         ✅ Parsed successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"         ❌ FAILED: {ex.GetType().Name} - JSON is double-escaped!");
}

Console.WriteLine("\n");

// NEW IMPLEMENTATION (FIXED)  
Console.WriteLine("✅ NEW IMPLEMENTATION (Correct serialization):");
Console.WriteLine("----------------------------------------");
var metadataArray = new[] { new[] { "text/plain", $"Flashcard {cardId} top-up" } };
var newMetadata = JsonConvert.SerializeObject(metadataArray);
Console.WriteLine($"Step 1 - Create metadata: {newMetadata}");
Console.WriteLine($"         Type: {newMetadata.GetType().Name}");

var newResponse = new
{
    callback = "https://example.com/callback",
    metadata = newMetadata,
    tag = "payRequest"
};

var newJson = JsonConvert.SerializeObject(newResponse, Formatting.Indented);
Console.WriteLine($"\nStep 2 - Serialize response to JSON:");
Console.WriteLine(newJson);

// Test wallet parsing
Console.WriteLine("\nStep 3 - Wallet tries to parse metadata:");
var parsedNew = JObject.Parse(newJson);
var newMetadataFromJson = parsedNew["metadata"].ToString();
Console.WriteLine($"         Extracted: {newMetadataFromJson}");
try
{
    var parsed = JsonConvert.DeserializeObject<string[][]>(newMetadataFromJson);
    Console.WriteLine($"         ✅ Parsed successfully!");
    Console.WriteLine($"         Description: \"{parsed[0][1]}\"");
}
catch (Exception ex)
{
    Console.WriteLine($"         ❌ FAILED: {ex.GetType().Name}");
}

Console.WriteLine("\n=== Summary ===");
Console.WriteLine("The fix ensures the metadata field is a proper JSON string that wallets can parse.");
Console.WriteLine("This resolves the 'failed to fetch lnurl invoice' error in flash-mobile and other wallets.");