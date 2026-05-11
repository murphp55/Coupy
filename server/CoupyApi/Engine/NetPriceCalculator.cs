using CoupyApi.Models;

namespace CoupyApi.Engine;

/// <summary>
/// Applies a set of offers to a product at a retailer and returns the resulting Stack with
/// net price. Coupons reduce the at-register price; cashback-class offers reduce effective
/// cost post-purchase (and can drive net negative = moneymaker).
/// </summary>
public class NetPriceCalculator
{
    public Stack Compute(Product product, Retailer retailer, List<Offer> offers)
    {
        var basePrice = product.BasePriceByRetailer.TryGetValue(retailer.Id, out var b)
            ? b
            : product.MsrpPrice;

        decimal couponSavings = 0m;
        decimal cashbackSavings = 0m;

        // Apply coupons in order that favors the shopper: AmountOff first, then PercentOff
        // (applied to the discounted remainder), then BOGO. This mirrors how most POS systems
        // sequence discounts, though policies vary — revisit if a retailer differs.
        var ordered = offers
            .OrderBy(o => o.DiscountType switch
            {
                DiscountType.AmountOff => 0,
                DiscountType.PercentOff => 1,
                DiscountType.BuyOneGetOne => 2,
                DiscountType.SpendThreshold => 3,
                DiscountType.FreeAfterRebate => 4,
                _ => 9
            })
            .ToList();

        decimal running = basePrice;

        foreach (var offer in ordered)
        {
            var isCashbackClass = offer.Type is CouponType.CashbackRebate
                or CouponType.PortalCashback
                or CouponType.PaymentReward;

            var amount = offer.DiscountType switch
            {
                DiscountType.AmountOff => offer.Value,
                DiscountType.PercentOff => Math.Round(running * offer.Value / 100m, 2),
                DiscountType.BuyOneGetOne => Math.Round(running / 2m, 2),
                DiscountType.SpendThreshold => offer.Value, // rewards the full Value as cashback equivalent
                DiscountType.FreeAfterRebate => running,
                _ => 0m
            };

            // Clamp coupon so it doesn't go below zero (most registers don't give change on coupons)
            if (!isCashbackClass)
            {
                amount = Math.Min(amount, running);
                running -= amount;
                couponSavings += amount;
            }
            else
            {
                cashbackSavings += amount;
            }
        }

        var net = running - cashbackSavings; // cashback can push this negative
        var pctOff = product.MsrpPrice > 0
            ? Math.Round((product.MsrpPrice - net) / product.MsrpPrice * 100m, 1)
            : 0m;

        return new Stack
        {
            Product = product,
            RetailerId = retailer.Id,
            BasePrice = basePrice,
            AppliedOffers = offers.ToList(),
            CouponSavings = Math.Round(couponSavings, 2),
            CashbackSavings = Math.Round(cashbackSavings, 2),
            NetPrice = Math.Round(net, 2),
            PercentOffMsrp = pctOff
        };
    }
}
