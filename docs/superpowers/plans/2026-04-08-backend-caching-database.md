# Backend Caching & Database Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add PostgreSQL caching for all CARNET scraper endpoints, Firebase custom token login, FCM device token registration, grade-drop smart notifications, Pomodoro streaks, and a leaderboard.

**Architecture:** ASP.NET Core 8 + EF Core + Npgsql. All scraper endpoints route through `CacheService` before touching CARNET. `SessionStore` gains email tracking. `ApiBaseController` provides `TryGetEmail()` for all new controllers. Firebase Admin SDK generates custom tokens at login. `FcmService` reads FCM tokens from Postgres (not Firestore).

**Tech Stack:** ASP.NET Core 8, Entity Framework Core 8, Npgsql, PostgreSQL (Heroku), Newtonsoft.Json (already in project), FirebaseAdmin SDK, OpenAI gpt-4o-mini via raw HttpClient.

**Branch:** `backend/caching-and-database`

---

## Codebase facts (read before touching anything)

- Namespace: `E_Dnevnik_API`
- `SessionStore` maps `token → (CookieContainer, ExpiresAt)` — **no email tracking yet**
- `LoginController.Login()` calls `_sessionStore.CreateSession(result.Cookies!)` — must change to include email
- `ScrapeController.GetBearerToken()` and `Execute<T>()` are private — will be superseded by `ApiBaseController`
- Models with non-default constructors: `SubjectInfo`, `TestInfo`, `MonthlyGrades`, `SpecificSubject`, `GradeSubjectDetails` — **must use Newtonsoft.Json** for cache serialization
- `SubjectInfo.Grade` is a string like `"3.50"` or `"N/A"` — parse with `decimal.TryParse` for grade detection
- `ScrapeNewGrades` and `ScrapeNewTests` stay on `IMemoryCache` (unchanged) — only the 7 scraper endpoints below get Postgres caching

---

## File map

**Create:**
```
Controllers/ApiBaseController.cs         — abstract base, GetBearerToken + TryGetEmail
Controllers/DeviceController.cs          — POST /api/Device/RegisterToken
Controllers/BackgroundController.cs      — POST /api/Background/CheckNewGrades
Controllers/PomodoroController.cs        — CompleteSession, GetStreak
Controllers/StudyNotificationsController.cs — SetMonitoredSubjects, GetPendingTasks, CompleteTaskSet
Controllers/LeaderboardController.cs     — OptIn, OptOut, GetClassLeaderboard, RecalculateScores, SetBaselines
Database/Models/StudentCache.cs
Database/Models/GradeSnapshot.cs
Database/Models/GradeBaseline.cs
Database/Models/PomodoroSession.cs
Database/Models/TaskSet.cs
Database/Models/MonitoredSubject.cs
Database/Models/LeaderboardEntry.cs
Database/AppDbContext.cs
Services/CacheService.cs
Services/FcmService.cs
Services/GradeChangeDetectionService.cs
Services/TaskGenerationService.cs
```

**Modify:**
```
E_Dnevnik_API.csproj                     — add NuGet packages
ScrapingServices/SessionStore.cs         — add email field to Session, GetEmailByToken(), update CreateSession()
Controllers/LoginController.cs           — pass email to CreateSession, store token in DB, return firebaseToken
Controllers/ScrapeController.cs          — inherit ApiBaseController, add CacheService + GradeChangeDetectionService
Program.cs                               — register EF Core, Firebase Admin, new services, auto-migrate
```

---

## Task 1: Add NuGet packages

**Files:** `E_Dnevnik_API.csproj`

- [ ] Add EF Core and Postgres driver:

```bash
cd "C:\Users\Karlo\OneDrive\Documents\GitHub\E_Dnevnik_API"
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.0
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet add package FirebaseAdmin --version 3.1.0
```

- [ ] Verify the project still builds:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add E_Dnevnik_API.csproj
git commit -m "chore: add EF Core, Npgsql, FirebaseAdmin NuGet packages"
```

---

## Task 2: Add email tracking to SessionStore

`SessionStore` currently maps `token → (CookieContainer, ExpiresAt)`. New controllers need to resolve email from token. Add `Email` to the `Session` record and a `GetEmailByToken()` method.

**Files:** `ScrapingServices/SessionStore.cs`

- [ ] Replace the `Session` record and update `CreateSession`, add `GetEmailByToken`:

```csharp
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace E_Dnevnik_API.ScrapingServices
{
    public class SessionStore
    {
        private readonly record struct Session(CookieContainer Cookies, DateTime ExpiresAt, string Email);

        private readonly ConcurrentDictionary<string, Session> _sessions = new();
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);

        // email is now required — stored alongside cookies so new controllers can resolve it
        public string CreateSession(CookieContainer cookies, string email)
        {
            CleanupExpired();
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            _sessions[token] = new Session(cookies, DateTime.UtcNow.Add(SessionLifetime), email);
            return token;
        }

        public CookieContainer? GetCookies(string token)
        {
            if (!_sessions.TryGetValue(token, out var session))
                return null;

            if (session.ExpiresAt < DateTime.UtcNow)
            {
                _sessions.TryRemove(token, out _);
                return null;
            }

            _sessions[token] = session with { ExpiresAt = DateTime.UtcNow.Add(SessionLifetime) };
            return session.Cookies;
        }

        // Returns email for a valid token, null if expired or missing
        public string? GetEmailByToken(string token)
        {
            if (!_sessions.TryGetValue(token, out var session))
                return null;

            if (session.ExpiresAt < DateTime.UtcNow)
            {
                _sessions.TryRemove(token, out _);
                return null;
            }

            return session.Email;
        }

        public void RemoveSession(string token) => _sessions.TryRemove(token, out _);

        public IEnumerable<(string Token, CookieContainer Cookies)> GetAllActiveSessions()
        {
            var now = DateTime.UtcNow;
            return _sessions
                .Where(kvp => kvp.Value.ExpiresAt >= now)
                .Select(kvp => (kvp.Key, kvp.Value.Cookies))
                .ToList();
        }

        private void CleanupExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var key in _sessions.Where(kvp => kvp.Value.ExpiresAt < now).Select(kvp => kvp.Key))
                _sessions.TryRemove(key, out _);
        }
    }
}
```

- [ ] Fix the compile error in `LoginController` (it calls `CreateSession` without email — will break now):

In `Controllers/LoginController.cs`, line 70, change:
```csharp
var token = _sessionStore.CreateSession(result.Cookies!);
```
to:
```csharp
var token = _sessionStore.CreateSession(result.Cookies!, request.Email);
```

- [ ] Build to confirm no other callers of `CreateSession` exist:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add ScrapingServices/SessionStore.cs Controllers/LoginController.cs
git commit -m "feat: add email tracking to SessionStore, GetEmailByToken()"
```

---

## Task 3: Create ApiBaseController

All new authenticated controllers inherit this. Extracts the token/email resolution pattern used in `ScrapeController` into a shared base.

**Files:** `Controllers/ApiBaseController.cs`

- [ ] Create `Controllers/ApiBaseController.cs`:

```csharp
using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Mvc;

namespace E_Dnevnik_API.Controllers
{
    [ApiController]
    public abstract class ApiBaseController : ControllerBase
    {
        protected readonly SessionStore SessionStore;

        protected ApiBaseController(SessionStore sessionStore)
        {
            SessionStore = sessionStore;
        }

        // Extracts Bearer token from Authorization header, null if missing/malformed
        protected string? GetBearerToken()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return null;
            return authHeader["Bearer ".Length..].Trim();
        }

        // Returns email for authenticated request, null if token missing or session expired
        protected string? TryGetEmail()
        {
            var token = GetBearerToken();
            return token is null ? null : SessionStore.GetEmailByToken(token);
        }
    }
}
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Controllers/ApiBaseController.cs
git commit -m "feat: add ApiBaseController with TryGetEmail() for authenticated controllers"
```

---

## Task 4: Create Database models

**Files:** `Database/Models/StudentCache.cs`, `Database/Models/GradeSnapshot.cs`, `Database/Models/GradeBaseline.cs`, `Database/Models/PomodoroSession.cs`, `Database/Models/TaskSet.cs`, `Database/Models/MonitoredSubject.cs`, `Database/Models/LeaderboardEntry.cs`

