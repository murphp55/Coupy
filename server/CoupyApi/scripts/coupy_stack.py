#!/usr/bin/env python3
"""
coupy_stack.py — runnable port of the C# StackingEngine.

Reads the same ground-truth data as the .NET backend:
  - Config/retailers/*.json   (stacking rulebooks)
  - Data/samples/*.json       (sample offers + product catalog)

Emits the best stack per product×retailer pair, ranked by percent-off-MSRP,
with per-stack breakdowns of which offers got applied and why.

Usage:
  python3 coupy_stack.py                  # all deals above 30% off MSRP
  python3 coupy_stack.py --min 50         # deals >= 50% off
  python3 coupy_stack.py --json           # machine-readable output
  python3 coupy_stack.py --moneymakers    # only negative-net stacks
"""

from __future__ import annotations

import argparse
import itertools
import json
import sys
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

ROOT = Path(__file__).resolve().parent.parent  # CoupyApi/
CONFIG_DIR = ROOT / "Config" / "retailers"
SAMPLES_DIR = ROOT / "Data" / "samples"

# CouponType names that layer on top of any stack (never count against per-item limits)
CASHBACK_CLASS = {"cashbackRebate", "portalCashback", "paymentReward"}


@dataclass
class Offer:
    id: str
    source: str
    type: str               # CouponType (camelCase)
    discountType: str       # DiscountType (camelCase)
    value: float
    minQty: int = 1
    minSpend: float | None = None
    brandName: str | None = None
    productDescription: str | None = None
    categoryName: str | None = None
    code: str | None = None
    startDate: str | None = None
    endDate: str | None = None
    retailerId: str | None = None
    eligibleUpcs: list[str] = field(default_factory=list)

    def is_active(self, now: datetime) -> bool:
        if self.endDate:
            try:
                end = datetime.fromisoformat(self.endDate.replace("Z", "+00:00"))
                if end < now:
                    return False
            except ValueError:
                pass
        if self.startDate:
            try:
                start = datetime.fromisoformat(self.startDate.replace("Z", "+00:00"))
                if start > now:
                    return False
            except ValueError:
                pass
        return True


@dataclass
class Product:
    id: str
    brand: str
    name: str
    msrpPrice: float
    upc: str | None = None
    size: str | None = None
    category: str | None = None
    basePriceByRetailer: dict[str, float] = field(default_factory=dict)
    alternateUpcs: list[str] = field(default_factory=list)


@dataclass
class Retailer:
    id: str
    name: str
    rules: dict[str, Any]


@dataclass
class Stack:
    product: Product
    retailer_id: str
    base_price: float
    offers: list[Offer]
    coupon_savings: float
    cashback_savings: float
    net_price: float
    pct_off_msrp: float

    @property
    def is_moneymaker(self) -> bool:
        return self.net_price < 0

    @property
    def is_free_or_near_free(self) -> bool:
        return self.net_price <= 0.50


def _camel(s: str) -> str:
    """PascalCase enum name → camelCase JSON name, for looking up rule limits."""
    return s[0].lower() + s[1:] if s else s


def _pascal(s: str) -> str:
    return s[0].upper() + s[1:] if s else s


# ----------------------- Loading -----------------------

def load_retailers() -> list[Retailer]:
    out = []
    for f in sorted(CONFIG_DIR.glob("*.json")):
        data = json.loads(f.read_text())
        out.append(Retailer(id=data["id"], name=data["name"], rules=data.get("rules", {})))
    return out


def load_products() -> list[Product]:
    f = SAMPLES_DIR / "products.json"
    if not f.exists():
        return []
    return [Product(**p) for p in json.loads(f.read_text())]


def load_offers() -> list[Offer]:
    offers: list[Offer] = []
    for f in sorted(SAMPLES_DIR.glob("*-offers.json")):
        for o in json.loads(f.read_text()):
            # tolerate unknown/extra keys
            kwargs = {k: v for k, v in o.items() if k in Offer.__dataclass_fields__}
            offers.append(Offer(**kwargs))
    return offers


