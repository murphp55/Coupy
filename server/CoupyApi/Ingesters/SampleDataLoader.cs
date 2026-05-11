using System.Text.Json;
using CoupyApi.Models;
using CoupyApi.Storage;

namespace CoupyApi.Ingesters;

/// <summary>
/// Shared helper: when an ingester is missing credentials, it loads offers from
/// Data/samples/{sourceName}-offers.json so the engine can still compute stacks.
/// </summary>
public static class SampleDataLoader
{
    public static IReadOnlyList<Offer> LoadOffersForSource(string sourceName, IWebHostEnvironment env, ILogger logger)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "samples", $"{sourceName}-offers.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("No sample file for source {Source} at {Path}", sourceName, path);
            return Array.Empty<Offer>();
        }

        try
        {
            var json = File.ReadAllText(path);
            var offers = JsonSerializer.Deserialize<List<Offer>>(json, DataStore.JsonOptions) ?? new();
            logger.LogInformation("Loaded {Count} sample offers for {Source}", offers.Count, sourceName);
            return offers;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load sample offers for {Source}", sourceName);
            return Array.Empty<Offer>();
        }
    }
}