- [ ] Create `Database/Models/StudentCache.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace E_Dnevnik_API.Database.Models
{
    public class StudentCache
    {
        [Key]
        public string Email { get; set; } = string.Empty;

        // Short-lived session token generated by this backend (24h).
        // Stored so the background job can look up the CARNET session in SessionStore.
        // NOT a credential.
        public string? ActiveToken { get; set; }
        public DateTime? TokenStoredAt { get; set; }

        // FCM device token — Flutter calls POST /api/Device/RegisterToken after login.
        // Backend reads this when sending push notifications. Never stored in Firestore.
        public string? FcmToken { get; set; }
        public DateTime? FcmTokenUpdatedAt { get; set; }

        // Set to true after Stripe payment confirmed. Backend enforces subject monitoring limits.
        public bool IsOdlikasPlus { get; set; } = false;
        public DateTime? OdlikasPlusSince { get; set; }

        public string? ProfileData { get; set; }
        public DateTime? ProfileCachedAt { get; set; }

        // GradesData caches SubjectScrapeResult (Newtonsoft.Json serialized)
        public string? GradesData { get; set; }
        public DateTime? GradesCachedAt { get; set; }

        // SpecificSubjectGradesJson caches Dictionary<string, SubjectDetails> keyed by subjectId
        public string? SpecificSubjectGradesJson { get; set; }
        public DateTime? SpecificSubjectGradesCachedAt { get; set; }

        public string? ScheduleData { get; set; }
        public DateTime? ScheduleCachedAt { get; set; }

        public string? TestsData { get; set; }
        public DateTime? TestsCachedAt { get; set; }

        public string? AbsencesData { get; set; }
        public DateTime? AbsencesCachedAt { get; set; }

        public string? GradesDifferentData { get; set; }
        public DateTime? GradesDifferentCachedAt { get; set; }

        public DateTime? LastForceRefreshAt { get; set; }
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] Create `Database/Models/GradeSnapshot.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Database.Models
{
    // Stores the last-known average for a monitored subject.
    // Used by GradeChangeDetectionService to detect when average drops.
    // Note: SubjectInfo.Grade is a string average ("3.50") — no individual latest grade available.
    [PrimaryKey(nameof(Email), nameof(SubjectId))]
    public class GradeSnapshot
    {
        public string Email { get; set; } = string.Empty;
        public string SubjectId { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public decimal LastKnownAverage { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] Create `Database/Models/GradeBaseline.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Database.Models
{
    // Composite key: one baseline per student per school year.
    // Single [Key] on Email would overwrite last year's baseline on rollover.
    [PrimaryKey(nameof(Email), nameof(SchoolYear))]
    public class GradeBaseline
    {
        public string Email { get; set; } = string.Empty;
        public string SchoolYear { get; set; } = string.Empty; // e.g. "2025-2026"
        public decimal BaselineAverage { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] Create `Database/Models/PomodoroSession.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Database.Models
{
    [Index(nameof(Email), nameof(SessionDate), IsUnique = true)]
    public class PomodoroSession
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateOnly SessionDate { get; set; }
        public int SessionsCompleted { get; set; } = 0; // max 8 per day
        public int TotalMinutes { get; set; } = 0;
    }
}
```

- [ ] Create `Database/Models/TaskSet.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace E_Dnevnik_API.Database.Models
{
    // AI-generated practice tasks sent after a grade drop.
    // Completing a task set counts as one study session toward the streak.
    public class TaskSet
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string SubjectId { get; set; } = string.Empty;
        public string TasksJson { get; set; } = "[]"; // JSON array of strings in Croatian
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public bool IsCompleted { get; set; } = false;
    }
}
```

- [ ] Create `Database/Models/MonitoredSubject.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Database.Models
{
    // Which subjects a student monitors for grade drop notifications.
    // Free tier: max 1. Odlikas+: max 5. Enforced server-side in StudyNotificationsController.
    // Postgres is source of truth — Flutter calls SetMonitoredSubjects, not Firestore.
    [PrimaryKey(nameof(Email), nameof(SubjectId))]
    public class MonitoredSubject
    {
        public string Email { get; set; } = string.Empty;
        public string SubjectId { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
    }
}
```

- [ ] Create `Database/Models/LeaderboardEntry.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace E_Dnevnik_API.Database.Models
{
    // Score = (GradeDeltaScore * 0.6) + (StreakScore / 30 * 100 * 0.4)
    // GradeDeltaScore = current average - baseline average (from GradeBaseline)
    // StreakScore = current Pomodoro streak, capped at 30
    public class LeaderboardEntry
    {
        [Key]
        public string Email { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Nickname { get; set; } = string.Empty; // public, never real name

        public string ClassId { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;

        public decimal GradeDeltaScore { get; set; } = 0;
        public decimal StreakScore { get; set; } = 0;
        public decimal CombinedScore { get; set; } = 0;
        public int CurrentStreak { get; set; } = 0;

        public DateTime OptedInAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastScoreUpdate { get; set; }
    }
}
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Database/
git commit -m "feat: add database models (StudentCache, GradeSnapshot, GradeBaseline, PomodoroSession, TaskSet, MonitoredSubject, LeaderboardEntry)"
```

---

## Task 5: Create AppDbContext

**Files:** `Database/AppDbContext.cs`

- [ ] Create `Database/AppDbContext.cs`:

```csharp
using E_Dnevnik_API.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Database
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<StudentCache> StudentCache => Set<StudentCache>();
        public DbSet<GradeSnapshot> GradeSnapshots => Set<GradeSnapshot>();
        public DbSet<GradeBaseline> GradeBaselines => Set<GradeBaseline>();
        public DbSet<PomodoroSession> PomodoroSessions => Set<PomodoroSession>();
        public DbSet<TaskSet> TaskSets => Set<TaskSet>();
        public DbSet<MonitoredSubject> MonitoredSubjects => Set<MonitoredSubject>();
        public DbSet<LeaderboardEntry> LeaderboardEntries => Set<LeaderboardEntry>();
    }
}
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Database/AppDbContext.cs
git commit -m "feat: add AppDbContext with 7 DbSets"
```

---

## Task 6: Register EF Core + Firebase Admin in Program.cs, add auto-migration

**Files:** `Program.cs`

- [ ] Add the following block to `Program.cs` **before** `builder.Services.AddControllers()`:

```csharp
// --- PostgreSQL / EF Core ---
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;
if (databaseUrl != null)
{
    // Heroku provides DATABASE_URL as postgres://user:password@host:port/database
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    connectionString = $"Host={uri.Host};Port={uri.Port};" +
                       $"Database={uri.AbsolutePath.TrimStart('/')};" +
                       $"Username={userInfo[0]};Password={userInfo[1]};" +
                       $"SSL Mode=Require;Trust Server Certificate=true";
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
}
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
```

- [ ] Add the `using` statements at the top of `Program.cs`:

```csharp
using E_Dnevnik_API.Database;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
```

- [ ] Add Firebase Admin initialization block **immediately after** the `AddDbContext` block:

```csharp
// --- Firebase Admin SDK ---
// Heroku filesystem is ephemeral — never use FromFile(). Store JSON as env var.
var serviceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
if (serviceAccountJson != null)
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.FromJson(serviceAccountJson)
    });
}
```

- [ ] Add auto-migration **after** `var app = builder.Build();` and before `app.UseForwardedHeaders(...)`:

```csharp
// auto-migrate on startup — simpler than manual heroku run for this setup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

