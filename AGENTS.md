# Coupy — Agent Notes

## One-liner
Flutter desktop-first coupon discovery app (Dart) with an ASP.NET Core (.NET 10, C#) backend in `server/`.

## Run it
```
# Backend (current target — supersedes WalgreensOffersApi)
cd server/CoupyApi
dotnet restore
dotnet run                       # http://localhost:5080

# Frontend (Windows is primary platform)
flutter pub get
flutter run -d windows
```

## Where we are right now
- Last touched: 2026-05-10 (single commit `23884cf` on 2025-01-22; active uncommitted work present)
- Working on: New `server/CoupyApi/` (multi-source stacking engine) is untracked and supersedes the older `server/WalgreensOffersApi/`. README.md has unstaged edits.
- Known broken: README still documents the old `WalgreensOffersApi` on port 5075; the new `CoupyApi` runs on 5080. Frontend (`lib/main.dart`) is still pointed at the old backend until repointed.

*This section goes stale fast. Check `git log -5` and `git status` before trusting it.*

## Gotchas
- Two backends exist in `server/`: `WalgreensOffersApi` (committed, port 5075, in README) and `CoupyApi` (untracked, port 5080, the new target). Don't assume the README's run commands match the active backend.
- `CoupyApi` auto-runs all ingesters on boot; disable with `COUPY_AUTO_INGEST=false`.
- Secrets via `.env` / env vars (`WALGREENS_API_KEY`, `WALGREENS_AFF_ID`, etc.) — never commit.
- Android builds require Java 17-24 for Gradle 8.14.

## Non-obvious conventions
- Frontend is essentially single-file (`lib/main.dart`, ~525 lines) — no state management, no `models/screens/widgets/` populated yet despite directories implied in README.
- No tests exist yet (`test/` is scaffold only); `flutter test` and `dotnet test` are aspirational.
- Material 3 seed color is Walgreens teal `#1C6A70` hardcoded in `lib/main.dart`.

See README.md for project description, tech stack, and feature list.
