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

        public PomodoroController(SessionStore sessionStore, AppDbContext db)
            : base(sessionStore)
        {
            _db = db;
        }

        // Called when student completes a 25-minute Pomodoro. Flutter enforces the timer.
        [HttpPost("CompleteSession")]
        public async Task<IActionResult> CompleteSession()
        {
            var email = TryGetEmail();
            if (email is null)
                return Unauthorized(
                    "Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava."
                );

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var session = await _db.PomodoroSessions.FirstOrDefaultAsync(s =>
                s.Email == email && s.SessionDate == today
            );

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
            if (email is null)
                return Unauthorized(
                    "Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava."
                );
            return Ok(await CalculateStreak(email));
        }

        private async Task<object> CalculateStreak(string email)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var sessions = await _db
                .PomodoroSessions.Where(s => s.Email == email && s.SessionDate <= today)
                .OrderByDescending(s => s.SessionDate)
                .Take(365)
                .ToListAsync();

            int currentStreak = 0;
            var checkDate = today;
            foreach (var s in sessions)
            {
                if (s.SessionDate == checkDate && s.SessionsCompleted > 0)
                {
                    currentStreak++;
                    checkDate = checkDate.AddDays(-1);
                }
                else if (s.SessionDate < checkDate)
                    break;
            }

            int longestStreak = 0,
                tempStreak = 0;
            DateOnly? prev = null;
            foreach (var s in sessions.OrderBy(s => s.SessionDate))
            {
                if (s.SessionsCompleted == 0)
                    continue;
                if (prev == null || s.SessionDate == prev.Value.AddDays(1))
                {
                    tempStreak++;
                    longestStreak = Math.Max(longestStreak, tempStreak);
                }
                else
                    tempStreak = 1;
                prev = s.SessionDate;
            }

            return new
            {
                currentStreak,
                longestStreak,
                todaySessions = sessions
                    .FirstOrDefault(s => s.SessionDate == today)
                    ?.SessionsCompleted ?? 0,
                todayMinutes = sessions.FirstOrDefault(s => s.SessionDate == today)?.TotalMinutes
                    ?? 0,
            };
        }
    }
}
