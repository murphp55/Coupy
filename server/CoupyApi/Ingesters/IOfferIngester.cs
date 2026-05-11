using CoupyApi.Models;

namespace CoupyApi.Ingesters;

/// <summary>
/// Every per-source integration implements this. Ingesters are responsible for:
///   1. Checking whether credentials/config are present (via Status)
///   2. Making the actual HTTP call to the source (or loading sample data if credentials missing)
///   3. Normalizing the source-specific payload into <see cref="Offer"/> records
/// </summary>
public interface IOfferIngester
{
    /// <summary>Stable identifier — also used as Offer.Source.</summary>
    string SourceName { get; }

    /// <summary>Reports whether the ingester is ready to hit the real source.</summary>
    Task<IngesterStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Ingest offers. If credentials are missing, implementations fall back to sample data so
    /// the rest of the pipeline is testable end-to-end.
    /// </summary>
    Task<IReadOnlyList<Offer>> IngestAsync(CancellationToken ct = default);
}

public record IngesterStatus(
    string SourceName,
    bool CredentialsConfigured,
    bool UsesSampleData,
    string? MissingCredentials,
    string Notes);
