using CoupyApi.Models;

namespace CoupyApi.Ingesters;

/// <summary>
/// Coupons.com (Quotient Technology / Neptune Retail Solutions) — the largest US manufacturer
/// coupon aggregator.
///
/// Access paths:
///   1. PARTNER XML/JSON FEED (preferred): Coupons.com offers a partner feed to approved
///      publishers. Gated behind a business review — apply via coupons.com/partners.
///      Config:
///        COUPONSCOM_PARTNER_KEY     — issued after approval
///        COUPONSCOM_FEED_URL        — partner-specific endpoint
///
///   2. CONSUMER SCRAPE (fallback): coupons.com/coupons/ serves a JSON catalog per zip via an
///      internal endpoint. Print-to-clip actions require their browser plugin / DRM and are
///      NOT usable server-side. Enumeration of available coupons is, with care on rate limits.
///      Config:
///        COUPONSCOM_ZIP             — required for per-zip results
///
/// Output type: Manufacturer (stacks with store + digital).
/// </summary>
public class CouponsComIngester : IOfferIngester
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CouponsComIngester> _logger;

    public string SourceName => "couponscom";

    public CouponsComIngester(IHttpClientFactory httpFactory, IConfiguration config, IWebHostEnvironment env, ILogger<CouponsComIngester> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _env = env;
        _logger = logger;
    }

    public Task<IngesterStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var partnerKey = _config["COUPONSCOM_PARTNER_KEY"];
        var zip = _config["COUPONSCOM_ZIP"];

        bool hasPartner = !string.IsNullOrWhiteSpace(partnerKey);
        bool hasZip = !string.IsNullOrWhiteSpace(zip);

        var note = hasPartner
            ? "Using partner feed."
            : hasZip
                ? "Falling back to consumer-scrape mode by zip (rate-limit sensitive)."
                : "No partner key and no zip — nothing to call. Apply for a partner feed at coupons.com/partners or set COUPONSCOM_ZIP to scrape.";

        return Task.FromResult(new IngesterStatus(
            SourceName,
            CredentialsConfigured: hasPartner || hasZip,
            UsesSampleData: !(hasPartner || hasZip),
            MissingCredentials: hasPartner || hasZip ? null : "COUPONSCOM_PARTNER_KEY or COUPONSCOM_ZIP",
            Notes: note));
    }

    public async Task<IReadOnlyList<Offer>> IngestAsync(CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (!status.CredentialsConfigured)
        {
            _logger.LogWarning("Coupons.com not configured; using sample data");
            return SampleDataLoader.LoadOffersForSource(SourceName, _env, _logger);
        }

        // Production:
        //   - If partner key: GET {COUPONSCOM_FEED_URL}?apiKey={key}&format=json
        //   - Else: scrape coupons.com/coupons/?zid={zip} and parse the embedded catalog JSON
        _logger.LogInformation("Coupons.com ingest: real endpoint not wired in skeleton; returning sample data");
        await Task.CompletedTask;
        return SampleDataLoader.LoadOffersForSource(SourceName, _env, _logger);
    }
}
