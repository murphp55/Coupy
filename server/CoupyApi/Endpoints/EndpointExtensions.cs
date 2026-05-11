using CoupyApi.Engine;
using CoupyApi.Ingesters;
using CoupyApi.Models;
using CoupyApi.Storage;

namespace CoupyApi.Endpoints;

/// <summary>
/// Minimal API route registration. Kept in one file for the skeleton — split by resource
/// once the surface grows.
/// </summary>
public static class EndpointExtensions
{
    public static void MapCoupyEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "CoupyApi" }));

        // --- Introspection ---

        app.MapGet("/api/sources", async (IEnumerable<IOfferIngester> ingesters, CancellationToken ct) =>
        {
            var statuses = new List<IngesterStatus>();
            foreach (var ing in ingesters)
                statuses.Add(await ing.GetStatusAsync(ct));
            return Results.Ok(statuses);
        });

        app.MapGet("/api/retailers", (DataStore store) =>
            Results.Ok(store.Retailers));

        app.MapGet("/api/products", (DataStore store) =>
            Results.Ok(store.Products));

        // --- Ingestion ---

        app.MapPost("/api/ingest/{source}", async (
            string source,
            IEnumerable<IOfferIngester> ingesters,
            DataStore store,
            CancellationToken ct) =>
        {
            var ing = ingesters.FirstOrDefault(i => string.Equals(i.SourceName, source, StringComparison.OrdinalIgnoreCase));
            if (ing is null) return Results.NotFound(new { message = $"No ingester named '{source}'." });

            var offers = await ing.IngestAsync(ct);
            store.UpsertOffersFromSource(ing.SourceName, offers);
            return Results.Ok(new { source = ing.SourceName, count = offers.Count });
        });

        app.MapPost("/api/ingest", async (
            IEnumerable<IOfferIngester> ingesters,
            DataStore store,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var summary = new List<object>();
            foreach (var ing in ingesters)
            {
                try
                {
                    var offers = await ing.IngestAsync(ct);
                    store.UpsertOffersFromSource(ing.SourceName, offers);
                    summary.Add(new { source = ing.SourceName, count = offers.Count, ok = true });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ingest failed for {Source}", ing.SourceName);
                    summary.Add(new { source = ing.SourceName, count = 0, ok = false, error = ex.Message });
                }
            }
            return Results.Ok(summary);
        });

        // --- Offers ---

        app.MapGet("/api/offers", (DataStore store, string? source, string? retailerId, CouponType? type) =>
        {
            var offers = store.Offers.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(source))
                offers = offers.Where(o => string.Equals(o.Source, source, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(retailerId))
                offers = offers.Where(o => string.Equals(o.RetailerId, retailerId, StringComparison.OrdinalIgnoreCase));
            if (type.HasValue)
                offers = offers.Where(o => o.Type == type.Value);
            return Results.Ok(offers.ToList());
        });

        // --- Stacks (the main value-delivering endpoint) ---

        app.MapGet("/api/stacks", (StackingEngine engine, int? topN, decimal? minPercentOffMsrp) =>
        {
            var stacks = engine.BestStacks(
                topN ?? 50,
                minPercentOffMsrp ?? 0m);
            return Results.Ok(stacks);
        });

        app.MapGet("/api/stacks/moneymakers", (StackingEngine engine) =>
        {
            var stacks = engine.BestStacks(topN: 200, minPercentOffMsrp: 0m)
                .Where(s => s.IsMoneymaker)
                .ToList();
            return Results.Ok(stacks);
        });
    }
}
