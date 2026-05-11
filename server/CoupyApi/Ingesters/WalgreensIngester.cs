using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CoupyApi.Models;

namespace CoupyApi.Ingesters;

/// <summary>
/// Walgreens Digital Offers API — developer.walgreens.com/apis
///
/// Requires partner credentials:
///   WALGREENS_API_KEY           — dev portal app key
///   WALGREENS_AFF_ID            — affiliate id tied to the key
///   WALGREENS_ENC_LOYALTY_ID    — your MyWalgreens encrypted loyalty id (from /offers/lookup/v1)
///   WALGREENS_BASE_URL          — defaults to services-qa.walgreens.com; prod is services.walgreens.com
///   WALGREENS_SVC_REQUESTOR     — channel id issued with your key ("ECOMMTP" in QA)
///
/// Endpoint shapes match the existing WalgreensOffersApi backend:
///   POST /api/offers/lookup/v1   — phone → encLoyaltyId
///   POST /api/offers/fetch/v1    — encLoyaltyId → paged offers
///   POST /api/offers/clip/v1     — clip an offer to the card
/// </summary>
public class WalgreensIngester : IOfferIngester
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WalgreensIngester> _logger;

    public string SourceName => "walgreens";

    public WalgreensIngester(IHttpClientFactory httpFactory, IConfiguration config, IWebHostEnvironment env, ILogger<WalgreensIngester> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _env = env;
        _logger = logger;
    }

    public Task<IngesterStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_config["WALGREENS_API_KEY"])) missing.Add("WALGREENS_API_KEY");
        if (string.IsNullOrWhiteSpace(_config["WALGREENS_AFF_ID"])) missing.Add("WALGREENS_AFF_ID");
        if (string.IsNullOrWhiteSpace(_config["WALGREENS_ENC_LOYALTY_ID"])) missing.Add("WALGREENS_ENC_LOYALTY_ID");

        return Task.FromResult(new IngesterStatus(
            SourceName,
            CredentialsConfigured: missing.Count == 0,
            UsesSampleData: missing.Count > 0,
            MissingCredentials: missing.Count == 0 ? null : string.Join(", ", missing),
            Notes: "Register at developer.walgreens.com for an app key. Use /offers/lookup/v1 with a phone number to get your encLoyaltyId."));
    }

    public async Task<IReadOnlyList<Offer>> IngestAsync(CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (!status.CredentialsConfigured)
        {
            _logger.LogWarning("Walgreens credentials missing; falling back to sample data");
            return SampleDataLoader.LoadOffersForSource(SourceName, _env, _logger);
        }

        var apiKey = _config["WALGREENS_API_KEY"]!;
        var affId = _config["WALGREENS_AFF_ID"]!;
        var encLoyaltyId = _config["WALGREENS_ENC_LOYALTY_ID"]!;
        var baseUrl = (_config["WALGREENS_BASE_URL"] ?? "https://services-qa.walgreens.com").TrimEnd('/');
        var svcRequestor = _config["WALGREENS_SVC_REQUESTOR"] ?? "ECOMMTP";
        var recSize = 50;

        var client = _httpFactory.CreateClient("walgreens");
        client.Timeout = TimeSpan.FromSeconds(20);

        var results = new List<Offer>();
        var startIndex = 0;

        for (var page = 0; page < 20; page++)
        {
            var body = new WgFetchRequest(apiKey, affId, svcRequestor, encLoyaltyId, "", recSize, startIndex);
            var response = await client.PostAsJsonAsync($"{baseUrl}/api/offers/fetch/v1", body, ct);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Walgreens fetch {Status}: {Detail}", (int)response.StatusCode, detail);
                break;
            }

            var payload = await response.Content.ReadFromJsonAsync<WgFetchResponse>(cancellationToken: ct);
            var coupons = payload?.Coupons ?? new();
            if (coupons.Count == 0) break;

            foreach (var c in coupons)
                results.Add(Normalize(c));

            var nextIndex = startIndex + recSize;
            var last = coupons[^1];
            if (int.TryParse(last.I, out var lastIdx)) nextIndex = lastIdx + 1;
            if (nextIndex <= startIndex) break;
            startIndex = nextIndex;
        }

        return results;
    }

    private Offer Normalize(WgCoupon c) => new()
    {
        Id = $"walgreens:{c.Id}",
        Source = SourceName,
        SourceOfferId = c.Id,
        Type = CouponType.DigitalClipped,
        DiscountType = DiscountType.AmountOff,
        Value = c.OfferValue ?? 0m,
        MinQty = c.MinQty ?? 1,
        BrandName = c.BrandName,
        ProductDescription = c.Description,
        CategoryName = c.CategoryName,
        Code = c.Code,
        EndDate = DateTimeOffset.TryParse(c.ExpiryDate, out var end) ? end : null,
        RetailerId = "walgreens"
    };

    // Minimal contract — matches existing WalgreensOffersApi shapes
    private record WgFetchRequest(
        [property: JsonPropertyName("apiKey")] string ApiKey,
        [property: JsonPropertyName("affId")] string AffId,
        [property: JsonPropertyName("svcRequestor")] string SvcRequestor,
        [property: JsonPropertyName("encLoyaltyId")] string EncLoyaltyId,
        [property: JsonPropertyName("cat")] string Cat,
        [property: JsonPropertyName("recSize")] int RecSize,
        [property: JsonPropertyName("recStartIndex")] int RecStartIndex);

    private record WgFetchResponse([property: JsonPropertyName("coupons")] List<WgCoupon>? Coupons);

    private record WgCoupon(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("brandName")] string? BrandName,
        [property: JsonPropertyName("categoryName")] string? CategoryName,
        [property: JsonPropertyName("offerValue")] decimal? OfferValue,
        [property: JsonPropertyName("minQty")] int? MinQty,
        [property: JsonPropertyName("expiryDate")] string? ExpiryDate,
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("i")] string? I);
}
