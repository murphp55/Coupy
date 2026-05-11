# Coupy

A Flutter desktop-first app for discovering, saving, and tracking online coupons with a focus on department store offers. Coupy combines a Flutter frontend with a .NET backend to aggregate and serve digital offers, starting with Walgreens as a proof-of-concept.

## Tech Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| **Frontend** | Flutter (Dart) | 3.10.7+ |
| **Backend** | .NET Core | 10 |
| **Backend Language** | C# | 12+ |
| **UI Theme** | Material Design 3 | - |
| **Primary Platform** | Windows Desktop | - |
| **Secondary Platforms** | Android, iOS (future) | - |

## Project Overview

Coupy is a **proof-of-concept coupon discovery and tracking application** currently targeting **Windows desktop** as the primary platform. The app integrates with the Walgreens digital offers API to display loyalty member promotions and deals.

**Current State**: Early prototype with scaffolding in place. Backend API endpoints functional; frontend UI partially implemented.

## Architecture

### Two-Tier Architecture

```
┌─────────────────────────────┐
│   Flutter Frontend (Dart)   │  Windows Desktop (primary)
│   - Loyalty lookup UI       │  Android/iOS (future targets)
│   - Offers feed display     │
└────────────┬────────────────┘
             │ HTTP REST API
┌────────────▼────────────────┐
│   .NET Backend (C#)         │  ASP.NET Core running on localhost:5075
│   - Walgreens API proxy     │  Handles auth, caching, aggregation
│   - Digital offers endpoint │
└─────────────────────────────┘
             │
             ▼
┌─────────────────────────────┐
│  Walgreens Digital API      │  External Walgreens service
└─────────────────────────────┘
```

## Key Features (Planned & Partial)

