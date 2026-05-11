# CoupyApi

Multi-source coupon stacking engine for the Coupy app. Ingests offers from retailer
loyalty APIs, manufacturer feeds, and cashback apps; normalizes them to a common
`Offer` model; and computes the best legal stack per product per retailer under each
store's rulebook.

This project supersedes `WalgreensOffersApi` — that folder is kept for reference and
can be deleted once the Flutter frontend is repointed at this backend.

## Quick start

```bash
cd server/CoupyApi
dotnet restore
dotnet run
```

The API boots on `http://localhost:5080`, seeds retailers from `Config/retailers/*.json`
and products from `Data/samples/products.json`, then runs every ingester once on boot
(sample data for any source missing credentials). `/api/stacks` immediately returns
computed stacks against the sample catalog.

Turn off boot-time ingest with `COUPY_AUTO_INGEST=false` and drive it via cron or the
Cowork scheduled-tasks skill instead.

## Layout

```
CoupyApi/
  Models/            Offer, Product, Retailer, Stack, enums
  Storage/           DataStore (in-memory) + Seeder (JSON → store)
  Ingesters/         One class per source; all implement IOfferIngester
  Engine/            ProductMatcher, StackingEngine, NetPriceCalculator
  Endpoints/         Minimal API routes
  Config/retailers/  One JSON per retailer: stacking rules
  Data/samples/      Sample offers used when credentials are missing
```

## Endpoints

| Method | Path                        | Purpose                                                  |
|--------|-----------------------------|----------------------------------------------------------|
| GET    | `/health`                   | Liveness                                                 |
| GET    | `/api/sources`              | Ingester status per source — which creds are missing     |
| GET    | `/api/retailers`            | Loaded retailer rules                                    |
| GET    | `/api/products`             | Seeded product catalog                                   |
| GET    | `/api/offers`               | All ingested offers (filter: `source`, `retailerId`, `type`) |
| POST   | `/api/ingest`               | Run every ingester, return per-source counts             |
| POST   | `/api/ingest/{source}`      | Run one ingester                                         |
| GET    | `/api/stacks`               | Top-N best stacks across the catalog                     |
| GET    | `/api/stacks/moneymakers`   | Stacks where NetPrice < $0                               |

## Sources and credentials

The 6 sources below represent the four viable tiers of coupon data. Each ingester
falls back to `Data/samples/{source}-offers.json` when credentials are missing, so
the pipeline works end-to-end from day one.

### Tier 1 — Official developer APIs

| Source               | Credentials                                                     | Where to get it                                                 |
|----------------------|-----------------------------------------------------------------|-----------------------------------------------------------------|
| Walgreens            | `WALGREENS_API_KEY`, `WALGREENS_AFF_ID`, `WALGREENS_ENC_LOYALTY_ID` | Register at https://developer.walgreens.com/apis              |
| Kroger               | `KROGER_CLIENT_ID`, `KROGER_CLIENT_SECRET`                      | https://developer.kroger.com (free OAuth client)                |
| Rakuten Advertising  | `RAKUTEN_WEB_SERVICES_TOKEN`                                    | Apply as publisher at https://rakutenadvertising.com; approval in days |

Rakuten Advertising is the highest-ROI cred to acquire — one token covers hundreds
of general-retail brands with clean structured coupon codes. Walgreens is the only
drug chain with real first-party coupon API access.

### Tier 2 — Partner feeds (gated business approval)

| Source        | Credentials                                      | Notes                                                              |
|---------------|--------------------------------------------------|--------------------------------------------------------------------|
| Coupons.com   | `COUPONSCOM_PARTNER_KEY` or `COUPONSCOM_ZIP`     | Apply at coupons.com/partners for the partner XML feed. Fallback: consumer scrape by zip. |

### Tier 3 — Unofficial mobile-app endpoints (no key available)

These are not represented as separate ingesters in the skeleton because the
authentication story is different for each — they require a logged-in user session
captured via mitmproxy/Charles, not a service key. Add as new ingesters once you
decide whether to support them:

- **CVS ExtraCare** — community tool `logkirk/cvs-coupons` shows the clip flow.
- **Target Circle** — app drives `api.targetcircle.com` with user tokens.
- **Safeway/Albertsons Just4U** — single app for Safeway, Albertsons, Vons, Jewel-Osco, Shaw's, Acme, Tom Thumb, Randalls.
- **Meijer mPerks**, **Publix digital**, **Harris Teeter e-VIC**, **Rite Aid Wellness**, **Dollar General DG Digital**.

### Tier 4 — Manual import (no API at all)

| Source  | How                                                                     |
|---------|-------------------------------------------------------------------------|
| Ibotta  | Edit `Data/samples/ibotta-offers.json` with the rebates visible in-app. |
| Fetch   | Same pattern — add a `fetch-offers.json` and a near-identical ingester. |

Ibotta's **IPN** (Ibotta Performance Network) is a real B2B API but onboarding is
~3 months and designed for large publishers. Not viable for personal use.

### Flipp (base-price ingester)

| Source | Credentials                                        | Notes                                                          |
|--------|----------------------------------------------------|----------------------------------------------------------------|
| Flipp  | `FLIPP_ENABLED=true` + `FLIPP_POSTAL_CODE=12345`   | No public API; opt-in because it hits an unofficial endpoint. Primary value: per-retailer shelf/sale prices for net-price math. |

## Stacking rulebooks

One JSON per retailer in `Config/retailers/`. Structure:

```json
{
  "id": "walgreens",
  "name": "Walgreens",
  "rules": {
    "perItemLimits": { "Manufacturer": 1, "Store": 1, "DigitalClipped": 99 },
    "rewardsAsTender": ["LoyaltyReward"],
    "stackCashback": true,
    "notes": "human-readable policy summary",
    "policyUrl": "https://..."
  }
}
```

`perItemLimits` keys are `CouponType` names; `99` means "unlimited distinct". Curate
these by hand from each retailer's published coupon policy — they change maybe
twice a year. Always include the `policyUrl` so the next person can re-verify.

Currently shipping: Walgreens, CVS, Kroger, Target, Safeway. Add more by dropping a
new file in `Config/retailers/` — no code change needed.

## Engine behavior

The `StackingEngine` iterates every `product × retailer` pair, filters offers to
those that match (via `ProductMatcher`) and apply at that retailer, buckets them by
`CouponType`, enumerates legal combinations under `perItemLimits`, and uses
`NetPriceCalculator` to compute the net out-of-pocket per candidate stack. Cashback,
portal cashback, and payment rewards always layer on top (when `stackCashback` is
true) and can push `NetPrice` negative — that's a moneymaker.

The weakest link is `ProductMatcher`. Current behavior: exact-UPC → brand + token
match → brand-only fallback. Expect false positives on brands with many product
lines (Colgate Total vs Colgate Optic White). Improvements worth doing in this
order: (1) size normalization, (2) stop-word-aware tokenization, (3) trained
embedding match against a real UPC catalog (OpenFoodFacts + store product pages).

## Next steps

In priority order:
1. Swap `DataStore` for EF Core + SQLite so you can diff yesterday's offers against today's and alert on new stacks.
2. Wire real Walgreens + Kroger + Rakuten Advertising credentials and confirm end-to-end.
3. Add CVS as the first Tier 3 ingester using captured app endpoints.
4. Build a weekly-ad ingester (Flipp) for true per-retailer base prices.
5. Schedule nightly ingest via the Cowork scheduled-tasks skill; notify on new moneymakers.
6. Repoint the Flutter frontend from `/api/walgreens/offers` to `/api/stacks`.
