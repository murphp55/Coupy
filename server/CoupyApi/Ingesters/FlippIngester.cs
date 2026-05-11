using CoupyApi.Models;

namespace CoupyApi.Ingesters;

/// <summary>
/// Flipp — weekly-ad aggregator. Critical for BASE PRICES: net-price math is meaningless
/// without knowing today's shelf/sale price per retailer.
///
/// No public developer API. Flipp's web app (flipp.com) drives off an internal JSON endpoint
/// at backflipp.wishabi.com that returns structured flyer items with price, unit, and validity.
/// Tractable via a browser-like HTTP client; subject to their ToS — use conservatively.
///
/// Config:
///   FLIPP_POSTAL_CODE      — required; base prices are store-local ("02139")
///   FLIPP_ENABLED          — set to "true" to call the real endpoint
///
/// Output note: this ingester produces "pseudo-offers" representing base-price drops vs
/// MSRP. The NetPriceCalculator treats these as part of the base price, not stackable
/// coupons — see Engine/NetPriceCalculator.cs for how they're folded in.
/// </summary>
public class FlippIngester : IOfferIngester
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FlippIngester> _logger;

    public string SourceName => "flipp";

    public FlippIngester(IHttpClientFactory httpFactory, IConfiguration config, IWebHostEnvironment env, ILogger<FlippIngester> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _env = env;
        _logger = logger;
    }

    public Task<IngesterStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var enabled = string.Equals(_config["FLIPP_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
        var postal = _config["FLIPP_POSTAL_CODE"];
        var missing = new List<string>();
        if (!enabled) missing.Add("FLIPP_ENABLED (must be 'true' to opt in)");
        if (string.IsNullOrWhiteSpace(postal)) missing.Add("FLIPP_POSTAL_CODE");

        return Task.FromResult(new IngesterStatus(
            SourceName,
            CredentialsConfigured: missing.Count == 0,
            UsesSampleData: missing.Count > 0,
            MissingCredentials: missing.Count == 0 ? null : string.Join(", ", missing),
            Notes: "No public API. Opt in explicitly via FLIPP_ENABLED=true because calls hit an unofficial endpoint. Primary value: per-retailer base prices for net-price math."));
    }

    public async Task<IReadOnlyList<Offer>> IngestAsync(CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (!status.CredentialsConfigured)
        {
            _logger.LogWarning("Flipp not enabled or missing postal code; using sample data");
            return SampleDataLoader.LoadOffersForSource(SourceName, _env, _logger);
        }

        // Production: call backflipp.wishabi.com/flipp/items/search?locale=en-us&postal_code={postal}&q={query}
        // and normalize items to price-drop "offers" keyed by retailer + product.
        // Skeleton: log and return sample data so the pipeline is runnable.
        _logger.LogInformation("Flipp ingest: real endpoint not wired in skeleton; returning sample data");
        await Task.CompletedTask;
        return SampleDataLoader.LoadOffersForSource(SourceName, _env, _logger);
    }
}
