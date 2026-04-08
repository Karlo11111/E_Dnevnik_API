using System.Net;
using System.Threading.RateLimiting;
using E_Dnevnik_API.Database;
using E_Dnevnik_API.ScrapingServices;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// sluša na heroku portu ili lokalno na 5168
var port = Environment.GetEnvironmentVariable("PORT") ?? "5168";
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, int.Parse(port));
});

// registracija svih servisa za scraping
builder.Services.AddScoped<ScraperService>();
builder.Services.AddScoped<SpecificSubjectScraperService>();
builder.Services.AddScoped<TestScraperService>();
builder.Services.AddScoped<StudentProfileScraperService>();
builder.Services.AddScoped<DifferentGradeLinkScraperService>();
builder.Services.AddScoped<AbsenceScraperService>();
builder.Services.AddScoped<ScheduleTableScraperService>();
builder.Services.AddScoped<NewGradesScraperService>();
builder.Services.AddScoped<NewTestsScraperService>();

// singleton jer mora živjeti između zahtjeva i čuvati aktivne sesije u memoriji
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<LoginBruteForceProtection>();

// rate limiting po ip adresi - max 20 zahtjeva u minuti
// štiti i naš server i e-dnevnik od preopterećenja
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(
        "perIp",
        context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 20,
                    QueueLimit = 0,
                }
            )
    );
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\": \"previše zahtjeva, pokušaj ponovo za minutu.\"}",
            cancellationToken
        );
    };
});

// hsts - forsira https, bez ovoga browser može slati plain http s lozinkom u bodiju
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365);
        options.IncludeSubDomains = true;
    });
}

builder.Services.AddMemoryCache();

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

// --- Firebase Admin SDK ---
// Heroku filesystem is ephemeral — never use FromFile(). Store JSON content as env var.
var serviceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
if (serviceAccountJson != null)
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.FromJson(serviceAccountJson)
    });
}

// cors: u developmentu dopuštamo sve origine (swagger ui radi iz browsera)
// u produkciji ne dopuštamo nijedan origin - browseri su blokirani, postman nije jer ne šalje Origin header
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
        )
    );
}
else
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins())); // nema dopuštenih origina = browseri dobivaju CORS grešku
}
builder.Services.AddHostedService<NewDataRefreshService>();

builder.Services.AddScoped<E_Dnevnik_API.Services.CacheService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// auto-migrate on startup — simpler than manual heroku run for this setup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// heroku terminira TLS na load balanceru - bez ovoga UseHttpsRedirection bi redirectao na krivi port
app.UseForwardedHeaders(
    new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    }
);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // u produkciji hvata sve unhandled exceptione i vraća generički 500 bez stack tracea
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"interna greška servera.\"}");
        });
    });
    app.UseHsts();
}

// security headers - štite od uobičajenih web napada
app.Use(
    async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
        // csp: api ne servira html pa možemo zabraniti sve osim same-origin json odgovora
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
        await next();
    }
);

app.UseCors();

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers().RequireRateLimiting("perIp");

// health check - heroku i monitoring alati mogu provjeriti je li app živa
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
