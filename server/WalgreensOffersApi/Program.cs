using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("walgreens", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/walgreens/offers", async (
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    string? encLoyaltyId) =>
{
    var apiKey = config["WALGREENS_API_KEY"];
    var affId = config["WALGREENS_AFF_ID"];
    var configuredLoyaltyId = config["WALGREENS_ENC_LOYALTY_ID"];
    var loyaltyIdToUse = string.IsNullOrWhiteSpace(encLoyaltyId)
        ? configuredLoyaltyId
        : encLoyaltyId;

    if (string.IsNullOrWhiteSpace(apiKey) ||
        string.IsNullOrWhiteSpace(affId) ||
        string.IsNullOrWhiteSpace(loyaltyIdToUse))
    {
        return Results.Problem(
            "Missing Walgreens configuration. Set WALGREENS_API_KEY, WALGREENS_AFF_ID, WALGREENS_ENC_LOYALTY_ID or pass encLoyaltyId.",
            statusCode: (int)HttpStatusCode.InternalServerError);
    }

    var baseUrl = config["WALGREENS_BASE_URL"] ?? "https://services-qa.walgreens.com";
    var svcRequestor = config["WALGREENS_SVC_REQUESTOR"] ?? "ECOMMTP";
    var appVer = config["WALGREENS_APP_VER"];
    var devInf = config["WALGREENS_DEV_INF"];
    var recSize = ReadInt(config["WALGREENS_REC_SIZE"], 50, 1, 50);
    var maxPages = ReadInt(config["WALGREENS_MAX_PAGES"], 20, 1, 100);

    var client = httpClientFactory.CreateClient("walgreens");
    var offers = new List<WalgreensOffer>();
    var startIndex = 0;

    for (var page = 0; page < maxPages; page++)
    {
        var body = new WalgreensFetchRequest(
            apiKey,
            affId,
            svcRequestor,
            loyaltyIdToUse,
            "",
            recSize,
            startIndex,
            appVer,
            devInf);

        var response = await client.PostAsJsonAsync(
            $"{baseUrl.TrimEnd('/')}/api/offers/fetch/v1",
            body);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            return Results.Problem(
                $"Walgreens fetch failed with {(int)response.StatusCode}.",
                statusCode: (int)response.StatusCode,
                detail: detail);
        }

        var payload = await response.Content.ReadFromJsonAsync<WalgreensFetchResponse>(JsonOptions);
        var coupons = payload?.Coupons ?? [];

        if (coupons.Count == 0)
        {
            break;
        }

        offers.AddRange(coupons);

        var nextIndex = startIndex + recSize;
        var last = coupons[^1];
        if (int.TryParse(last.I, out var lastIndex))
        {
            nextIndex = lastIndex + 1;
        }

        if (nextIndex <= startIndex)
        {
            break;
        }

        startIndex = nextIndex;
    }

    return Results.Ok(new WalgreensOffersEnvelope(offers, offers.Count));
});

app.MapPost("/api/walgreens/loyalty-lookup", async (
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    LoyaltyLookupRequest request) =>
{
    var apiKey = config["WALGREENS_API_KEY"];
    var affId = config["WALGREENS_AFF_ID"];

    if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(affId))
    {
        return Results.Problem(
            "Missing Walgreens configuration. Set WALGREENS_API_KEY and WALGREENS_AFF_ID.",
            statusCode: (int)HttpStatusCode.InternalServerError);
    }

    if (string.IsNullOrWhiteSpace(request.PhoneNumber))
    {
        return Results.BadRequest(new { message = "phoneNumber is required." });
    }

    var baseUrl = config["WALGREENS_BASE_URL"] ?? "https://services-qa.walgreens.com";
    var svcRequestor = config["WALGREENS_SVC_REQUESTOR"] ?? "ECOMMTP";
    var appVer = config["WALGREENS_APP_VER"];
    var devInf = config["WALGREENS_DEV_INF"];

    var client = httpClientFactory.CreateClient("walgreens");
    var body = new WalgreensLookupRequest(
        apiKey,
        affId,
        request.PhoneNumber,
        svcRequestor,
        appVer,
        devInf);

    var response = await client.PostAsJsonAsync(
        $"{baseUrl.TrimEnd('/')}/api/offers/lookup/v1",
        body);

    if (!response.IsSuccessStatusCode)
    {
        var detail = await response.Content.ReadAsStringAsync();
        return Results.Problem(
            $"Walgreens lookup failed with {(int)response.StatusCode}.",
            statusCode: (int)response.StatusCode,
            detail: detail);
    }

    var payload = await response.Content.ReadFromJsonAsync<WalgreensLookupResponse>(JsonOptions);
    return Results.Ok(payload);
});

