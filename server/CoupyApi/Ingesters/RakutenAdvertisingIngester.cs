using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CoupyApi.Models;

namespace CoupyApi.Ingesters;

/// <summary>
/// Rakuten Advertising Coupon Feed API — developers.rakutenadvertising.com
///
/// NOT the Rakuten cashback extension. This is the old LinkShare affiliate network.
/// Requires a publisher account (free but must be approved — can take days).
///   RAKUTEN_WEB_SERVICES_TOKEN   — from the publisher Developer Portal
///   RAKUTEN_SECURITY_TOKEN       — per-account token for some endpoints
///
/// Endpoint:
///   GET https://api.linksynergy.com/coupon/1.0?token={token}&amp;network=1&amp;category=1&amp;promotiontype=1
///
/// Returns structured coupon data (code, expiration, advertiser, category). Covers hundreds of
/// retailers — the single highest-value general-retail feed. Cleaner than scraping RetailMeNot.
/// </summary>
public class RakutenAdvertisingIngester : IOfferIngester
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<RakutenAdvertisingIngester> _logger;

    public string SourceName => "rakuten-advertising";

    public RakutenAdvertisingIngester(IHttpClientFactory httpFactory, IConfiguration config, IWebHostEnvironment env, ILogger<RakutenAdvertisingIngester> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _env = env;
        _logger = logger;
    }

    public Task<IngesterStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var token = _config["RAKUTEN_WEB_SERVICES_TOKEN"];
        var missing = string.IsNullOrWhiteSpace(token) ? "RAKUTEN_WEB_SERVICES_TOKEN" : null;

        return Task.FromResult(new IngesterStatus(
            SourceName,
            CredentialsConfigured: missing is null,
            UsesSampleData: missing is not null,
            MissingCredentials: missing,
            Notes: "Apply as a publisher at rakutenadvertising.com, get approved, then grab the Web Services Token from the Developer Portal. Single highest-value feed for general retail online codes."));
    }

    public async Task<IReadOnlyList<Offer>> IngestAsync(CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (!status.CredentialsConfigured)
        {
            _logger.LogWarning("Rakuten Advertising token missing; falling back to sample data");
            return SampleDataLoader.LoadOffersForSource(SourceName, _env, _logger);
        }

        var token = _config["RAKUTEN_WEB_SERVICES_TOKEN"]!;
        var baseUrl = _config["RAKUTEN_BASE_URL"] ?? "https://api.linksynergy.com";
        var client = _httpFactory.CreateClient("rakuten-advertising");

        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/coupon/1.0?network=1&resultsperpage=200");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Rakuten Advertising fetch {Status}", (int)response.StatusCode);
            return SampleDataLoader.LoadOffersForSource(SourceName, _env, _logger);
        }

        // Rakuten returns XML by default; ask for JSON or parse XML as needed.
        // Skeleton: parse a simple JSON shape when the endpoint supports it.
        var payload = await response.Content.ReadFromJsonAsync<RakutenResponse>(cancellationToken: ct);
        var coupons = payload?.Coupons ?? new();

        return coupons.Select(Normalize).ToList();
    }

    private Offer Normalize(RakutenCoupon c) => new()
    {
        Id = $"rakuten-advertising:{c.OfferId}",
        Source = SourceName,
        SourceOfferId = c.OfferId,
        Type = CouponType.PromoCode,
        DiscountType = DiscountType.AmountOff,
        Value = c.Discount ?? 0m,
        BrandName = c.AdvertiserName,
        ProductDescription = c.OfferDescription,
        CategoryName = c.Category,
        Code = c.CouponCode,
        StartDate = DateTimeOffset.TryParse(c.StartDate, out var s) ? s : null,
        EndDate = DateTimeOffset.TryParse(c.EndDate, out var e) ? e : null,
        RetailerId = null // promo codes apply to the advertiser's own store
    };

    private record RakutenResponse([property: JsonPropertyName("coupons")] List<RakutenCoupon>? Coupons);

    private record RakutenCoupon(
        [property: JsonPropertyName("offerid")] string? OfferId,
        [property: JsonPropertyName("advertisername")] string? AdvertiserName,
        [property: JsonPropertyName("offerdescription")] string? OfferDescription,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("couponcode")] string? CouponCode,
        [property: JsonPropertyName("discount")] decimal? Discount,
        [property: JsonPropertyName("startdate")] string? StartDate,
        [property: JsonPropertyName("enddate")] string? EndDate);
}
