using CoupyApi.Models;
using CoupyApi.Storage;

namespace CoupyApi.Engine;

/// <summary>
/// The core reasoner. For every product × retailer pair, enumerates legal offer combinations
/// under that retailer's rulebook, computes net price for each, and yields ranked Stacks.
///
/// Algorithm:
///   1. Filter offers to those that match the product (via ProductMatcher) AND apply at this
///      retailer (offer.RetailerId == retailer.Id OR offer.RetailerId == null for manufacturer
///      coupons and online promo codes).
///   2. Bucket offers by CouponType.
///   3. For each bucket, enumerate "choose up to K" per retailer's PerItemLimits (K=0 means
///      the bucket is disallowed for this retailer).
///   4. Cartesian-product the bucket choices → candidate stacks.
///   5. Always include cashback/portal-cashback/payment-reward offers — they stack on top.
///   6. Apply the NetPriceCalculator to each candidate; keep the best N by percent-off-MSRP.
/// </summary>
public class StackingEngine
{
    private readonly DataStore _store;
    private readonly ProductMatcher _matcher;
    private readonly NetPriceCalculator _calc;
    private readonly ILogger<StackingEngine> _logger;

    public StackingEngine(DataStore store, ProductMatcher matcher, NetPriceCalculator calc, ILogger<StackingEngine> logger)
    {
        _store = store;
        _matcher = matcher;
        _calc = calc;
        _logger = logger;
    }

    /// <summary>Compute the best stack for every product × retailer pair.</summary>
    public IReadOnlyList<Stack> BestStacks(int topN = 50, decimal minPercentOffMsrp = 0m)
    {
        var now = DateTimeOffset.UtcNow;
        var activeOffers = _store.Offers.Where(o => o.IsActive(now)).ToList();
        var results = new List<Stack>();

        foreach (var retailer in _store.Retailers)
        {
            // Offers that could apply at this retailer: manufacturer (RetailerId null) + this
            // retailer's store/digital offers + pure online promo codes that happen to target
            // this retailer's online store (we only trust explicit RetailerId here).
            var retailerOffers = activeOffers
                .Where(o => o.RetailerId is null || string.Equals(o.RetailerId, retailer.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var product in _store.Products)
            {
                // Which of the retailer-eligible offers match this product?
                var matched = retailerOffers
                    .Where(o => _matcher.MatchOffer(o, new[] { product }).Any())
                    .ToList();

                if (matched.Count == 0) continue;

                var best = EnumerateStacks(matched, retailer)
                    .Select(stack => _calc.Compute(product, retailer, stack))
                    .OrderBy(s => s.NetPrice)
                    .FirstOrDefault();

                if (best is not null && best.PercentOffMsrp >= minPercentOffMsrp)
                    results.Add(best);
            }
        }

        return results
            .OrderByDescending(s => s.PercentOffMsrp)
            .Take(topN)
            .ToList();
    }

    /// <summary>Enumerate all legal stacks for a set of matched offers under a retailer's rules.</summary>
    public IEnumerable<List<Offer>> EnumerateStacks(IReadOnlyList<Offer> matched, Retailer retailer)
    {
        // Separate cashback-layer offers — they always stack on top.
        var cashbackLayer = matched
            .Where(o => o.Type is CouponType.CashbackRebate or CouponType.PortalCashback or CouponType.PaymentReward)
            .ToList();

        var couponLayer = matched.Except(cashbackLayer).ToList();

        // Bucket by CouponType → list
        var buckets = Enum.GetValues<CouponType>()
            .ToDictionary(
                t => t,
                t => couponLayer.Where(o => o.Type == t).ToList());

        // For each bucket, build the choices: either empty (pick none) or every combination up to the cap.
        var bucketChoices = new List<List<List<Offer>>>();
        foreach (var (type, offers) in buckets)
        {
            if (offers.Count == 0) continue;
            var cap = retailer.Rules.PerItemLimits.GetValueOrDefault(type.ToString(), 0);
            if (cap <= 0) continue;

            var choicesForBucket = new List<List<Offer>> { new() }; // "pick none" option
            choicesForBucket.AddRange(Combinations(offers, cap));
            bucketChoices.Add(choicesForBucket);
        }

        if (bucketChoices.Count == 0)
        {
            // No coupon-layer offers are legal here; still emit a stack with just cashback (if any).
            if (cashbackLayer.Count > 0) yield return cashbackLayer.ToList();
            yield break;
        }

        // Cartesian product across buckets
        foreach (var combo in CartesianProduct(bucketChoices))
        {
            var stack = combo.SelectMany(c => c).ToList();
            if (retailer.Rules.StackCashback) stack.AddRange(cashbackLayer);
            if (stack.Count > 0) yield return stack;
        }
    }

    private static IEnumerable<List<T>> Combinations<T>(IReadOnlyList<T> items, int maxSize)
    {
        var size = Math.Min(maxSize, items.Count);
        for (int k = 1; k <= size; k++)
            foreach (var c in CombinationsK(items, k))
                yield return c;
    }

    private static IEnumerable<List<T>> CombinationsK<T>(IReadOnlyList<T> items, int k)
    {
        if (k == 0) { yield return new List<T>(); yield break; }
        for (int i = 0; i <= items.Count - k; i++)
        {
            foreach (var tail in CombinationsK(items.Skip(i + 1).ToList(), k - 1))
            {
                var combo = new List<T> { items[i] };
                combo.AddRange(tail);
                yield return combo;
            }
        }
    }

    private static IEnumerable<List<List<T>>> CartesianProduct<T>(List<List<List<T>>> sequences)
    {
        IEnumerable<List<List<T>>> result = new[] { new List<List<T>>() };
        foreach (var seq in sequences)
        {
            var s = seq;
            result = result.SelectMany(acc => s.Select(item =>
            {
                var next = new List<List<T>>(acc) { item };
                return next;
            }));
        }
        return result;
    }
}