# ----------------------- Matching -----------------------

def tokenize(s: str) -> list[str]:
    toks = []
    cur = ""
    for ch in s.lower():
        if ch.isalnum():
            cur += ch
        else:
            if cur:
                toks.append(cur.rstrip("s"))
                cur = ""
    if cur:
        toks.append(cur.rstrip("s"))
    return [t for t in toks if len(t) >= 3]


def match(offer: Offer, product: Product) -> bool:
    # Strategy 1: UPC
    if offer.eligibleUpcs:
        if product.upc in offer.eligibleUpcs:
            return True
        if any(u in offer.eligibleUpcs for u in product.alternateUpcs):
            return True
        return False

    # Strategy 2: brand + token
    if offer.brandName:
        if offer.brandName.lower() != product.brand.lower():
            return False
        if offer.productDescription:
            brand_lower = offer.brandName.lower()
            tokens = [t for t in tokenize(offer.productDescription) if t != brand_lower]
            if tokens and not any(t in product.name.lower() for t in tokens):
                return False
        return True

    return False


# ----------------------- Stack enumeration -----------------------

def combinations_up_to(items: list[Offer], cap: int) -> list[list[Offer]]:
    out: list[list[Offer]] = []
    for k in range(1, min(cap, len(items)) + 1):
        out.extend([list(c) for c in itertools.combinations(items, k)])
    return out


def enumerate_legal_stacks(matched: list[Offer], retailer: Retailer) -> Iterable[list[Offer]]:
    cashback = [o for o in matched if o.type in CASHBACK_CLASS]
    coupon_layer = [o for o in matched if o.type not in CASHBACK_CLASS]

    buckets: dict[str, list[Offer]] = {}
    for o in coupon_layer:
        buckets.setdefault(o.type, []).append(o)

    per_item = retailer.rules.get("perItemLimits", {})

    bucket_choices: list[list[list[Offer]]] = []
    for ctype, offers in buckets.items():
        cap = per_item.get(_pascal(ctype), 0) or per_item.get(ctype, 0)
        if cap <= 0:
            continue
        choices: list[list[Offer]] = [[]]  # "pick none"
        choices.extend(combinations_up_to(offers, cap))
        bucket_choices.append(choices)

    stack_cashback = retailer.rules.get("stackCashback", True)

    if not bucket_choices:
        if cashback and stack_cashback:
            yield cashback[:]
        return

    for combo in itertools.product(*bucket_choices):
        picked = [o for group in combo for o in group]
        if stack_cashback:
            picked = picked + cashback
        if picked:
            yield picked


# ----------------------- Pricing -----------------------

def _discount_order(o: Offer) -> int:
    return {
        "amountOff": 0,
        "percentOff": 1,
        "buyOneGetOne": 2,
        "spendThreshold": 3,
        "freeAfterRebate": 4,
    }.get(o.discountType, 9)


def compute_net(product: Product, retailer: Retailer, offers: list[Offer]) -> Stack:
    base = product.basePriceByRetailer.get(retailer.id, product.msrpPrice)
    running = base
    coupon_savings = 0.0
    cashback_savings = 0.0

    for offer in sorted(offers, key=_discount_order):
        is_cashback = offer.type in CASHBACK_CLASS

        if offer.discountType == "amountOff":
            amount = offer.value
        elif offer.discountType == "percentOff":
            amount = round(running * offer.value / 100.0, 2)
        elif offer.discountType == "buyOneGetOne":
            amount = round(running / 2.0, 2)
        elif offer.discountType == "spendThreshold":
            amount = offer.value
        elif offer.discountType == "freeAfterRebate":
            amount = running
        else:
            amount = 0.0

        if is_cashback:
            cashback_savings += amount
        else:
            amount = min(amount, running)
            running -= amount
            coupon_savings += amount

    net = round(running - cashback_savings, 2)
    pct = round((product.msrpPrice - net) / product.msrpPrice * 100.0, 1) if product.msrpPrice else 0.0

    return Stack(
        product=product,
        retailer_id=retailer.id,
        base_price=round(base, 2),
        offers=offers,
        coupon_savings=round(coupon_savings, 2),
        cashback_savings=round(cashback_savings, 2),
        net_price=net,
        pct_off_msrp=pct,
    )


