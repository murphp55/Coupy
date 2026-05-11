using System.Text.Json;
using System.Text.Json.Serialization;
using CoupyApi.Models;

namespace CoupyApi.Storage;

/// <summary>
/// In-memory store seeded from Config/retailers/*.json and Data/samples/*.json at startup.
/// Deliberately simple for the skeleton — swap for EF Core + SQLite/Postgres once the engine
/// stabilizes and you want incremental ingest + diff-based alerting.
/// </summary>
public class DataStore
{
    private readonly object _lock = new();
    private List<Retailer> _retailers = new();
    private List<Product> _products = new();
    private List<Offer> _offers = new();

    public IReadOnlyList<Retailer> Retailers
    {
        get { lock (_lock) return _retailers.ToList(); }
    }

    public IReadOnlyList<Product> Products
    {
        get { lock (_lock) return _products.ToList(); }
    }

    public IReadOnlyList<Offer> Offers
    {
        get { lock (_lock) return _offers.ToList(); }
    }

    public void SetRetailers(IEnumerable<Retailer> retailers)
    {
        lock (_lock) _retailers = retailers.ToList();
    }

    public void SetProducts(IEnumerable<Product> products)
    {
        lock (_lock) _products = products.ToList();
    }

    /// <summary>Replace offers from a given source (an ingest run for that source).</summary>
    public void UpsertOffersFromSource(string source, IEnumerable<Offer> offers)
    {
        lock (_lock)
        {
            _offers.RemoveAll(o => o.Source == source);
            _offers.AddRange(offers);
        }
    }

    public Retailer? GetRetailer(string id) =>
        Retailers.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
