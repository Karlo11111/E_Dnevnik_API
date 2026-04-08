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

        public StudyNotificationsController(SessionStore sessionStore, AppDbContext db)
            : base(sessionStore)
        {
            _db = db;
        }

        // Postgres is source of truth for monitored subjects — Flutter calls this, NOT Firestore.
        // Returns 403 if free tier sends more than 1 subject. Never trust client-side gating alone.
        [HttpPost("SetMonitoredSubjects")]
        public async Task<IActionResult> SetMonitoredSubjects(
            [FromBody] List<MonitoredSubjectDto> subjects
        )
        {
            var email = TryGetEmail();
            if (email is null)
                return Unauthorized(
                    "Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava."
                );

            const int maxFree = 1;
            const int maxPremium = 5;
            var cache = await _db.StudentCache.FindAsync(email);
            var max = (cache?.IsOdlikasPlus == true) ? maxPremium : maxFree;

            if (subjects.Count > max)
                return StatusCode(
                    403,
                    new
                    {
                        error = $"Besplatni plan dozvoljava praćenje {max} predmeta. Nadogradi na Odlikas+ za praćenje do 5 predmeta.",
                    }
                );

            var existing = await _db.MonitoredSubjects.Where(m => m.Email == email).ToListAsync();
            _db.MonitoredSubjects.RemoveRange(existing);

            foreach (var s in subjects.Take(max))
            {
                _db.MonitoredSubjects.Add(
                    new MonitoredSubject
                    {
                        Email = email,
                        SubjectId = s.SubjectId,
                        SubjectName = s.SubjectName,
                    }
                );
            }

            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("GetMonitoredSubjects")]
        public async Task<IActionResult> GetMonitoredSubjects()
        {
            var email = TryGetEmail();
            if (email is null)
                return Unauthorized(
                    "Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava."
                );

            var subjects = await _db
                .MonitoredSubjects.Where(m => m.Email == email)
                .Select(m => new { m.SubjectId, m.SubjectName })
                .ToListAsync();
            return Ok(subjects);
        }

        [HttpGet("GetPendingTasks")]
        public async Task<IActionResult> GetPendingTasks()
        {
            var email = TryGetEmail();
            if (email is null)
                return Unauthorized(
                    "Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava."
                );

            var taskSets = await _db
                .TaskSets.Where(t => t.Email == email && !t.IsCompleted)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            return Ok(
                taskSets.Select(t => new
                {
                    t.Id,
                    t.SubjectName,
                    tasks = JsonConvert.DeserializeObject<List<string>>(t.TasksJson),
                    t.CreatedAt,
                })
            );
        }

        // Completing a task set counts as one study session toward the streak
        [HttpPost("CompleteTaskSet/{id:int}")]
        public async Task<IActionResult> CompleteTaskSet(int id)
        {
            var email = TryGetEmail();
            if (email is null)
                return Unauthorized(
                    "Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava."
                );

            var taskSet = await _db.TaskSets.FindAsync(id);
            if (taskSet == null || taskSet.Email != email)
                return NotFound();
            if (taskSet.IsCompleted)
                return Ok(new { alreadyCompleted = true });

            taskSet.IsCompleted = true;
            taskSet.CompletedAt = DateTime.UtcNow;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var session = await _db.PomodoroSessions.FirstOrDefaultAsync(s =>
                s.Email == email && s.SessionDate == today
            );

            if (session == null)
            {
                session = new PomodoroSession
                {
                    Email = email,
                    SessionDate = today,
                    SessionsCompleted = 1,
                    TotalMinutes = 0,
                };
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
