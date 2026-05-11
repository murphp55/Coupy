namespace CoupyApi.Models;

/// <summary>
/// A computed stack: one product at one retailer with a specific combination of applied offers
/// and the resulting net out-of-pocket. The engine yields these and the API ranks them.
/// </summary>
public record Stack
{
    public Product Product { get; init; } = null!;
    public string RetailerId { get; init; } = "";
    public decimal BasePrice { get; init; }

    /// <summary>The offers that make up this stack, in the order they're applied.</summary>
    public List<Offer> AppliedOffers { get; init; } = new();

    /// <summary>Total savings from coupons (applied at checkout).</summary>
    public decimal CouponSavings { get; init; }

    /// <summary>Total savings from post-purchase cashback/rebate/rewards.</summary>
    public decimal CashbackSavings { get; init; }

    /// <summary>BasePrice - CouponSavings - CashbackSavings. Can be negative (moneymaker).</summary>
    public decimal NetPrice { get; init; }

    /// <summary>Net price as a percent off MSRP — the main ranking signal.</summary>
    public decimal PercentOffMsrp { get; init; }

    public bool IsMoneymaker => NetPrice < 0;
    public bool IsFreeOrNearFree => NetPrice <= 0.50m;
}
