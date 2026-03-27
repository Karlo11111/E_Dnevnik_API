using Microsoft.Extensions.Caching.Memory;

namespace E_Dnevnik_API.ScrapingServices
{
    // osvježava nove ocjene i ispite u pozadini svakih 5 minuta za sve aktivne sesije
    public class NewDataRefreshService : BackgroundService
    {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(10);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NewDataRefreshService> _logger;

        public NewDataRefreshService(
            IServiceScopeFactory scopeFactory,
            ILogger<NewDataRefreshService> logger
        )
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(RefreshInterval, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var sessionStore = scope.ServiceProvider.GetRequiredService<SessionStore>();
                var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
                var newGradesService =
                    scope.ServiceProvider.GetRequiredService<NewGradesScraperService>();
                var newTestsService =
                    scope.ServiceProvider.GetRequiredService<NewTestsScraperService>();

                foreach (var (token, cookies) in sessionStore.GetAllActiveSessions())
                {
                    try
                    {
                        using var handler = new System.Net.Http.HttpClientHandler
                        {
                            UseCookies = true,
                            CookieContainer = cookies,
                        };
                        using var client = new System.Net.Http.HttpClient(handler)
                        {
                            Timeout = TimeSpan.FromSeconds(30),
                        };

                        var grades = await newGradesService.ScrapeNewGrades(client);
                        cache.Set($"newgrades:{token}", grades, CacheExpiry);

                        var tests = await newTestsService.ScrapeNewTests(client);
                        cache.Set($"newtests:{token}", tests, CacheExpiry);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "background refresh nije uspio za jednu sesiju, nastavljam s ostalima"
                        );
                    }
                }
            }
        }
    }
}