- **Loyalty Member Lookup**: Search for Walgreens loyalty member by phone number
- **Digital Offers Feed**: Display loyalty member's personalized offers
- **Offer Details**: Brand, category, discount value, expiration date, offer image
- **Material 3 Theming**: Walgreens teal branding (#1C6A70)
- **Multi-Platform Support**: Windows desktop first; Android/iOS to follow
- **Environment-Based Configuration**: API keys and credentials via environment variables

## Project Structure

```
coupy/
├── lib/
│   ├── main.dart                       # App entry point (525 lines)
│   │                                   # - UI scaffolding
│   │                                   # - Loyalty lookup flow
│   │                                   # - Offers display
│   ├── models/
│   │   └── [offer models - planned]
│   ├── screens/
│   │   └── [screens - planned]
│   └── widgets/
│       └── [reusable components - planned]
│
├── server/
│   └── WalgreensOffersApi/
│       ├── Program.cs                  # Backend entry point (262 lines)
│       │                               # - API route setup
│       │                               # - Walgreens API integration
│       │                               # - Health check endpoint
│       ├── appsettings.json            # Configuration (dev)
│       ├── appsettings.Production.json # Production config
│       ├── WalgreensOffersApi.csproj   # .NET project file
│       └── Properties/
│           └── launchSettings.json     # Debug profiles
│
├── pubspec.yaml                        # Flutter dependencies
├── pubspec.lock                        # Locked versions
├── .env.example                        # Environment variable template
├── .env                                # Local environment (git-ignored)
└── README.md
```

## Key Files to Know

### Frontend

| File | Lines | Purpose |
|------|-------|---------|
| `lib/main.dart` | ~525 | App initialization, Material 3 theme setup, loyalty lookup UI, offers display |

### Backend

| File | Lines | Purpose |
|------|-------|---------|
| `server/WalgreensOffersApi/Program.cs` | ~262 | ASP.NET Core app setup, route configuration, Walgreens API proxy, dependency injection |
| `server/WalgreensOffersApi/appsettings.json` | ~30 | Local development settings |

## API Endpoints

### Health Check
```
GET /health
```
Returns: `200 OK` with status message

### Loyalty Member Lookup
```
POST /api/walgreens/loyalty-lookup
Content-Type: application/json

{
  "phoneNumber": "6175551234"
}
```
Returns:
```json
{
  "matchProfiles": [
    {
      "loyaltyMemberId": "encryptedMemberId",
      "firstName": "John",
      "lastName": "Doe"
    }
  ]
}
```

### Get Digital Offers
```
GET /api/walgreens/offers?loyaltyMemberId=encryptedMemberId
```
Returns:
```json
{
  "offers": [
    {
      "id": "offer123",
      "title": "Save $5",
      "description": "Save $5 on Beauty Products",
      "imageUrl": "https://...",
      "brand": "CoverGirl",
      "category": "Beauty",
      "expiryDate": "2026-04-30",
      "value": 5.0,
      "discount": "Save $5"
    }
  ]
}
```

## How to Build & Run

### Prerequisites
- **Frontend**: Flutter SDK (Dart 3.10.7+)
- **Backend**: .NET 10 SDK, C# 12+
- **Java** (for Android/iOS builds): Java 17-24 recommended (for Gradle 8.14)
- **Windows**: Windows 10/11 with Visual Studio 2022 Community or higher

### Environment Configuration

Create a `.env` file in project root:
```env
# Backend server
WALGREENS_API_BASE_URL=http://localhost:5075

# Walgreens API credentials (backend)
WALGREENS_API_KEY=your_api_key_here
WALGREENS_AFF_ID=your_affiliate_id_here
WALGREENS_ENC_LOYALTY_ID=your_encrypted_loyalty_id_here
```

Or set as environment variables:
```bash
export WALGREENS_API_KEY=your_api_key_here
export WALGREENS_AFF_ID=your_affiliate_id_here
```

### Running the Backend

```bash
# Navigate to backend directory
cd server/WalgreensOffersApi

# Restore NuGet dependencies
dotnet restore

# Run in development mode (localhost:5075)
dotnet run

# Or build and run release
dotnet build -c Release
dotnet run -c Release
```

The API will be available at `http://localhost:5075`

### Running the Frontend

```bash
# Get Flutter dependencies
flutter pub get

# Run on Windows (primary platform)
flutter run -d windows

# Run on Android emulator
flutter run -d android

# Run on iOS simulator (macOS only)
flutter run -d ios

# Build release APK
flutter build apk --release

# Build Windows executable
flutter build windows --release
```

### Full Integration Test

Terminal 1 (Backend):
```bash
cd server/WalgreensOffersApi
dotnet run
```

Terminal 2 (Frontend):
```bash
flutter run -d windows
```

## Development State

**Status**: Proof of Concept (PoC) / Fresh Scaffold

**Latest Updates**:
- Single commit in repository
- Created Jan 22, 2025

**Current Implementation**:
- Backend API scaffolding complete with 3 endpoints
- Walgreens digital offers API integration stubbed
- Flutter frontend main.dart with Material 3 theme
- Loyalty lookup screen UI partial
- Offers feed UI partial

**Code Stats**:
- Frontend: ~525 lines (lib/main.dart)
- Backend: ~262 lines (Program.cs)
- Total: ~800 lines of code

**What's Working**:
- Backend server starts and listens on localhost:5075
- `/health` endpoint operational
- Environment variable configuration
- Flutter project structure in place
- Material 3 Walgreens teal theme (#1C6A70)

**Known Limitations**:
- Walgreens API integration is mock/stubbed
- Frontend UI is only partially implemented
- No persistent data storage (SQLite/database)
- No state management (Provider, Riverpod, Bloc)
- No error handling or user feedback for failed requests
- No offline caching
- No tests (unit or widget)
- Android/iOS not primary focus yet
- No real-world coupon data sources connected

## Dependencies

### Frontend (Flutter/Dart)
```yaml
flutter: sdk
cupertino_icons: ^1.0.8     # iOS-style icons
```

### Backend (.NET)
```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```
Standard ASP.NET Core libraries included with .NET 10 SDK

## Configuration

### Backend Settings
Edit `server/WalgreensOffersApi/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*",
  "WalgreensApi": {
    "BaseUrl": "https://...",
    "ApiKey": "${WALGREENS_API_KEY}",
    "AffiliateId": "${WALGREENS_AFF_ID}"
  }
}
```

### Frontend Theme
Edit `lib/main.dart`:
```dart
const seedColor = Color(0xFF1C6A70);  // Walgreens teal
// Material 3 theme auto-generates from seed color
```

## Future Enhancements

### Phase 2 (Next)
- [ ] Complete Walgreens API integration (remove mocks)
- [ ] Implement all frontend screens (Store directory, Coupon detail, Search, Favorites)
- [ ] Add state management (Provider or Riverpod)
- [ ] Implement persistence (SQLite or local storage)
- [ ] Add unit and widget tests
- [ ] Error handling and user feedback

### Phase 3
- [ ] Multi-source coupon aggregation (Target, Costco, CVS, etc.)
- [ ] User authentication (Firebase or custom backend)
- [ ] Saved coupons and wishlist
- [ ] Push notifications for new deals
- [ ] Android and iOS optimization
- [ ] Web platform support

### Phase 4 (Long-term)
- [ ] AI-powered coupon recommendations
- [ ] Price tracking integration
- [ ] Cashback partner integration
- [ ] Community reviews and ratings
- [ ] Referral system

## Testing

```bash
# Run Flutter tests (when implemented)
flutter test

# Run backend tests (when created)
cd server/WalgreensOffersApi
dotnet test
```

## Debugging

### Flutter
```bash
# Enable verbose logging
flutter run -v

# Launch DevTools
flutter pub global run devtools
```

### .NET Backend
```bash
# Enable debug logging
ASPNETCORE_ENVIRONMENT=Development dotnet run

# Attach debugger in Visual Studio Code
# (requires Debug Adapter installed)
```

## API Integration Roadmap

**Current**: Mocked Walgreens API responses

**To Implement Real Integration**:
1. Obtain Walgreens digital offers API credentials
2. Update `appsettings.json` with real base URL and API key
3. Implement actual HTTP client calls in `Program.cs`
4. Add request signing (OAuth2 or HMAC) if required
5. Add response caching and error handling
6. Test with real loyalty account

## Platform-Specific Notes

### Windows Desktop
- Primary development platform
- Uses WinUI 3 via Flutter
- Window size and DPI scaling configured in `pubspec.yaml`

### Android
- Target SDK 21+ (Android 5.0)
- Requires Gradle 8.14 with Java 17-24
- Run `flutter pub get` before building

### iOS
- Deployment target iOS 11.0+
- Requires Xcode 14+
- macOS only for development

## Tips for Developers

1. **Start with Backend**: Get the .NET API running first, then develop frontend against it
2. **Use DevTools**: Flutter DevTools helpful for inspecting widget tree and network calls
3. **Mock APIs During Frontend Work**: Keep backend running in background; focus on UI
4. **Environment Variables**: Always use `.env` for secrets; never commit credentials
5. **State Management**: Consider adding Provider or Riverpod once more screens are added
6. **Testing**: Write widget tests for UI screens and unit tests for business logic

## Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| Backend won't start | Check port 5075 not in use; ensure .NET 10 installed |
| Frontend can't reach API | Verify backend running on localhost:5075; check firewall |
| Flutter build fails | Run `flutter clean` then `flutter pub get` |
| Java version mismatch (Android) | Install Java 17-24; update JAVA_HOME environment variable |

## Notes for AI Assistants

When opening this project in a new session:
- **Dual Project**: Flutter frontend + .NET backend in same repo
- **Backend First**: Start the .NET server before running Flutter app
- **API Base URL**: Frontend hardcoded to `http://localhost:5075` (configurable via environment variable)
- **Walgreens API**: Currently stubbed; real implementation needed
- **Primary Platform**: Windows desktop (Android/iOS are future targets)
- **Theme Color**: Walgreens teal (#1C6A70) with Material 3
- **Configuration**: Uses environment variables for API credentials
- **Status**: Fresh scaffold with basic scaffolding; implementation in progress

## License

Proprietary—Patrick Murphy