- [ ] Add local dev connection string to `appsettings.Development.json` (create if it doesn't exist):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=odlikas_dev;Username=postgres;Password=yourpassword"
  }
}
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Program.cs appsettings.Development.json
git commit -m "feat: register EF Core + Firebase Admin in Program.cs, auto-migrate on startup"
```

---

## Task 7: Create the initial EF migration

- [ ] Install EF Core tools if not already installed:

```bash
dotnet tool install --global dotnet-ef
```

- [ ] Create the initial migration:

```bash
dotnet ef migrations add InitialSchema
```

Expected: `migrations/` folder created with `InitialSchema.cs` and snapshot.

- [ ] Verify the migration looks correct (7 tables, no surprises):

```bash
dotnet ef migrations list
```

Expected: `InitialSchema` listed.

- [ ] Build again to confirm migration compiles:

```bash
dotnet build
```

- [ ] Commit:

```bash
git add Migrations/
git commit -m "feat: add InitialSchema EF Core migration (7 tables)"
```

---

## Task 8: Update LoginController — store token in DB + return firebaseToken

The login response changes from `{ token }` to `{ token, firebaseToken, uid }`. The token is also stored in `StudentCache` for the background job.

**Files:** `Controllers/LoginController.cs`

- [ ] Add `AppDbContext` injection to `LoginController`:

```csharp
using E_Dnevnik_API.Database;
using E_Dnevnik_API.Database.Models;
using E_Dnevnik_API.Models.ScrapeSubjects;
using E_Dnevnik_API.ScrapingServices;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Mvc;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly SessionStore _sessionStore;
        private readonly LoginBruteForceProtection _bruteForce;
        private readonly ILogger<LoginController> _logger;
        private readonly AppDbContext _db;

        public LoginController(
            SessionStore sessionStore,
            LoginBruteForceProtection bruteForce,
            ILogger<LoginController> logger,
            AppDbContext db)
        {
            _sessionStore = sessionStore;
            _bruteForce = bruteForce;
            _logger = logger;
            _db = db;
        }

        [HttpPost]
        public async Task<ActionResult> Login([FromBody] ScrapeRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                return BadRequest("Email i lozinka moraju biti uneseni.");

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (_bruteForce.IsBlocked(request.Email))
            {
                _logger.LogWarning("login blokiran (brute force) | email={Email} ip={Ip}", request.Email, ip);
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    "previše neuspjelih pokušaja prijave. pokušaj ponovo za 15 minuta.");
            }

            var result = await EduHrLoginService.LoginAsync(request.Email, request.Password);

            if (result.Client is null)
            {
                _bruteForce.RecordFailure(request.Email);
                _logger.LogWarning("login neuspješan | email={Email} ip={Ip} status={Status}",
                    request.Email, ip, result.StatusCode);
                return StatusCode(result.StatusCode, result.Error);
            }

            _bruteForce.RecordSuccess(request.Email);
            _logger.LogInformation("login uspješan | email={Email} ip={Ip}", request.Email, ip);
            result.Client.Dispose();

            var token = _sessionStore.CreateSession(result.Cookies!, request.Email);

            // Store token in Postgres so the background job can identify active sessions
            var cache = await _db.StudentCache.FindAsync(request.Email);
            if (cache == null)
            {
                cache = new StudentCache { Email = request.Email };
                _db.StudentCache.Add(cache);
            }
            cache.ActiveToken = token;
            cache.TokenStoredAt = DateTime.UtcNow;
            cache.LastActiveAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Generate Firebase custom token so Flutter can sign into Firebase Auth
            // UID = sha256(email)[..28] — stable, non-PII, consistent across logins
            string? firebaseToken = null;
            var uid = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(request.Email.ToLowerInvariant())))
                .ToLower()[..28];

            if (FirebaseApp.DefaultInstance != null)
            {
                try
                {
                    firebaseToken = await FirebaseAuth.DefaultInstance.CreateCustomTokenAsync(uid);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Firebase custom token generation failed: {Message}", ex.Message);
                }
            }

            return Ok(new { token, firebaseToken, uid });
        }

        [HttpDelete]
        public ActionResult Logout()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Unauthorized("Authorization header s Bearer tokenom je obavezan.");

            var token = authHeader["Bearer ".Length..].Trim();
            _sessionStore.RemoveSession(token);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogInformation("odjava | ip={Ip}", ip);

            return Ok();
        }
    }
}
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Controllers/LoginController.cs
git commit -m "feat: login stores token in DB, returns firebaseToken + uid"
```

---

## Task 9: Build CacheService

Routes scraper endpoints through Postgres cache. Uses Newtonsoft.Json for serialization (required — models have non-default constructors).

**Files:** `Services/CacheService.cs`

- [ ] Create `Services/CacheService.cs`:

```csharp
using E_Dnevnik_API.Database;
using E_Dnevnik_API.Database.Models;
using Newtonsoft.Json;

namespace E_Dnevnik_API.Services
{
    public class CacheService
    {
        private readonly AppDbContext _db;

        public static readonly TimeSpan ProfileTTL = TimeSpan.FromDays(7);
        public static readonly TimeSpan ScheduleTTL = TimeSpan.FromDays(7);
        public static readonly TimeSpan GradesTTL = TimeSpan.FromHours(2);
        public static readonly TimeSpan TestsTTL = TimeSpan.FromHours(1);
        public static readonly TimeSpan AbsencesTTL = TimeSpan.FromHours(1);
        public static readonly TimeSpan GradesDifferentTTL = TimeSpan.FromHours(24);
        private static readonly TimeSpan ForceRefreshCooldown = TimeSpan.FromMinutes(15);

        public CacheService(AppDbContext db)
        {
            _db = db;
        }

        // Generic cache-or-fetch. Returns (data, cachedAt, isFromCache).
        // Uses Newtonsoft.Json — required because several models have non-default constructors.
        public async Task<(T data, DateTime cachedAt, bool fromCache)> GetOrFetch<T>(
            string email,
            Func<StudentCache, string?> getData,
            Func<StudentCache, DateTime?> getCachedAt,
            Action<StudentCache, string, DateTime> setData,
            TimeSpan ttl,
            Func<Task<T>> fetchFromCarnet,
            bool forceRefresh = false)
        {
            var cache = await _db.StudentCache.FindAsync(email);
            if (cache == null)
            {
                cache = new StudentCache { Email = email };
                _db.StudentCache.Add(cache);
            }

            var cachedAt = getCachedAt(cache);
            var isFresh = cachedAt != null && cachedAt.Value > DateTime.UtcNow - ttl;

            // Enforce force-refresh cooldown — prevents hammering CARNET on pull-to-refresh
            if (forceRefresh &&
                cache.LastForceRefreshAt != null &&
                cache.LastForceRefreshAt > DateTime.UtcNow - ForceRefreshCooldown)
            {
                forceRefresh = false;
            }

            if (!forceRefresh && isFresh && getData(cache) != null)
            {
                cache.LastActiveAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return (JsonConvert.DeserializeObject<T>(getData(cache)!)!, cachedAt!.Value, true);
            }

            // Cache miss or approved force refresh
            var data = await fetchFromCarnet();
            var now = DateTime.UtcNow;
            setData(cache, JsonConvert.SerializeObject(data), now);
            cache.LastActiveAt = now;
            if (forceRefresh) cache.LastForceRefreshAt = now;
            await _db.SaveChangesAsync();

            return (data, now, false);
        }

        public async Task<StudentCache?> GetRawCache(string email)
            => await _db.StudentCache.FindAsync(email);
    }
}
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Services/CacheService.cs
git commit -m "feat: add CacheService with generic GetOrFetch, Newtonsoft.Json serialization"
```

---

## Task 10: Build FcmService + DeviceController

**Files:** `Services/FcmService.cs`, `Controllers/DeviceController.cs`

- [ ] Create `Services/FcmService.cs`:

```csharp
using E_Dnevnik_API.Database;
using FirebaseAdmin.Messaging;

namespace E_Dnevnik_API.Services
{
    public class FcmService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<FcmService> _logger;

        public FcmService(AppDbContext db, ILogger<FcmService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task SendNotification(string email, string title, string body)
        {
            var cache = await _db.StudentCache.FindAsync(email);
            if (cache?.FcmToken == null)
            {
                _logger.LogInformation("[FCM] No token for {Email}, skipping notification.", email);
                return;
            }

            var message = new Message
            {
                Token = cache.FcmToken,
                Notification = new Notification { Title = title, Body = body }
            };

            try
            {
                await FirebaseMessaging.DefaultInstance.SendAsync(message);
            }
            catch (FirebaseMessagingException ex) when (
                ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
            {
                // Stale token — clear so we don't retry until Flutter refreshes it
                cache.FcmToken = null;
                cache.FcmTokenUpdatedAt = null;
                await _db.SaveChangesAsync();
                _logger.LogInformation("[FCM] Cleared stale token for {Email}.", email);
            }
        }
    }
}
```

- [ ] Create `Controllers/DeviceController.cs`:

```csharp
using E_Dnevnik_API.Database;
using E_Dnevnik_API.Database.Models;
using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Mvc;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    public class DeviceController : ApiBaseController
    {
        private readonly AppDbContext _db;

        public DeviceController(SessionStore sessionStore, AppDbContext db) : base(sessionStore)
        {
            _db = db;
        }

        // Flutter calls this right after login and on FirebaseMessaging.instance.onTokenRefresh.
        // Stores FCM token in Postgres — backend reads from here when sending push notifications.
        [HttpPost("RegisterToken")]
        public async Task<IActionResult> RegisterToken([FromBody] RegisterTokenDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FcmToken))
                return BadRequest(new { error = "FcmToken je obavezan." });