# ----------------------- Main -----------------------

def best_stacks(products: list[Product], offers: list[Offer], retailers: list[Retailer],
                min_pct: float = 0.0) -> list[Stack]:
    now = datetime.now(timezone.utc)
    active = [o for o in offers if o.is_active(now)]
    results: list[Stack] = []

    for retailer in retailers:
        eligible = [
            o for o in active
            if o.retailerId is None or o.retailerId.lower() == retailer.id.lower()
        ]
        for product in products:
            matched = [o for o in eligible if match(o, product)]
            if not matched:
                continue
            best = min(
                (compute_net(product, retailer, stack) for stack in enumerate_legal_stacks(matched, retailer)),
                default=None,
                key=lambda s: s.net_price,
            )
            if best and best.pct_off_msrp >= min_pct:
                results.append(best)

    return sorted(results, key=lambda s: -s.pct_off_msrp)


def format_stack(s: Stack) -> str:
    lines = [
        f"  {s.product.brand} {s.product.name}",
        f"    @ {s.retailer_id}: base ${s.base_price:.2f} → net ${s.net_price:.2f} "
        f"({s.pct_off_msrp:.1f}% off MSRP ${s.product.msrpPrice:.2f})"
        + ("  💰 MONEYMAKER" if s.is_moneymaker else ("  ★ near-free" if s.is_free_or_near_free else "")),
        f"    coupon savings ${s.coupon_savings:.2f}  |  cashback ${s.cashback_savings:.2f}",
    ]
    for o in s.offers:
        kind = f"{o.source}/{o.type}"
        val = f"${o.value:.2f}" if o.discountType in ("amountOff", "spendThreshold") else (
            f"{o.value:.0f}% off" if o.discountType == "percentOff" else o.discountType
        )
        lines.append(f"      • [{kind}] {val} — {o.productDescription or o.brandName or o.id}")
    return "\n".join(lines)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--min", type=float, default=30.0, help="Min percent off MSRP (default 30)")
    ap.add_argument("--moneymakers", action="store_true", help="Show only net-negative stacks")
    ap.add_argument("--json", action="store_true", help="Emit JSON instead of text")
    args = ap.parse_args()

    retailers = load_retailers()
    products = load_products()
    offers = load_offers()

    stacks = best_stacks(products, offers, retailers, min_pct=0.0 if args.moneymakers else args.min)
    if args.moneymakers:
        stacks = [s for s in stacks if s.is_moneymaker]

    if args.json:
        out = [
            {
                "product": s.product.name,
                "brand": s.product.brand,
                "retailer": s.retailer_id,
                "basePrice": s.base_price,
                "netPrice": s.net_price,
                "percentOffMsrp": s.pct_off_msrp,
                "couponSavings": s.coupon_savings,
                "cashbackSavings": s.cashback_savings,
                "isMoneymaker": s.is_moneymaker,
                "offers": [
                    {
                        "source": o.source,
                        "type": o.type,
                        "discountType": o.discountType,
                        "value": o.value,
                        "description": o.productDescription,
                    } for o in s.offers
                ],
            } for s in stacks
        ]
        json.dump(out, sys.stdout, indent=2)
        sys.stdout.write("\n")
        return

    if not stacks:
        print(f"No stacks above {args.min}% off MSRP.")
        return

    print(f"=== {len(stacks)} worthwhile stack(s) ===\n")
    print(f"Catalog: {len(products)} products × {len(retailers)} retailers × {len(offers)} active offers\n")
    for s in stacks:
        print(format_stack(s))
        print()


if __name__ == "__main__":
    main()
