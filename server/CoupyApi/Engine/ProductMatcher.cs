using CoupyApi.Models;
using CoupyApi.Storage;

namespace CoupyApi.Engine;

/// <summary>
/// Resolves an Offer to the set of Products it applies to. This is the hardest part of the system.
/// Strategies, in order of precision:
///   1. Exact UPC match — when the offer explicitly lists eligible UPCs (rare but ideal).
///   2. Brand + product description fuzzy match — most common path.
///   3. Brand-only fallback — matches every product from the brand (lowest precision).
///
/// Real-world improvements (not in skeleton):
///   - Size normalization ("4.8oz" / "4.8 fl oz" / "4.8 FL OZ" should be equivalent)
///   - Tokenized description matching with stop-word removal
///   - UPC-A/GTIN-13 normalization
///   - Trained embedding model for "Colgate Total" vs "Colgate Optic White"
/// </summary>
public class ProductMatcher
{
    private readonly DataStore _store;
    private readonly ILogger<ProductMatcher> _logger;

    public ProductMatcher(DataStore store, ILogger<ProductMatcher> logger)
    {
        _store = store;
        _logger = logger;
    }

    public IReadOnlyList<Product> MatchOffer(Offer offer) =>
        MatchOffer(offer, _store.Products);

    public IReadOnlyList<Product> MatchOffer(Offer offer, IReadOnlyList<Product> products)
    {
        // Strategy 1: exact UPC
        if (offer.EligibleUpcs.Any())
        {
            var upcSet = offer.EligibleUpcs.ToHashSet(StringComparer.Ordinal);
            var upcMatches = products
                .Where(p => (p.Upc is not null && upcSet.Contains(p.Upc)) ||
                             p.AlternateUpcs.Any(u => upcSet.Contains(u)))
                .ToList();
            if (upcMatches.Count > 0) return upcMatches;
        }

        // Strategy 2: brand + description token
        if (!string.IsNullOrWhiteSpace(offer.BrandName))
        {
            var brand = offer.BrandName!;
            var brandMatches = products
                .Where(p => string.Equals(p.Brand, brand, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (brandMatches.Count == 0) return Array.Empty<Product>();

            if (!string.IsNullOrWhiteSpace(offer.ProductDescription))
            {
                // Break the offer description into tokens and require at least one distinctive
                // token to appear in the product name. "Colgate Total 4.8oz" → tokens ["total", "4.8oz"]
                // with "colgate" dropped as a brand duplicate.
                var tokens = Tokenize(offer.ProductDescription)
                    .Where(t => !string.Equals(t, brand, StringComparison.OrdinalIgnoreCase))
                    .Where(t => t.Length >= 3)
                    .ToList();

                if (tokens.Count > 0)
                {
                    var refined = brandMatches
                        .Where(p => tokens.Any(t => p.Name.Contains(t, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    if (refined.Count > 0) return refined;
                }
            }

            return brandMatches;
        }

        // No match possible
        return Array.Empty<Product>();
    }

    private static IEnumerable<string> Tokenize(string input) =>
        input
            .Split(new[] { ' ', ',', '.', '/', '(', ')', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().TrimEnd('s'))
            .Where(s => s.Length > 0);
}
