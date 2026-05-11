namespace CoupyApi.Models;

/// <summary>
/// The canonical, source-agnostic coupon/offer model. Every ingester normalizes its
/// retailer-specific payload into this shape so the stacking engine can reason uniformly.
/// </summary>
public record Offer
{
    /// <summary>Stable internal id (sha256 of source + source_offer_id).</summary>
    public string Id { get; init; } = "";

    /// <summary>Ingester source name — "walgreens", "kroger", "rakuten-advertising", "ibotta", etc.</summary>
    public string Source { get; init; } = "";

    /// <summary>Original id in the source system (useful for dedupe + re-fetching details).</summary>
    public string? SourceOfferId { get; init; }

    /// <summary>What kind of coupon this is — drives stacking rules.</summary>
    public CouponType Type { get; init; }

    /// <summary>How the value is applied to a base price.</summary>
    public DiscountType DiscountType { get; init; }

    /// <summary>Dollar amount (for AmountOff, SpendThreshold), percent 0-100 (for PercentOff), or 0 for BOGO.</summary>
    public decimal Value { get; init; }

    /// <summary>For SpendThreshold offers: minimum basket spend to earn Value.</summary>
    public decimal? MinSpend { get; init; }

    /// <summary>Minimum quantity of matching items required.</summary>
    public int MinQty { get; init; } = 1;

    /// <summary>Manufacturer or brand name — used by the product matcher.</summary>
    public string? BrandName { get; init; }

    /// <summary>Free-text product description ("Colgate Total toothpaste 4.8oz").</summary>
    public string? ProductDescription { get; init; }

    /// <summary>Category hint from the source ("Beauty", "Grocery", "Household").</summary>
    public string? CategoryName { get; init; }

    /// <summary>Promo code, if any (applies mostly to online PromoCode type).</summary>
    public string? Code { get; init; }

    /// <summary>Offer valid from (inclusive).</summary>
    public DateTimeOffset? StartDate { get; init; }

    /// <summary>Offer valid through (inclusive).</summary>
    public DateTimeOffset? EndDate { get; init; }

    /// <summary>Retailer this offer is tied to. Null for manufacturer coupons (work anywhere).</summary>
    public string? RetailerId { get; init; }

    /// <summary>Known eligible UPCs, when the source provides them. Empty list means match by brand/description.</summary>
    public List<string> EligibleUpcs { get; init; } = new();

    /// <summary>Raw source payload as JSON string — kept for debugging / re-ingestion.</summary>
    public string? RawPayload { get; init; }

    public bool IsExpired(DateTimeOffset now) =>
        EndDate is { } end && end < now;

    public bool IsActive(DateTimeOffset now) =>
        (StartDate is null || StartDate <= now) && !IsExpired(now);
}
