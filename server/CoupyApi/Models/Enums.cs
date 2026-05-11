namespace CoupyApi.Models;

/// <summary>
/// Classifies an offer by its role in a stack. Stacking rules are keyed off this.
/// </summary>
public enum CouponType
{
    /// <summary>Manufacturer coupon — P&amp;G, SmartSource, Coupons.com. Usually 1 per item per retailer.</summary>
    Manufacturer,

    /// <summary>Store-issued coupon (non-digital) — Walgreens paper coupon, CVS red-machine coupon.</summary>
    Store,

    /// <summary>Digital coupon clipped to a loyalty card — Walgreens MyWalgreens, CVS ExtraCare,
    /// Kroger digital, Target Circle offer. Typically stacks with manufacturer.</summary>
    DigitalClipped,

    /// <summary>Post-purchase cashback — Ibotta, Fetch, Checkout51. Applied after checkout; always stacks.</summary>
    CashbackRebate,

    /// <summary>Loyalty reward that prints as currency — Walgreens Register Rewards, CVS ExtraBucks.
    /// Counts as tender, not a coupon.</summary>
    LoyaltyReward,

    /// <summary>Online promo code — Rakuten Advertising feed, RetailMeNot. Used at checkout.</summary>
    PromoCode,

    /// <summary>Cashback portal rebate — Rakuten extension, MaxRebates, TopCashback. Applied via click-through.</summary>
    PortalCashback,

    /// <summary>Credit card / payment method reward — 5% drug store category, store card discount.</summary>
    PaymentReward
}

/// <summary>
/// How the offer's value should be applied to the base price.
/// </summary>
public enum DiscountType
{
    /// <summary>Fixed dollar amount — e.g. $2 off.</summary>
    AmountOff,

    /// <summary>Percentage — Value is 0-100.</summary>
    PercentOff,

    /// <summary>Buy one get one free — approximated as 50% off when one item is evaluated.</summary>
    BuyOneGetOne,

    /// <summary>Spend $X, get $Y reward — MinSpend must be set; Value is the reward amount.</summary>
    SpendThreshold,

    /// <summary>Free after rebate.</summary>
    FreeAfterRebate
}
