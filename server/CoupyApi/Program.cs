using System.Text.Json.Serialization;
using CoupyApi.Endpoints;
using CoupyApi.Engine;
using CoupyApi.Ingesters;
using CoupyApi.Storage;

var builder = WebApplication.CreateBuilder(args);

// JSON: camelCase + enums-as-strings, matching DataStore.JsonOptions.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
});

// HTTP clients for each ingester. Short timeouts — ingest is a scheduled job, not user-facing latency.
foreach (var name in new[] { "walgreens", "kroger", "rakuten-advertising", "flipp", "couponscom" })
{
    builder.Services.AddHttpClient(name, c => c.Timeout = TimeSpan.FromSeconds(20));
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Storage + engine as singletons (stateless computation, shared in-memory state).
builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<Seeder>();
builder.Services.AddSingleton<ProductMatcher>();
builder.Services.AddSingleton<NetPriceCalculator>();
builder.Services.AddSingleton<StackingEngine>();

// Ingesters: register by concrete type AND as IOfferIngester so we can enumerate all sources.
builder.Services.AddSingleton<WalgreensIngester>();
builder.Services.AddSingleton<KrogerIngester>();
builder.Services.AddSingleton<RakutenAdvertisingIngester>();
builder.Services.AddSingleton<FlippIngester>();
builder.Services.AddSingleton<CouponsComIngester>();
builder.Services.AddSingleton<IbottaIngester>();

builder.Services.AddSingleton<IOfferIngester>(sp => sp.GetRequiredService<WalgreensIngester>());
builder.Services.AddSingleton<IOfferIngester>(sp => sp.GetRequiredService<KrogerIngester>());
builder.Services.AddSingleton<IOfferIngester>(sp => sp.GetRequiredService<RakutenAdvertisingIngester>());
builder.Services.AddSingleton<IOfferIngester>(sp => sp.GetRequiredService<FlippIngester>());
builder.Services.AddSingleton<IOfferIngester>(sp => sp.GetRequiredService<CouponsComIngester>());
builder.Services.AddSingleton<IOfferIngester>(sp => sp.GetRequiredService<IbottaIngester>());

var app = builder.Build();

app.UseCors();

// Seed retailers + products from JSON at startup.
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<Seeder>();
    seeder.SeedAll();
}

// Auto-ingest all sources on boot so /api/stacks has data to compute against. Turn off in prod
// and drive via the scheduled-tasks skill or cron.
var autoIngest = app.Configuration.GetValue<bool>("COUPY_AUTO_INGEST", defaultValue: true);
if (autoIngest)
{
    using var scope = app.Services.CreateScope();
    var store = scope.ServiceProvider.GetRequiredService<DataStore>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    foreach (var ing in scope.ServiceProvider.GetServices<IOfferIngester>())
    {
        try
        {
            var offers = await ing.IngestAsync();
            store.UpsertOffersFromSource(ing.SourceName, offers);
            logger.LogInformation("Boot ingest {Source}: {Count} offers", ing.SourceName, offers.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Boot ingest failed for {Source}", ing.SourceName);
        }
    }
}

app.MapCoupyEndpoints();

app.Run();

// Make Program discoverable for integration tests later (WebApplicationFactory<Program>).
public partial class Program { }
