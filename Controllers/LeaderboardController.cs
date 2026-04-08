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

        public LeaderboardController(
            SessionStore sessionStore,
            AppDbContext db,
            IConfiguration config
        )
            : base(sessionStore)
        {
            _db = db;
            _config = config;
        }

        [HttpPost("OptIn")]
        public async Task<IActionResult> OptIn([FromBody] OptInDto dto)
        {
            var email = TryGetEmail();
            if (email is null)
                return Unauthorized(
                    "Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava."
                );

            if (string.IsNullOrWhiteSpace(dto.Nickname) || dto.Nickname.Length > 50)
                return BadRequest(new { error = "Nickname mora biti između 1 i 50 znakova." });

            var nicknameInUse = await _db.LeaderboardEntries.AnyAsync(e =>
                e.Nickname == dto.Nickname && e.Email != email
            );
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
            if (email is null)
                return Unauthorized(
                    "Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava."
                );

            var entry = await _db.LeaderboardEntries.FindAsync(email);
            if (entry != null)
            {
                _db.LeaderboardEntries.Remove(entry);
                await _db.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpGet("Class/{classId}")]
        public async Task<IActionResult> GetClassLeaderboard(string classId)
        {
            var entries = await _db
                .LeaderboardEntries.Where(e => e.ClassId == classId)
                .OrderByDescending(e => e.CombinedScore)
                .Take(50)
                .Select(e => new
                {
                    e.Nickname,
                    e.CombinedScore,
                    e.CurrentStreak,
                    e.GradeDeltaScore,
                    e.StreakScore,
                })
                .ToListAsync();
            return Ok(entries);
        }

        [HttpPost("RecalculateScores")]
        public async Task<IActionResult> RecalculateScores(
            [FromHeader(Name = "X-Background-Secret")] string? secret
        )
        {
            if (secret != _config["BackgroundJobSecret"])
                return Unauthorized();

            var entries = await _db.LeaderboardEntries.ToListAsync();
            var currentSchoolYear = GetCurrentSchoolYear();
            // Filter to current year — GradeBaseline has composite key (Email, SchoolYear)
            var baselines = await _db
                .GradeBaselines.Where(b => b.SchoolYear == currentSchoolYear)
                .ToDictionaryAsync(b => b.Email);
            var sessions = await _db.PomodoroSessions.ToListAsync();

            foreach (var entry in entries)
            {
                var cache = await _db.StudentCache.FindAsync(entry.Email);
                if (
                    cache?.GradesData == null
                    || !baselines.TryGetValue(entry.Email, out var baseline)
                )
                    continue;

                var grades = JsonConvert.DeserializeObject<SubjectScrapeResult>(cache.GradesData);
                var parseable = grades
                    ?.Subjects?.Where(s =>
                        decimal.TryParse(
                            s.Grade,
                            System.Globalization.NumberStyles.Number,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out _
                        )
                    )
                    .Select(s =>
                        decimal.Parse(s.Grade, System.Globalization.CultureInfo.InvariantCulture)
                    )
                    .ToList();
                if (parseable == null || !parseable.Any())
                    continue;

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
                    if (s.SessionDate == checkDate && s.SessionsCompleted > 0)
                    {
                        streak++;
                        checkDate = checkDate.AddDays(-1);
                    }
                    else if (s.SessionDate < checkDate)
                        break;
                }

                entry.CurrentStreak = streak;
                entry.GradeDeltaScore = delta;
                entry.StreakScore = Math.Min(streak, 30);
                entry.CombinedScore =
                    (entry.GradeDeltaScore * 0.6m) + (entry.StreakScore / 30m * 100m * 0.4m);
                entry.LastScoreUpdate = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { updated = entries.Count });
        }

        [HttpPost("SetBaselines")]
        public async Task<IActionResult> SetBaselines(
            [FromHeader(Name = "X-Background-Secret")] string? secret
        )
        {
            if (secret != _config["BackgroundJobSecret"])
                return Unauthorized();

            var allCache = await _db.StudentCache.Where(c => c.GradesData != null).ToListAsync();
            var schoolYear = GetCurrentSchoolYear();
            int set = 0;

            foreach (var cache in allCache)
            {
                // GradeBaseline has composite key (Email, SchoolYear) — pass both to FindAsync
                var existing = await _db.GradeBaselines.FindAsync(cache.Email, schoolYear);
                if (existing != null)
                    continue;

                var grades = JsonConvert.DeserializeObject<SubjectScrapeResult>(cache.GradesData!);
                var parseable = grades
                    ?.Subjects?.Where(s =>
                        decimal.TryParse(
                            s.Grade,
                            System.Globalization.NumberStyles.Number,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out _
                        )
                    )
                    .Select(s =>
                        decimal.Parse(s.Grade, System.Globalization.CultureInfo.InvariantCulture)
                    )
                    .ToList();
                if (parseable == null || !parseable.Any())
                    continue;

                _db.GradeBaselines.Add(
                    new GradeBaseline
                    {
                        Email = cache.Email,
                        SchoolYear = schoolYear,
                        BaselineAverage = (decimal)parseable.Average(),
                        RecordedAt = DateTime.UtcNow,
                    }
                );
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

    public record OptInDto(
        string Nickname,
        string ClassId,
        string SchoolId,
        string City,
        string County
    );
}
