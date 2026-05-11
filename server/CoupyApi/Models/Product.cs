namespace CoupyApi.Models;

/// <summary>
/// A product we care about. Populated by the product catalog ingester (or seeded by hand for a shortlist).
/// The BasePriceByRetailer map is the critical input for stack evaluation — without a known shelf/sale
/// price per retailer, we can't compute net out-of-pocket.
/// </summary>
public record Product
{
    public string Id { get; init; } = "";

    /// <summary>Primary UPC if known. Products may have multiple UPCs across sizes; we keep those in Variants.</summary>
    public string? Upc { get; init; }

    public string Brand { get; init; } = "";

    /// <summary>Short product name ("Colgate Total Toothpaste").</summary>
    public string Name { get; init; } = "";

    /// <summary>Size string as labeled ("4.8 oz", "12 ct"). Normalized size parsing lives in the matcher.</summary>
    public string? Size { get; init; }

    /// <summary>Category hint — aligns with retailer taxonomy when possible.</summary>
    public string? Category { get; init; }

    /// <summary>Manufacturer's suggested retail price. Used as a fallback when no retailer has a current price.</summary>
    public decimal MsrpPrice { get; init; }

    /// <summary>Current shelf/sale price per retailer. Keyed by Retailer.Id.</summary>
    public Dictionary<string, decimal> BasePriceByRetailer { get; init; } = new();

    /// <summary>Other UPCs that refer to the same product (different sizes, variants).</summary>
    public List<string> AlternateUpcs { get; init; } = new();
}
