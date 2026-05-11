namespace CoupyApi.Models;

/// <summary>
/// A retailer with its stacking rulebook. Rules are loaded from Config/retailers/*.json at startup.
/// Curate these by hand from each chain's published coupon policy — they change maybe twice a year.
/// </summary>
public record Retailer
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public StackingRules Rules { get; init; } = new();
}

/// <summary>
/// Encoded coupon policy. Read each retailer's public policy page before editing.
/// </summary>
public record StackingRules
{
    /// <summary>
    /// Per-item cap, keyed by CouponType name. Missing key = 0 (not allowed).
    /// Use a high number (e.g. 99) for "unlimited distinct".
    /// </summary>
    public Dictionary<string, int> PerItemLimits { get; init; } = new();

    /// <summary>
    /// Which CouponType values apply once per basket (not per item) — e.g. CVS $/$ off ExtraCare coupons.
    /// </summary>
    public List<string> OncePerBasket { get; init; } = new();

    /// <summary>
    /// Loyalty rewards that behave as tender, not as coupons
    /// (Walgreens Register Rewards, CVS ExtraBucks). Stacking engine excludes these from coupon limits.
    /// </summary>
    public List<string> RewardsAsTender { get; init; } = new();

    /// <summary>Whether post-purchase cashback apps (Ibotta, Fetch) stack on this retailer.</summary>
    public bool StackCashback { get; init; } = true;

    /// <summary>Whether portal cashback (Rakuten extension) applies — usually online-only.</summary>
    public bool SupportsPortalCashback { get; init; } = false;

    /// <summary>Human-readable policy notes — for operators, not the engine.</summary>
    public string? Notes { get; init; }

    /// <summary>URL of the retailer's official coupon policy (keep this fresh).</summary>
    public string? PolicyUrl { get; init; }
}