app.Run();

static int ReadInt(string? value, int fallback, int min, int max)
{
    if (!int.TryParse(value, out var parsed))
    {
        return fallback;
    }

    if (parsed < min)
    {
        return min;
    }

    if (parsed > max)
    {
        return max;
    }

    return parsed;
}

static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNameCaseInsensitive = true,
};

record WalgreensFetchRequest(
    [property: JsonPropertyName("apiKey")] string ApiKey,
    [property: JsonPropertyName("affId")] string AffId,
    [property: JsonPropertyName("svcRequestor")] string SvcRequestor,
    [property: JsonPropertyName("encLoyaltyId")] string EncLoyaltyId,
    [property: JsonPropertyName("cat")] string Cat,
    [property: JsonPropertyName("recSize")] int RecSize,
    [property: JsonPropertyName("recStartIndex")] int RecStartIndex,
    [property: JsonPropertyName("appVer")] string? AppVer,
    [property: JsonPropertyName("devInf")] string? DevInf);

record WalgreensFetchResponse(
    [property: JsonPropertyName("summary")] WalgreensSummary? Summary,
    [property: JsonPropertyName("coupons")] List<WalgreensOffer> Coupons);

record WalgreensSummary(
    [property: JsonPropertyName("availableCount")] int? AvailableCount,
    [property: JsonPropertyName("totalRecords")] int? TotalRecords,
    [property: JsonPropertyName("targetedCount")] int? TargetedCount,
    [property: JsonPropertyName("maxClippingLimit")] int? MaxClippingLimit);

record WalgreensOffer(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("brandName")] string? BrandName,
    [property: JsonPropertyName("categoryName")] string? CategoryName,
    [property: JsonPropertyName("image")] string? Image,
    [property: JsonPropertyName("image2")] string? Image2,
    [property: JsonPropertyName("offerValue")] decimal? OfferValue,
    [property: JsonPropertyName("minQty")] int? MinQty,
    [property: JsonPropertyName("expiryDate")] string? ExpiryDate,
    [property: JsonPropertyName("activeDate")] string? ActiveDate,
    [property: JsonPropertyName("offerDisclaimer")] string? OfferDisclaimer,
    [property: JsonPropertyName("isJustForYou")] bool? IsJustForYou,
    [property: JsonPropertyName("sneakpeek")] bool? Sneakpeek,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("i")] string? I);

record WalgreensOffersEnvelope(
    [property: JsonPropertyName("offers")] List<WalgreensOffer> Offers,
    [property: JsonPropertyName("total")] int Total);

record LoyaltyLookupRequest(
    [property: JsonPropertyName("phoneNumber")] string PhoneNumber);

record WalgreensLookupRequest(
    [property: JsonPropertyName("apiKey")] string ApiKey,
    [property: JsonPropertyName("affId")] string AffId,
    [property: JsonPropertyName("phoneNumber")] string PhoneNumber,
    [property: JsonPropertyName("svcRequestor")] string SvcRequestor,
    [property: JsonPropertyName("appVer")] string? AppVer,
    [property: JsonPropertyName("devInf")] string? DevInf);

record WalgreensLookupResponse(
    [property: JsonPropertyName("phoneNumber")] string? PhoneNumber,
    [property: JsonPropertyName("messages")] List<WalgreensMessage>? Messages,
    [property: JsonPropertyName("matchProfiles")] List<WalgreensMatchProfile>? MatchProfiles);

record WalgreensMessage(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("type")] string? Type);

record WalgreensMatchProfile(
    [property: JsonPropertyName("loyaltyMemberId")] string? LoyaltyMemberId,
    [property: JsonPropertyName("loyaltyCardNumber")] string? LoyaltyCardNumber,
    [property: JsonPropertyName("firstName")] string? FirstName,
    [property: JsonPropertyName("lastName")] string? LastName,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("zipCode")] string? ZipCode);
