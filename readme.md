# E-Dnevnik API

Backend REST API for the **Odlikaš** mobile app — a companion for the Croatian national e-grade system [E-Dnevnik](https://ocjene.skole.hr). The API authenticates students, scrapes their grade data, detects new grades in real time and delivers push notifications via Firebase Cloud Messaging.

**Live:** `https://e-dnevnik-api.fly.dev`

---

## Features

- Student login via E-Dnevnik credentials (session-based, stored server-side)
- Grade scraping: all subjects, individual subject detail, grade history
- Absence tracking
- Upcoming test schedule
- Student profile and school class info
- Background polling — detects new grades and tests since last check
- Push notifications on grade drop (FCM)
- Leaderboard — opt-in anonymous GPA ranking by school program
- Pomodoro session tracking
- Study notification scheduling
- Stripe subscription paywall — create subscription, confirm payment, cancel
- Rate limiting (20 req/min per IP), HSTS, security headers
- Auto-migration on startup (EF Core + PostgreSQL)

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 (ASP.NET Core) |
| Database | PostgreSQL + Entity Framework Core 8 |
| Scraping | HtmlAgilityPack |
| Push notifications | Firebase Admin SDK (FCM) |
| Payments | Stripe.net v51 |
| Realtime DB | Google.Cloud.Firestore |
| Deployment | Docker → Fly.io |
| DB hosting | Neon (serverless Postgres) |

---

## Project Structure

```
E_Dnevnik_API/
├── Controllers/          # HTTP endpoints (thin layer, no business logic)
│   ├── LoginController               # POST /api/Login
│   ├── ScrapeController              # Grade, subject, profile scraping
│   ├── AccountController             # Account status, Odlikas+ flag
│   ├── LeaderboardController         # Anonymous GPA leaderboard
│   ├── DeviceController              # FCM token registration
│   ├── BackgroundController          # Manual background refresh trigger
│   ├── PomodoroController            # Pomodoro session tracking
│   ├── StudyNotificationsController
│   └── PaymentController             # Stripe subscription (create, confirm, cancel)
├── ScrapingServices/     # E-Dnevnik HTML scraping logic
│   ├── ScraperService                # Main grades scraper
│   ├── SpecificSubjectScraperService
│   ├── StudentProfileScraperService
│   ├── AbsenceScraperService
│   ├── ScheduleTableScraperService
│   ├── NewGradesScraperService       # Delta detection (new grades only)
│   ├── NewTestsScraperService
│   ├── SessionStore                  # In-memory session management
│   └── LoginBruteForceProtection
├── Services/             # Application services
│   ├── CacheService                  # DB-backed response caching
│   ├── FcmService                    # Firebase push notifications
│   ├── GradeChangeDetectionService
│   ├── TaskGenerationService         # AI-generated study tasks
│   └── NewDataRefreshService         # Background hosted service (polling)
├── Database/             # EF Core DbContext
├── Models/               # Response/request DTOs
├── Migrations/           # EF Core migrations (auto-applied on startup)
├── Program.cs            # App bootstrap, DI registration, middleware pipeline
└── Dockerfile
```

---

## Running Locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- PostgreSQL instance (local or [Neon](https://neon.tech) free tier)

### Setup

1. Clone the repo:
   ```bash
   git clone https://github.com/Karlo11111/E_Dnevnik_API.git
   cd E_Dnevnik_API
   ```

2. Add a local connection string in `appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=ednevnik;Username=postgres;Password=yourpassword"
     }
   }
   ```

3. Run and start:
   ```bash
   dotnet run
   ```

4. Swagger UI available at: `http://localhost:5168/swagger`

### Environment Variables

| Variable | Required | Description |
|---|---|---|
| `DATABASE_URL` | Yes (production) | Postgres connection URL (`postgres://user:pass@host/db`) |
| `PORT` | No | Port to listen on (default: `5168`) |
| `FIREBASE_SERVICE_ACCOUNT_JSON` | No | Firebase service account JSON content for push notifications and Firestore |
| `STRIPE_SECRET_KEY` | Yes (payments) | Stripe secret key — app throws on startup if missing |

---

## Running with Docker

```bash
docker build -t e-dnevnik-api .
docker run -p 8080:8080 \
  -e DATABASE_URL="postgres://user:pass@host/db" \
  -e PORT=8080 \
  e-dnevnik-api
```

---

## API Overview

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/Login` | Authenticate with E-Dnevnik credentials |
| GET | `/api/Scrape/grades` | Fetch all subjects and grades |
| GET | `/api/Scrape/subject/{id}` | Fetch specific subject detail |
| GET | `/api/Scrape/profile` | Fetch student profile |
| GET | `/api/Scrape/absences` | Fetch absences |
| GET | `/api/Scrape/schedule` | Fetch test schedule |
| GET | `/api/Account/Status` | Get account flags (e.g. isOdlikasPlus) |
| GET | `/api/Leaderboard` | Get anonymous leaderboard by program |
| POST | `/api/Device/token` | Register FCM device token |
| POST | `/api/Payment/CreateSubscription` | Start Stripe subscription, returns `clientSecret` |
| POST | `/api/Payment/Confirm` | Confirm payment, grants Odlikaš+ in Postgres + Firestore |
| POST | `/api/Payment/Cancel` | Cancel active Stripe subscription, revokes Odlikaš+ |

Full API documentation: [`API_DOCUMENTATION.md`](API_DOCUMENTATION.md)

---

## Deployment

The app is deployed on [Fly.io](https://fly.io) using the included `Dockerfile`. Database is hosted on [Neon](https://neon.tech).

EF Core migrations run automatically on startup — no manual migration step needed.

To redeploy manually:
```bash
fly deploy -a e-dnevnik-api
```

---

## Security

- Credentials never stored — only a session token tied to the live E-Dnevnik cookie
- All secrets managed via environment variables (no hardcoded values)
- Rate limiting: 20 requests/min per IP
- HSTS enforced in production
- Security headers: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`
- CORS blocks all browser origins in production (API is mobile-only)
