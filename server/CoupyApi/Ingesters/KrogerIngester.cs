using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using CoupyApi.Models;

namespace CoupyApi.Ingesters;

/// <summary>
/// Kroger Public APIs — developer.kroger.com
///
/// Requires OAuth 2.0 client credentials (free to register):
///   KROGER_CLIENT_ID
///   KROGER_CLIENT_SECRET
///   KROGER_BASE_URL         — defaults to https://api.kroger.com/v1
///
/// Public APIs cover: Products, Locations, Cart (requires user auth), Identity.
/// Digital coupons are NOT exposed through the public API — they drive the Kroger app via
/// authenticated endpoints that require user login. A realistic coupon ingester wires both:
///   1. This class using client_credentials → Products catalog (UPC resolution, base prices)
///   2. A user-auth flow (authorization_code) to hit the app's coupon endpoints
///
/// Covers all Kroger banners: Fred Meyer, King Soopers, Ralphs, Harris Teeter,
/// Fry's, QFC, Smith's, Pick 'n Save, Owen's, Metro Market, City Market.
/// </summary>
public class KrogerIngester : IOfferIngester
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<KrogerIngester> _logger;

    public string SourceName => "kroger";

    public KrogerIngester(IHttpClientFactory httpFactory, IConfiguration config, IWebHostEnvironment env, ILogger<KrogerIngester> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _env = env;
        _logger = logger;
    }

    public Task<IngesterStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_config["KROGER_CLIENT_ID"])) missing.Add("KROGER_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(_config["KROGER_CLIENT_SECRET"])) missing.Add("KROGER_CLIENT_SECRET");

        return Task.FromResult(new IngesterStatus(
            SourceName,
            CredentialsConfigured: missing.Count == 0,
            UsesSampleData: missing.Count > 0,
            MissingCredentials: missing.Count == 0 ? null : string.Join(", ", missing),
            Notes: "Register at developer.kroger.com for a client_id/secret. Public API covers products + locations. Digital coupons require user-auth against the app endpoints (see Shmakov/kroger-cli for a working example)."));
    }

    public async Task<IReadOnlyList<Offer>> IngestAsync(CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (!status.CredentialsConfigured)
        {
            _logger.LogWarning("Kroger credentials missing; falling back to sample data");
            return SampleDataLoader.LoadOffersForSource(SourceName, _env, _logger);
        }

        // With valid client_credentials, the public API gives products but not coupons.
        // Production implementation: (1) get client_credentials token, (2) optionally prompt user
        // for authorization_code grant to access digital coupons. For skeleton we get the token
        // as a smoke-test and still return sample offers.
        var token = await GetClientCredentialsTokenAsync(ct);
        if (token is null)
        {
            _logger.LogWarning("Kroger token fetch failed; using sample data");
            return SampleDataLoader.LoadOffersForSource(SourceName, _env, _logger);
        }

        _logger.LogInformation("Kroger client_credentials token acquired; coupons still require user-auth. Using sample data.");
        return SampleDataLoader.LoadOffersForSource(SourceName, _env, _logger);
    }

    private async Task<string?> GetClientCredentialsTokenAsync(CancellationToken ct)
    {
        var clientId = _config["KROGER_CLIENT_ID"]!;
        var secret = _config["KROGER_CLIENT_SECRET"]!;
        var baseUrl = (_config["KROGER_BASE_URL"] ?? "https://api.kroger.com/v1").TrimEnd('/');

        var client = _httpFactory.CreateClient("kroger");
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/connect/oauth2/token")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Basic", creds) },
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = "product.compact"
            })
        };

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
        return payload?.AccessToken;
    }

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn);
}