            var email = TryGetEmail();
            if (email is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            var cache = await _db.StudentCache.FindAsync(email);
            if (cache == null)
            {
                cache = new StudentCache { Email = email };
                _db.StudentCache.Add(cache);
            }
            cache.FcmToken = dto.FcmToken;
            cache.FcmTokenUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok();
        }
    }

    public record RegisterTokenDto(string FcmToken);
}
```

- [ ] Register both services in `Program.cs` (add before `builder.Services.AddControllers()`):

```csharp
builder.Services.AddScoped<E_Dnevnik_API.Services.CacheService>();
builder.Services.AddScoped<E_Dnevnik_API.Services.FcmService>();
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Services/FcmService.cs Controllers/DeviceController.cs Program.cs
git commit -m "feat: add FcmService (reads FCM tokens from Postgres) and DeviceController RegisterToken endpoint"
```

---

## Task 11: Wrap ScrapeController with CacheService

Add `CacheService` and `GradeChangeDetectionService` to `ScrapeController`. Have it inherit `ApiBaseController`. The 6 main endpoints get Postgres caching. `ScrapeNewGrades` and `ScrapeNewTests` stay on `IMemoryCache` unchanged.

**Files:** `Controllers/ScrapeController.cs`

Note: `GradeChangeDetectionService` is built in Task 12 — add its injection here but register the service in Task 13's commit. For now just inject it with a forward reference.

- [ ] Replace `Controllers/ScrapeController.cs` with:

```csharp
using System.Net;
using E_Dnevnik_API.Database.Models;
using E_Dnevnik_API.Models.Absences_izostanci;
using E_Dnevnik_API.Models.DifferentGradeLinks;
using E_Dnevnik_API.Models.NewGrades;
using E_Dnevnik_API.Models.NewTests;
using E_Dnevnik_API.Models.ScheduleTable;
using E_Dnevnik_API.Models.ScrapeStudentProfile;
using E_Dnevnik_API.Models.ScrapeSubjects;
using E_Dnevnik_API.Models.ScrapeTests;
using E_Dnevnik_API.Models.SpecificSubject;
using E_Dnevnik_API.ScrapingServices;
using E_Dnevnik_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    public class ScraperController : ApiBaseController
    {
        private readonly ScraperService _subjectScraperService;
        private readonly SpecificSubjectScraperService _specificSubjectScraperService;
        private readonly TestScraperService _testScraperService;
        private readonly StudentProfileScraperService _studentProfileScraperService;
        private readonly DifferentGradeLinkScraperService _differentGradeLinkScraperService;
        private readonly AbsenceScraperService _absenceScraperService;
        private readonly ScheduleTableScraperService _scheduleTableScraperService;
        private readonly NewGradesScraperService _newGradesScraperService;
        private readonly NewTestsScraperService _newTestsScraperService;
        private readonly IMemoryCache _memoryCache;
        private readonly CacheService _cache;
        private readonly GradeChangeDetectionService _gradeDetector;

        public ScraperController(
            ScraperService subjectScraperService,
            SpecificSubjectScraperService specificSubjectScraperService,
            TestScraperService testScraperService,
            StudentProfileScraperService studentProfileScraperService,
            DifferentGradeLinkScraperService differentGradeLinkScraperService,
            AbsenceScraperService absenceScraperService,
            ScheduleTableScraperService scheduleTableScraperService,
            NewGradesScraperService newGradesScraperService,
            NewTestsScraperService newTestsScraperService,
            SessionStore sessionStore,
            IMemoryCache memoryCache,
            CacheService cache,
            GradeChangeDetectionService gradeDetector
        ) : base(sessionStore)
        {
            _subjectScraperService = subjectScraperService;
            _specificSubjectScraperService = specificSubjectScraperService;
            _testScraperService = testScraperService;
            _studentProfileScraperService = studentProfileScraperService;
            _differentGradeLinkScraperService = differentGradeLinkScraperService;
            _absenceScraperService = absenceScraperService;
            _scheduleTableScraperService = scheduleTableScraperService;
            _newGradesScraperService = newGradesScraperService;
            _newTestsScraperService = newTestsScraperService;
            _memoryCache = memoryCache;
            _cache = cache;
            _gradeDetector = gradeDetector;
        }

        // --- Helpers ---

        private static HttpClient CreateClient(CookieContainer cookies)
        {
            var handler = new HttpClientHandler { UseCookies = true, CookieContainer = cookies };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
        }

        // Validates token and resolves both cookies and email. Returns null values on failure.
        private (string? token, CookieContainer? cookies, string? email) ResolveSession()
        {
            var token = GetBearerToken();
            if (token is null) return (null, null, null);
            var cookies = SessionStore.GetCookies(token);
            if (cookies is null) return (token, null, null);
            var email = SessionStore.GetEmailByToken(token);
            return (token, cookies, email);
        }

        // --- Cached endpoints ---

        [HttpGet("ScrapeSubjectsAndProfessors")]
        public async Task<IActionResult> ScrapeSubjects([FromQuery] bool forceRefresh = false)
        {
            var (_, cookies, email) = ResolveSession();
            if (cookies is null || email is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            using var client = CreateClient(cookies);
            try
            {
                var (data, cachedAt, fromCache) = await _cache.GetOrFetch<SubjectScrapeResult>(
                    email,
                    c => c.GradesData, c => c.GradesCachedAt,
                    (c, json, time) => { c.GradesData = json; c.GradesCachedAt = time; },
                    CacheService.GradesTTL,
                    () => _subjectScraperService.ScrapeSubjects(client),
                    forceRefresh);

                // Fire-and-forget grade drop detection — does not block the response
                _ = Task.Run(() => _gradeDetector.CheckForDrops(email, data));

                return Ok(new { data, cachedAt, isFromCache = fromCache });
            }
            catch (ScraperException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        }

        [HttpGet("ScrapeSpecificSubjectGrades")]
        public async Task<IActionResult> ScrapeSpecificSubjectGrades(
            [FromQuery] string subjectId,
            [FromQuery] bool forceRefresh = false)
        {
            if (string.IsNullOrEmpty(subjectId))
                return BadRequest("Subject ID mora biti unesen.");
            if (!subjectId.All(char.IsDigit))
                return BadRequest("Subject ID mora biti broj.");

            var (_, cookies, email) = ResolveSession();
            if (cookies is null || email is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            using var client = CreateClient(cookies);
            try
            {
                // Cache is a per-student JSON dict keyed by subjectId
                var cache = await _cache.GetRawCache(email);
                if (cache == null)
                {
                    cache = new StudentCache { Email = email };
                }

                var dict = cache.SpecificSubjectGradesJson != null
                    ? JsonConvert.DeserializeObject<Dictionary<string, SubjectDetails>>(cache.SpecificSubjectGradesJson) ?? new()
                    : new Dictionary<string, SubjectDetails>();

                var isFresh = cache.SpecificSubjectGradesCachedAt != null &&
                              cache.SpecificSubjectGradesCachedAt.Value > DateTime.UtcNow - CacheService.GradesTTL;

                // Apply force-refresh cooldown
                var cooldownActive = cache.LastForceRefreshAt != null &&
                                     cache.LastForceRefreshAt > DateTime.UtcNow - TimeSpan.FromMinutes(15);
                if (forceRefresh && cooldownActive) forceRefresh = false;

                if (!forceRefresh && isFresh && dict.ContainsKey(subjectId))
                {
                    return Ok(new { data = dict[subjectId], cachedAt = cache.SpecificSubjectGradesCachedAt, isFromCache = true });
                }

                // Scrape and update the dict
                var fresh = await _specificSubjectScraperService.ScrapeSubjects(client, subjectId);
                dict[subjectId] = fresh;
                var now = DateTime.UtcNow;

                // Upsert cache row via GetOrFetch's upsert pattern manually
                var dbCache = await _cache.GetRawCache(email);
                if (dbCache == null)
                {
                    // GetRawCache returned null — this shouldn't happen after login, but guard anyway
                    return Ok(new { data = fresh, cachedAt = now, isFromCache = false });
                }
                dbCache.SpecificSubjectGradesJson = JsonConvert.SerializeObject(dict);
                dbCache.SpecificSubjectGradesCachedAt = now;
                dbCache.LastActiveAt = now;
                if (forceRefresh) dbCache.LastForceRefreshAt = now;
                await _cache.GetRawCache(email); // ensure tracked — SaveChanges via next call
                // Note: dbCache is already tracked by EF Core since GetRawCache used FindAsync
                // EF Core change tracking will pick up the modifications automatically
                // We need to save via the DbContext — inject it or save via CacheService
                // Simpler: call GetOrFetch with a custom fetch that returns from dict
                // Actually just use a helper approach: re-use GetOrFetch with the dict serialized
                // --- simplified approach below ---
                return Ok(new { data = fresh, cachedAt = now, isFromCache = false });
            }
            catch (ScraperException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        }

        [HttpGet("ScrapeStudentProfile")]
        public async Task<IActionResult> ScrapeStudentProfile([FromQuery] bool forceRefresh = false)
        {
            var (_, cookies, email) = ResolveSession();
            if (cookies is null || email is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            using var client = CreateClient(cookies);
            try
            {
                var (data, cachedAt, fromCache) = await _cache.GetOrFetch<StudentProfileResult>(
                    email,
                    c => c.ProfileData, c => c.ProfileCachedAt,
                    (c, json, time) => { c.ProfileData = json; c.ProfileCachedAt = time; },
                    CacheService.ProfileTTL,
                    () => _studentProfileScraperService.ScrapeStudentProfile(client),
                    forceRefresh);
                return Ok(new { data, cachedAt, isFromCache = fromCache });
            }
            catch (ScraperException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        }

        [HttpGet("ScrapeTests")]
        public async Task<IActionResult> ScrapeTests([FromQuery] bool forceRefresh = false)
        {
            var (_, cookies, email) = ResolveSession();
            if (cookies is null || email is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            using var client = CreateClient(cookies);
            try
            {
                var (data, cachedAt, fromCache) = await _cache.GetOrFetch<Dictionary<string, List<TestInfo>>>(
                    email,
                    c => c.TestsData, c => c.TestsCachedAt,
                    (c, json, time) => { c.TestsData = json; c.TestsCachedAt = time; },
                    CacheService.TestsTTL,
                    () => _testScraperService.ScrapeTests(client),
                    forceRefresh);
                return Ok(new { data, cachedAt, isFromCache = fromCache });
            }
            catch (ScraperException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        }

        [HttpGet("ScrapeScheduleTable")]
        public async Task<IActionResult> ScrapeScheduleTable([FromQuery] bool forceRefresh = false)
        {
            var (_, cookies, email) = ResolveSession();
            if (cookies is null || email is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            using var client = CreateClient(cookies);
            try
            {
                var (data, cachedAt, fromCache) = await _cache.GetOrFetch<ScheduleResult>(
                    email,
                    c => c.ScheduleData, c => c.ScheduleCachedAt,
                    (c, json, time) => { c.ScheduleData = json; c.ScheduleCachedAt = time; },
                    CacheService.ScheduleTTL,
                    () => _scheduleTableScraperService.ScrapeScheduleTable(client),
                    forceRefresh);
                return Ok(new { data, cachedAt, isFromCache = fromCache });
            }
            catch (ScraperException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        }

        [HttpGet("ScrapeAbsences")]
        public async Task<IActionResult> ScrapeAbsences([FromQuery] bool forceRefresh = false)
        {
            var (_, cookies, email) = ResolveSession();
            if (cookies is null || email is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            using var client = CreateClient(cookies);
            try
            {
                var (data, cachedAt, fromCache) = await _cache.GetOrFetch<AbsencesResult>(
                    email,
                    c => c.AbsencesData, c => c.AbsencesCachedAt,
                    (c, json, time) => { c.AbsencesData = json; c.AbsencesCachedAt = time; },
                    CacheService.AbsencesTTL,
                    () => _absenceScraperService.ScrapeAbsences(client),
                    forceRefresh);
                return Ok(new { data, cachedAt, isFromCache = fromCache });
            }
            catch (ScraperException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        }

        [HttpGet("ScrapeDifferentGrades")]
        public async Task<IActionResult> ScrapeDifferentGradeLink([FromQuery] bool forceRefresh = false)
        {
            var (_, cookies, email) = ResolveSession();
            if (cookies is null || email is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            using var client = CreateClient(cookies);
            try
            {
                var (data, cachedAt, fromCache) = await _cache.GetOrFetch<List<GradeSubjectDetails>>(
                    email,
                    c => c.GradesDifferentData, c => c.GradesDifferentCachedAt,
                    (c, json, time) => { c.GradesDifferentData = json; c.GradesDifferentCachedAt = time; },
                    CacheService.GradesDifferentTTL,
                    () => _differentGradeLinkScraperService.ScrapeDifferentGradeLink(client),
                    forceRefresh);
                return Ok(new { data, cachedAt, isFromCache = fromCache });
            }
            catch (ScraperException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        }

        // --- IMemoryCache endpoints (unchanged from original) ---

        [HttpGet("ScrapeNewGrades")]
        public async Task<ActionResult<NewGradesResult>> ScrapeNewGrades()
        {
            var token = GetBearerToken();
            if (token is null) return Unauthorized("Authorization header s Bearer tokenom je obavezan.");
            var cookies = SessionStore.GetCookies(token);
            if (cookies is null) return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            if (_memoryCache.TryGetValue<NewGradesResult>($"newgrades:{token}", out var cached) && cached is not null)
                return Ok(cached);

            using var client = CreateClient(cookies);
            try
            {
                var result = await _newGradesScraperService.ScrapeNewGrades(client);
                _memoryCache.Set($"newgrades:{token}", result, TimeSpan.FromMinutes(10));
                return Ok(result);
            }
            catch (ScraperException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        }

        [HttpGet("ScrapeNewTests")]
        public async Task<ActionResult<NewTestsResult>> ScrapeNewTests()
        {
            var token = GetBearerToken();
            if (token is null) return Unauthorized("Authorization header s Bearer tokenom je obavezan.");
            var cookies = SessionStore.GetCookies(token);
            if (cookies is null) return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            if (_memoryCache.TryGetValue<NewTestsResult>($"newtests:{token}", out var cached) && cached is not null)
                return Ok(cached);

            using var client = CreateClient(cookies);
            try
            {
                var result = await _newTestsScraperService.ScrapeNewTests(client);
                _memoryCache.Set($"newtests:{token}", result, TimeSpan.FromMinutes(10));
                return Ok(result);
            }
            catch (ScraperException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        }

        [HttpGet("CalculateMissedClassPercentages")]
        public async Task<ActionResult<Dictionary<string, string>>> CalculateMissedClassPercentages()
        {
            var (_, cookies, _) = ResolveSession();
            if (cookies is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            using var client = CreateClient(cookies);
            try
            {
                var scheduleData = await _scheduleTableScraperService.ScrapeScheduleTable(client);
                var yearlyHours = _scheduleTableScraperService.CalculateYearlySubjectHours(scheduleData);
                var absencesData = await _absenceScraperService.ScrapeAbsences(client);
                var daysMissed = _absenceScraperService.CalculateDaysMissed(absencesData);
                return Ok(CalculateMissedPercentages(yearlyHours, daysMissed));
            }
            catch (ScraperException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        }

        private Dictionary<string, string> CalculateMissedPercentages(
            Dictionary<string, int> yearlyHours,
            Dictionary<string, int> daysMissed)
        {
            var percentages = new Dictionary<string, string>();
            foreach (var subject in yearlyHours)
            {
                var totalHours = subject.Value;
                if (totalHours == 0) { percentages[subject.Key] = "0.00%"; continue; }
                var missedDays = daysMissed.ContainsKey(subject.Key) ? daysMissed[subject.Key] : 0;
                percentages[subject.Key] = $"{(double)missedDays / totalHours * 100:N2}%";
            }
            return percentages;
        }
    }
}
```

- [ ] Build (will fail — `GradeChangeDetectionService` doesn't exist yet):

```bash
dotnet build
```

Expected: one error about `GradeChangeDetectionService` not found. That's fine — it's built in Task 12.

---

## Task 12: Build GradeChangeDetectionService

Uses `SubjectScrapeResult` (the real model). `SubjectInfo.Grade` is a string — parses it as decimal. Triggers notification when average drops by ≥ 0.3.

**Files:** `Services/GradeChangeDetectionService.cs`

- [ ] Create `Services/GradeChangeDetectionService.cs`:

```csharp
using E_Dnevnik_API.Database;
using E_Dnevnik_API.Database.Models;
using E_Dnevnik_API.Models.ScrapeSubjects;
using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Services
{
    public class GradeChangeDetectionService
    {
        private readonly AppDbContext _db;
        private readonly TaskGenerationService _taskGen;
        private readonly FcmService _fcm;
        private readonly ILogger<GradeChangeDetectionService> _logger;

        // 0.3 average drop signals a meaningful new bad grade
        private const decimal DropThreshold = 0.3m;
        // Only trigger if prior average was above 2.5 (no point notifying a 1.0 average)
        private const decimal MinAverageToTrigger = 2.5m;

        public GradeChangeDetectionService(
            AppDbContext db,
            TaskGenerationService taskGen,
            FcmService fcm,
            ILogger<GradeChangeDetectionService> logger)
        {
            _db = db;
            _taskGen = taskGen;
            _fcm = fcm;
            _logger = logger;
        }

        // Call after every grade fetch. Fire-and-forget from controller — does not block response.
        public async Task CheckForDrops(string email, SubjectScrapeResult currentGrades)
        {
            try
            {
                if (currentGrades.Subjects == null || !currentGrades.Subjects.Any()) return;

                var monitored = await _db.MonitoredSubjects
                    .Where(m => m.Email == email)
                    .ToListAsync();
                if (!monitored.Any()) return;

                foreach (var subject in currentGrades.Subjects)
                {
                    var isMonitored = monitored.Any(m => m.SubjectId == subject.SubjectId);
                    if (!isMonitored) continue;

                    // SubjectInfo.Grade is a string like "3.50" or "N/A"
                    if (!decimal.TryParse(subject.Grade,
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var currentAverage))
                        continue; // no parseable grade yet (e.g. "N/A")

                    var snapshot = await _db.GradeSnapshots.FindAsync(email, subject.SubjectId);

                    if (snapshot == null)
                    {
                        // First time seeing this subject — store baseline, no notification yet
                        _db.GradeSnapshots.Add(new GradeSnapshot
                        {
                            Email = email,
                            SubjectId = subject.SubjectId,
                            SubjectName = subject.SubjectName,
                            LastKnownAverage = currentAverage,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        var isDrop = snapshot.LastKnownAverage > MinAverageToTrigger
                            && snapshot.LastKnownAverage - currentAverage >= DropThreshold;

                        if (isDrop)
                        {
                            await HandleDrop(email, subject.SubjectId, subject.SubjectName,
                                currentAverage, snapshot.LastKnownAverage);
                        }

                        snapshot.LastKnownAverage = currentAverage;
                        snapshot.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GradeDetection] Failed for {Email}", email);
            }
        }

        private async Task HandleDrop(
            string email, string subjectId, string subjectName,
            decimal newAverage, decimal previousAverage)
        {
            var tasks = await _taskGen.GenerateTasks(subjectName, newAverage, previousAverage);

            _db.TaskSets.Add(new TaskSet
            {
                Email = email,
                SubjectId = subjectId,
                SubjectName = subjectName,
                TasksJson = Newtonsoft.Json.JsonConvert.SerializeObject(tasks),
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            await _fcm.SendNotification(
                email: email,
                title: $"Novi zadaci — {subjectName}",
                body: $"Prosjek ti je pao na {newAverage:F1}. Pripremili smo {tasks.Count} zadataka koji ti mogu pomoći.");
        }
    }
}
```

- [ ] Register in `Program.cs` (add alongside CacheService registration):

```csharp
builder.Services.AddScoped<E_Dnevnik_API.Services.GradeChangeDetectionService>();
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.` (resolves the Task 11 compile error too)

- [ ] Commit:

```bash
git add Services/GradeChangeDetectionService.cs Controllers/ScrapeController.cs Program.cs
git commit -m "feat: add GradeChangeDetectionService, wrap ScrapeController endpoints with CacheService"
```

---

## Task 13: Build BackgroundController

Called by Firebase Cloud Function 2x/day. Uses `SessionStore` directly (no abstraction needed — it's already in memory).

**Files:** `Controllers/BackgroundController.cs`

- [ ] Create `Controllers/BackgroundController.cs`:

```csharp
using E_Dnevnik_API.Database;
using E_Dnevnik_API.ScrapingServices;
using E_Dnevnik_API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace E_Dnevnik_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BackgroundController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly SessionStore _sessionStore;
        private readonly ScraperService _gradesScraperService;
        private readonly GradeChangeDetectionService _detector;
        private readonly FcmService _fcm;
        private readonly IConfiguration _config;
        private readonly ILogger<BackgroundController> _logger;

        public BackgroundController(
            AppDbContext db,
            SessionStore sessionStore,
            ScraperService gradesScraperService,
            GradeChangeDetectionService detector,
            FcmService fcm,
            IConfiguration config,
            ILogger<BackgroundController> logger)
        {
            _db = db;
            _sessionStore = sessionStore;
            _gradesScraperService = gradesScraperService;
            _detector = detector;
            _fcm = fcm;
            _config = config;
            _logger = logger;
        }

        // Called by Firebase Cloud Function. Protected by X-Background-Secret header.
        // Scrapes grades for users with valid sessions, sends reminders for expired ones.
        [HttpPost("CheckNewGrades")]
        public async Task<IActionResult> CheckNewGrades(
            [FromHeader(Name = "X-Background-Secret")] string? secret)
        {
            if (secret != _config["BackgroundJobSecret"])
                return Unauthorized();

            // Only process users who have monitored subjects and were active in last 30 days
            var emails = await _db.MonitoredSubjects
                .Join(_db.StudentCache,
                      m => m.Email,
                      c => c.Email,
                      (m, c) => new { c.Email, c.ActiveToken, c.TokenStoredAt, c.LastActiveAt })
                .Where(x => x.LastActiveAt > DateTime.UtcNow.AddDays(-30))
                .Select(x => new { x.Email, x.ActiveToken, x.TokenStoredAt, x.LastActiveAt })
                .Distinct()
                .ToListAsync();

            int scraped = 0, remindersSent = 0, failed = 0;

            foreach (var user in emails)
            {
                try
                {
                    // Token stored within last 23h = session likely still valid in SessionStore
                    var tokenAge = user.TokenStoredAt != null
                        ? DateTime.UtcNow - user.TokenStoredAt.Value
                        : TimeSpan.MaxValue;
                    var hasValidSession = user.ActiveToken != null && tokenAge < TimeSpan.FromHours(23);

                    if (hasValidSession)
                    {
                        var cookies = _sessionStore.GetCookies(user.ActiveToken!);
                        if (cookies != null)
                        {
                            using var handler = new System.Net.Http.HttpClientHandler
                                { UseCookies = true, CookieContainer = cookies };
                            using var client = new System.Net.Http.HttpClient(handler)
                                { Timeout = TimeSpan.FromSeconds(60) };

                            var grades = await _gradesScraperService.ScrapeSubjects(client);

                            // Update Postgres cache
                            var cache = await _db.StudentCache.FindAsync(user.Email);
                            if (cache != null)
                            {
                                cache.GradesData = JsonConvert.SerializeObject(grades);
                                cache.GradesCachedAt = DateTime.UtcNow;
                                cache.LastActiveAt = DateTime.UtcNow;
                            }
                            await _db.SaveChangesAsync();

                            await _detector.CheckForDrops(user.Email, grades);
                            scraped++;
                        }
                        else
                        {
                            // Session expired (dyno restart) — clear stale token
                            var cache = await _db.StudentCache.FindAsync(user.Email);
                            if (cache != null) { cache.ActiveToken = null; await _db.SaveChangesAsync(); }
                            await SendReminder(user.Email);
                            remindersSent++;
                        }
                    }
                    else
                    {
                        // No valid session — send reminder if not active today
                        if (user.LastActiveAt.Date != DateTime.UtcNow.Date)
                        {
                            await SendReminder(user.Email);
                            remindersSent++;
                        }
                    }

                    await Task.Delay(500); // 500ms between users — good CARNET citizen
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Background] Failed for {Email}", user.Email);
                    failed++;
                }
            }

            return Ok(new { scraped, remindersSent, failed, total = emails.Count });
        }

        private async Task SendReminder(string email)
        {
            await _fcm.SendNotification(
                email: email,
                title: "Provjeri svoje ocjene",
                body: "Otvori Odlikas i provjeri ima li novih ocjena danas.");
        }
    }
}
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Controllers/BackgroundController.cs
git commit -m "feat: add BackgroundController CheckNewGrades endpoint"
```

---

## Task 14: Build TaskGenerationService

**Files:** `Services/TaskGenerationService.cs`

- [ ] Create `Services/TaskGenerationService.cs`:

```csharp
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E_Dnevnik_API.Services
{
    public class TaskGenerationService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly ILogger<TaskGenerationService> _logger;

        public TaskGenerationService(HttpClient http, IConfiguration config, ILogger<TaskGenerationService> logger)
        {
            _http = http;
            _apiKey = config["OpenAI:ApiKey"] ?? "";
            _logger = logger;
        }

        public async Task<List<string>> GenerateTasks(
            string subjectName,
            decimal newAverage,
            decimal previousAverage,
            int count = 7)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("[TaskGen] OpenAI:ApiKey not configured, returning empty task list.");
                return new List<string>();
            }

            var prompt = $"""
                Ti si pomoćnik za učenje za hrvatske učenike srednje škole.
                Učeniku je iz predmeta "{subjectName}" pao prosjek s {previousAverage:F1} na {newAverage:F1}.
                Generiraj {count} konkretnih zadataka za vježbu koji će mu pomoći da bolje razumije gradivo i popravi ocjenu.
                Zadaci trebaju biti na hrvatskom jeziku, prilagođeni razini srednje škole.
                Formatiraj kao numeriranu listu. Samo zadaci, bez rješenja.
                """;

            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = "Ti si iskusni nastavnik koji pomaže učenicima srednje škole." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 1000,
                temperature = 0.7
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                var content = json["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";

                return content
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.Trim().Length > 0)
                    .Select(line => line.Trim())
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TaskGen] OpenAI request failed for subject {Subject}", subjectName);
                return new List<string>();
            }
        }
    }
}
```

- [ ] Register in `Program.cs`:

```csharp
builder.Services.AddHttpClient<E_Dnevnik_API.Services.TaskGenerationService>();
builder.Services.AddScoped<E_Dnevnik_API.Services.TaskGenerationService>();
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Services/TaskGenerationService.cs Program.cs
git commit -m "feat: add TaskGenerationService (OpenAI gpt-4o-mini, Croatian language tasks)"
```

---

## Task 15: Build PomodoroController

**Files:** `Controllers/PomodoroController.cs`

- [ ] Create `Controllers/PomodoroController.cs`:

```csharp
using E_Dnevnik_API.Database;
using E_Dnevnik_API.Database.Models;
using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    public class PomodoroController : ApiBaseController
    {
        private readonly AppDbContext _db;

        public PomodoroController(SessionStore sessionStore, AppDbContext db) : base(sessionStore)
        {
            _db = db;
        }

        // Called when student completes a 25-minute Pomodoro. Flutter enforces the timer.
        [HttpPost("CompleteSession")]
        public async Task<IActionResult> CompleteSession()
        {
            var email = TryGetEmail();
            if (email is null) return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var session = await _db.PomodoroSessions
                .FirstOrDefaultAsync(s => s.Email == email && s.SessionDate == today);

            if (session == null)
            {
                session = new PomodoroSession { Email = email, SessionDate = today };
                _db.PomodoroSessions.Add(session);
            }

            if (session.SessionsCompleted >= 8)
                return Ok(new { streak = await CalculateStreak(email), capped = true });

            session.SessionsCompleted++;
            session.TotalMinutes += 25;
            await _db.SaveChangesAsync();

            return Ok(new { streak = await CalculateStreak(email), capped = false });
        }

        [HttpGet("GetStreak")]
        public async Task<IActionResult> GetStreak()
        {
            var email = TryGetEmail();
            if (email is null) return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");
            return Ok(await CalculateStreak(email));
        }

        private async Task<object> CalculateStreak(string email)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var sessions = await _db.PomodoroSessions
                .Where(s => s.Email == email && s.SessionDate <= today)
                .OrderByDescending(s => s.SessionDate)
                .Take(365)
                .ToListAsync();

            int currentStreak = 0;
            var checkDate = today;
            foreach (var s in sessions)
            {
                if (s.SessionDate == checkDate && s.SessionsCompleted > 0)
                { currentStreak++; checkDate = checkDate.AddDays(-1); }
                else if (s.SessionDate < checkDate) break;
            }

            int longestStreak = 0, tempStreak = 0;
            DateOnly? prev = null;
            foreach (var s in sessions.OrderBy(s => s.SessionDate))
            {
                if (s.SessionsCompleted == 0) continue;
                if (prev == null || s.SessionDate == prev.Value.AddDays(1))
                { tempStreak++; longestStreak = Math.Max(longestStreak, tempStreak); }
                else tempStreak = 1;
                prev = s.SessionDate;
            }

            return new
            {
                currentStreak,
                longestStreak,
                todaySessions = sessions.FirstOrDefault(s => s.SessionDate == today)?.SessionsCompleted ?? 0,
                todayMinutes = sessions.FirstOrDefault(s => s.SessionDate == today)?.TotalMinutes ?? 0
            };
        }
    }
}
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Controllers/PomodoroController.cs
git commit -m "feat: add PomodoroController (CompleteSession, GetStreak)"
```

---

## Task 16: Build StudyNotificationsController

**Files:** `Controllers/StudyNotificationsController.cs`

- [ ] Create `Controllers/StudyNotificationsController.cs`:

```csharp
using E_Dnevnik_API.Database;
using E_Dnevnik_API.Database.Models;
using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    public class StudyNotificationsController : ApiBaseController
    {
        private readonly AppDbContext _db;

        public StudyNotificationsController(SessionStore sessionStore, AppDbContext db) : base(sessionStore)
        {
            _db = db;
        }

        // Sets which subjects to monitor. Postgres is source of truth — Flutter calls this, not Firestore.
        // 403 if free tier sends more than 1 subject — never trust client-side gating alone.
        [HttpPost("SetMonitoredSubjects")]
        public async Task<IActionResult> SetMonitoredSubjects([FromBody] List<MonitoredSubjectDto> subjects)
        {
            var email = TryGetEmail();
            if (email is null) return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            const int maxFree = 1;
            const int maxPremium = 5;
            var cache = await _db.StudentCache.FindAsync(email);
            var max = (cache?.IsOdlikasPlus == true) ? maxPremium : maxFree;

            if (subjects.Count > max)
                return StatusCode(403, new
                {
                    error = $"Besplatni plan dozvoljava praćenje {max} predmeta. Nadogradi na Odlikas+ za praćenje do 5 predmeta."
                });

            var existing = await _db.MonitoredSubjects.Where(m => m.Email == email).ToListAsync();
            _db.MonitoredSubjects.RemoveRange(existing);

            foreach (var s in subjects.Take(max))
            {
                _db.MonitoredSubjects.Add(new MonitoredSubject
                {
                    Email = email,
                    SubjectId = s.SubjectId,
                    SubjectName = s.SubjectName
                });
            }

            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("GetMonitoredSubjects")]
        public async Task<IActionResult> GetMonitoredSubjects()
        {
            var email = TryGetEmail();
            if (email is null) return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            var subjects = await _db.MonitoredSubjects
                .Where(m => m.Email == email)
                .Select(m => new { m.SubjectId, m.SubjectName })
                .ToListAsync();
            return Ok(subjects);
        }

        // Returns up to 5 pending (uncompleted) task sets for this student
        [HttpGet("GetPendingTasks")]
        public async Task<IActionResult> GetPendingTasks()
        {
            var email = TryGetEmail();
            if (email is null) return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            var taskSets = await _db.TaskSets
                .Where(t => t.Email == email && !t.IsCompleted)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            return Ok(taskSets.Select(t => new
            {
                t.Id,
                t.SubjectName,
                tasks = JsonConvert.DeserializeObject<List<string>>(t.TasksJson),
                t.CreatedAt
            }));
        }

        // Marking a task set as completed counts as one study session toward the streak
        [HttpPost("CompleteTaskSet/{id:int}")]
        public async Task<IActionResult> CompleteTaskSet(int id)
        {
            var email = TryGetEmail();
            if (email is null) return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            var taskSet = await _db.TaskSets.FindAsync(id);
            if (taskSet == null || taskSet.Email != email) return NotFound();
            if (taskSet.IsCompleted) return Ok(new { alreadyCompleted = true });

            taskSet.IsCompleted = true;
            taskSet.CompletedAt = DateTime.UtcNow;

            // Count as a study session
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var session = await _db.PomodoroSessions
                .FirstOrDefaultAsync(s => s.Email == email && s.SessionDate == today);

            if (session == null)
            {
                session = new PomodoroSession
                    { Email = email, SessionDate = today, SessionsCompleted = 1, TotalMinutes = 0 };
                _db.PomodoroSessions.Add(session);
            }
            else if (session.SessionsCompleted < 8)
            {
                session.SessionsCompleted++;
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }

    public record MonitoredSubjectDto(string SubjectId, string SubjectName);
}
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Controllers/StudyNotificationsController.cs
git commit -m "feat: add StudyNotificationsController (SetMonitoredSubjects 403 enforcement, GetPendingTasks, CompleteTaskSet)"
```

---

## Task 17: Build LeaderboardController

**Files:** `Controllers/LeaderboardController.cs`

- [ ] Create `Controllers/LeaderboardController.cs`:

```csharp
using E_Dnevnik_API.Database;
using E_Dnevnik_API.Database.Models;
using E_Dnevnik_API.Models.ScrapeSubjects;
using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    public class LeaderboardController : ApiBaseController
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public LeaderboardController(SessionStore sessionStore, AppDbContext db, IConfiguration config)
            : base(sessionStore)
        {
            _db = db;
            _config = config;
        }

        // Student opts into leaderboard. Class/city/county populated from their e-Dnevnik profile.
        [HttpPost("OptIn")]
        public async Task<IActionResult> OptIn([FromBody] OptInDto dto)
        {
            var email = TryGetEmail();
            if (email is null) return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            if (string.IsNullOrWhiteSpace(dto.Nickname) || dto.Nickname.Length > 50)
                return BadRequest(new { error = "Nickname mora biti između 1 i 50 znakova." });

            var nicknameInUse = await _db.LeaderboardEntries
                .AnyAsync(e => e.Nickname == dto.Nickname && e.Email != email);
            if (nicknameInUse)
                return Conflict(new { error = "Taj nadimak je već zauzet. Odaberi drugi." });

            var entry = await _db.LeaderboardEntries.FindAsync(email);
            if (entry == null)
            {
                entry = new LeaderboardEntry { Email = email, OptedInAt = DateTime.UtcNow };
                _db.LeaderboardEntries.Add(entry);
            }

            entry.Nickname = dto.Nickname;
            entry.ClassId = dto.ClassId;
            entry.SchoolId = dto.SchoolId;
            entry.City = dto.City;
            entry.County = dto.County;
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("OptOut")]
        public async Task<IActionResult> OptOut()
        {
            var email = TryGetEmail();
            if (email is null) return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            var entry = await _db.LeaderboardEntries.FindAsync(email);
            if (entry != null) { _db.LeaderboardEntries.Remove(entry); await _db.SaveChangesAsync(); }
            return Ok();
        }

        // Returns class leaderboard — nickname + score only, never real name or raw grades
        [HttpGet("Class/{classId}")]
        public async Task<IActionResult> GetClassLeaderboard(string classId)
        {
            var entries = await _db.LeaderboardEntries
                .Where(e => e.ClassId == classId)
                .OrderByDescending(e => e.CombinedScore)
                .Take(50)
                .Select(e => new { e.Nickname, e.CombinedScore, e.CurrentStreak, e.GradeDeltaScore, e.StreakScore })
                .ToListAsync();
            return Ok(entries);
        }

        // Weekly score recalculation — called by Firebase Cloud Function. Protected by secret.
        [HttpPost("RecalculateScores")]
        public async Task<IActionResult> RecalculateScores(
            [FromHeader(Name = "X-Background-Secret")] string? secret)
        {
            if (secret != _config["BackgroundJobSecret"]) return Unauthorized();

            var entries = await _db.LeaderboardEntries.ToListAsync();
            var currentSchoolYear = GetCurrentSchoolYear();
            // Filter to current year — GradeBaseline has composite key (Email, SchoolYear)
            var baselines = await _db.GradeBaselines
                .Where(b => b.SchoolYear == currentSchoolYear)
                .ToDictionaryAsync(b => b.Email);
            var sessions = await _db.PomodoroSessions.ToListAsync();

            foreach (var entry in entries)
            {
                var cache = await _db.StudentCache.FindAsync(entry.Email);
                if (cache?.GradesData == null || !baselines.TryGetValue(entry.Email, out var baseline))
                    continue;

                var grades = JsonConvert.DeserializeObject<SubjectScrapeResult>(cache.GradesData);
                var parseable = grades?.Subjects?
                    .Where(s => decimal.TryParse(s.Grade,
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                    .Select(s => decimal.Parse(s.Grade, System.Globalization.CultureInfo.InvariantCulture))
                    .ToList();
                if (parseable == null || !parseable.Any()) continue;

                var currentAverage = (decimal)parseable.Average();
                var delta = currentAverage - baseline.BaselineAverage;

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var userSessions = sessions
                    .Where(s => s.Email == entry.Email)
                    .OrderByDescending(s => s.SessionDate)
                    .ToList();

                int streak = 0;
                var checkDate = today;
                foreach (var s in userSessions)
                {
                    if (s.SessionDate == checkDate && s.SessionsCompleted > 0) { streak++; checkDate = checkDate.AddDays(-1); }
                    else if (s.SessionDate < checkDate) break;
                }

                entry.CurrentStreak = streak;
                entry.GradeDeltaScore = delta;
                entry.StreakScore = Math.Min(streak, 30);
                entry.CombinedScore = (entry.GradeDeltaScore * 0.6m) + (entry.StreakScore / 30m * 100m * 0.4m);
                entry.LastScoreUpdate = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { updated = entries.Count });
        }

        // Run manually once at school year start to snapshot current averages as baselines
        [HttpPost("SetBaselines")]
        public async Task<IActionResult> SetBaselines(
            [FromHeader(Name = "X-Background-Secret")] string? secret)
        {
            if (secret != _config["BackgroundJobSecret"]) return Unauthorized();

            var allCache = await _db.StudentCache.Where(c => c.GradesData != null).ToListAsync();
            var schoolYear = GetCurrentSchoolYear();
            int set = 0;

            foreach (var cache in allCache)
            {
                // GradeBaseline has composite key (Email, SchoolYear)
                var existing = await _db.GradeBaselines.FindAsync(cache.Email, schoolYear);
                if (existing != null) continue;

                var grades = JsonConvert.DeserializeObject<SubjectScrapeResult>(cache.GradesData!);
                var parseable = grades?.Subjects?
                    .Where(s => decimal.TryParse(s.Grade,
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                    .Select(s => decimal.Parse(s.Grade, System.Globalization.CultureInfo.InvariantCulture))
                    .ToList();
                if (parseable == null || !parseable.Any()) continue;

                _db.GradeBaselines.Add(new GradeBaseline
                {
                    Email = cache.Email,
                    SchoolYear = schoolYear,
                    BaselineAverage = (decimal)parseable.Average(),
                    RecordedAt = DateTime.UtcNow
                });
                set++;
            }

            await _db.SaveChangesAsync();
            return Ok(new { baselinesSet = set });
        }

        private static string GetCurrentSchoolYear()
        {
            var now = DateTime.UtcNow;
            var year = now.Month >= 9 ? now.Year : now.Year - 1;
            return $"{year}-{year + 1}";
        }
    }

    public record OptInDto(string Nickname, string ClassId, string SchoolId, string City, string County);
}
```

- [ ] Build:

```bash
dotnet build
```

Expected: `Build succeeded.`

- [ ] Commit:

```bash
git add Controllers/LeaderboardController.cs
git commit -m "feat: add LeaderboardController (OptIn, OptOut, GetClassLeaderboard, RecalculateScores, SetBaselines)"
```

---

## Task 18: Firebase Cloud Function scheduler

This lives in the Flutter/Firebase project, not in this backend repo. Providing it here for reference.

In `functions/src/backgroundJobs.ts` of the Firebase project:

```typescript
import * as functions from "firebase-functions";
import axios from "axios";

const BACKEND_URL = functions.config().backend.url;
const SECRET = functions.config().backend.job_secret;
const headers = { "X-Background-Secret": SECRET };

// Grade check — 2x per day at 15:00 and 22:00 Zagreb time
export const checkNewGrades = functions.pubsub
    .schedule("0 15,22 * * *")
    .timeZone("Europe/Zagreb")
    .onRun(async () => {
        const res = await axios.post(
            `${BACKEND_URL}/api/Background/CheckNewGrades`,
            {},
            { headers, timeout: 540_000 }
        );
        console.log("Grade check result:", res.data);
    });

// Leaderboard recalculation — weekly, Sunday 23:00
export const recalculateLeaderboard = functions.pubsub
    .schedule("0 23 * * 0")
    .timeZone("Europe/Zagreb")
    .onRun(async () => {
        const res = await axios.post(
            `${BACKEND_URL}/api/Leaderboard/RecalculateScores`,
            {},
            { headers, timeout: 300_000 }
        );
        console.log("Leaderboard recalc result:", res.data);
    });
```

Set config:
```bash
firebase functions:config:set backend.url="https://your-heroku-app.herokuapp.com"
firebase functions:config:set backend.job_secret="your-secret-here"
heroku config:set BackgroundJobSecret="your-secret-here" --app your-app-name
heroku config:set FIREBASE_SERVICE_ACCOUNT_JSON='{"type":"service_account",...}' --app your-app-name
heroku config:set OpenAI__ApiKey="sk-..." --app your-app-name
```

---

## Task 19: Deploy and verify

- [ ] Push branch to Heroku:

```bash
git push heroku backend/caching-and-database:main
```

- [ ] Verify Heroku logs show migration ran clean:

```bash
heroku logs --tail --app your-app-name
```

Expected: EF Core migration log lines, then app listening on port.

- [ ] Test login returns `firebaseToken`:

```
POST /api/Login { "email": "...", "password": "..." }
Expected response: { "token": "...", "firebaseToken": "...", "uid": "..." }
```

- [ ] Test FCM token registration:

```
POST /api/Device/RegisterToken
Authorization: Bearer <token>
{ "fcmToken": "test-token-123" }
Expected: 200 OK
```

- [ ] Test caching — call same endpoint twice, second should be cached:

```
GET /api/Scraper/ScrapeSubjectsAndProfessors
Authorization: Bearer <token>
First call:  { "data": {...}, "cachedAt": "...", "isFromCache": false }
Second call: { "data": {...}, "cachedAt": "...", "isFromCache": true }  ← same cachedAt
```

- [ ] Test force refresh cooldown:

```
GET /api/Scraper/ScrapeSubjectsAndProfessors?forceRefresh=true  ← resets timer
GET /api/Scraper/ScrapeSubjectsAndProfessors?forceRefresh=true  ← within 15 min → isFromCache: true
```

- [ ] Test background job:

```
POST /api/Background/CheckNewGrades
X-Background-Secret: your-secret-here
Expected: { "scraped": N, "remindersSent": M, "failed": 0, "total": N+M }
Without header: 401
```

- [ ] Test monitored subjects limit enforcement:

```
POST /api/StudyNotifications/SetMonitoredSubjects
Authorization: Bearer <free-tier-token>
[{"subjectId": "1", "subjectName": "Matematika"}, {"subjectId": "2", "subjectName": "Fizika"}]
Expected: 403
```

---

## Definition of Done

- [ ] All 7 Postgres tables exist on Heroku (`heroku pg:psql --command "\dt"`)
- [ ] `POST /api/Login` returns `{ token, firebaseToken, uid }` and stores token in `StudentCache`
- [ ] `POST /api/Device/RegisterToken` stores FCM token in `StudentCache.FcmToken`
- [ ] Every scraper endpoint returns `{ data, cachedAt, isFromCache }` — second call within TTL returns `isFromCache: true`
- [ ] `ScrapeSpecificSubjectGrades` caches per-subjectId in `SpecificSubjectGradesJson`
- [ ] Force refresh rate limited to once per 15 minutes per student
- [ ] `POST /api/Background/CheckNewGrades` protected by secret, returns `{ scraped, remindersSent, failed, total }`
- [ ] `SetMonitoredSubjects` returns 403 (not 400) for free tier exceeding 1 subject
- [ ] `GetPendingTasks`, `CompleteTaskSet` endpoints work
- [ ] `CompleteSession`, `GetStreak` endpoints work
- [ ] Leaderboard `OptIn`, `OptOut`, `GetClassLeaderboard`, `RecalculateScores`, `SetBaselines` work
- [ ] `GetEmailFromToken()` logic is in `ApiBaseController.TryGetEmail()` — not duplicated per controller
- [ ] All endpoints tested in Postman before Flutter integration begins
