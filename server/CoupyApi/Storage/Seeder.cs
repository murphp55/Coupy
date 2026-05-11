using System.Text.Json;
using CoupyApi.Models;

namespace CoupyApi.Storage;

/// <summary>
/// Loads retailer rule configs (Config/retailers/*.json) and sample product catalog
/// (Data/samples/products.json) into the DataStore at startup.
/// </summary>
public class Seeder
{
    private readonly DataStore _store;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<Seeder> _logger;

    public Seeder(DataStore store, IWebHostEnvironment env, ILogger<Seeder> logger)
    {
        _store = store;
        _env = env;
        _logger = logger;
    }

    public void SeedAll()
    {
        SeedRetailers();
        SeedProducts();
    }

    private void SeedRetailers()
    {
        var dir = Path.Combine(_env.ContentRootPath, "Config", "retailers");
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("Retailer config dir not found: {Dir}", dir);
            return;
        }

        var retailers = new List<Retailer>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var retailer = JsonSerializer.Deserialize<Retailer>(json, DataStore.JsonOptions);
                if (retailer is not null)
                {
                    retailers.Add(retailer);
                    _logger.LogInformation("Loaded retailer rules: {Id}", retailer.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load retailer config {File}", file);
            }
        }

        _store.SetRetailers(retailers);
    }

    private void SeedProducts()
    {
        var path = Path.Combine(_env.ContentRootPath, "Data", "samples", "products.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Sample products file not found: {Path}", path);
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var products = JsonSerializer.Deserialize<List<Product>>(json, DataStore.JsonOptions) ?? new();
            _store.SetProducts(products);
            _logger.LogInformation("Seeded {Count} products", products.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed products");
        }
    }
}
