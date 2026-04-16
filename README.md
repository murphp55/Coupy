# Coupy

Desktop-first Flutter app for discovering, saving, and tracking online coupons.

## Notes
- Current focus is Windows desktop UI first; Android/iOS are future targets.
- This repo is a fresh Flutter scaffold; `lib/main.dart` still needs to be created.
- Java/Gradle note from Flutter create: Java 17–24 is recommended for the default Gradle 8.14 setup.

## What the app does (planned)
- Discover coupons from multiple sources in a single feed.
- Search and filter by store, category, discount, and expiration.
- View store pages with all active coupons and deals.
- Save coupons and stores, and set alerts for new deals.
- Track coupon validity signals (reporting + success rate).

## What still needs to be done
- Implement the Windows desktop UI in `lib/main.dart`.
- Define navigation structure (sidebar + top search + main content).
- Create core screens:
  - Home / Discover
  - Store Directory
  - Store Detail
  - Coupon Detail
  - Categories
  - Search Results
  - Saved / Favorites
  - Alerts
  - Settings
- Build reusable components (coupon card, store tile, filter chips).
- Add sample data models and mock data for UI development.
- Decide on data sources (affiliate APIs vs. aggregators).
- Add basic state management (provider, riverpod, or bloc).
- Wire search, filtering, and sorting logic.
- Add assets (icons, logo, branding).
- Validate coupon expiration and “not working” reporting flow.
- Later: add price tracking, cashback, and in-store offers.

## Dev quick start
- `flutter run -d windows`
