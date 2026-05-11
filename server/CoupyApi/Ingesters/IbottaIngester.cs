using CoupyApi.Models;

namespace CoupyApi.Ingesters;

/// <summary>
/// Ibotta — post-purchase cashback. No consumer API; the Ibotta Performance Network (IPN)
/// is a B2B partner program with a multi-month onboarding and is not viable for personal use.
///
/// Realistic approach for a personal tool: MANUAL IMPORT. Users open Ibotta in their phone
/// or browser, visually review available rebates for items they already plan to buy, and
/// paste them into Data/samples/ibotta-offers.json (or upload via a future UI endpoint).
///
/// This ingester reads that file on demand and normalizes it.
/// </summary>
public class IbottaIngester : IOfferIngester
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<IbottaIngester> _logger;

    public string SourceName => "ibotta";

    public IbottaIngester(IWebHostEnvironment env, ILogger<IbottaIngester> logger)
    {
        _env = env;
        _logger = logger;
    }

    public Task<IngesterStatus> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult(new IngesterStatus(
            SourceName,
            CredentialsConfigured: true, // always "configured" — reads local file
            UsesSampleData: true,
            MissingCredentials: null,
            Notes: "No consumer API. Manually maintain Data/samples/ibotta-offers.json with the rebates you see in-app. Ibotta's IPN B2B program exists but requires a ~3-month partner onboarding."));

    public Task<IReadOnlyList<Offer>> IngestAsync(CancellationToken ct = default)
    {
        var offers = SampleDataLoader.LoadOffersForSource(SourceName, _env, _logger);
        return Task.FromResult(offers);
    }
}
