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

        private const decimal DropThreshold = 0.3m;
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
                        continue;

                    var snapshot = await _db.GradeSnapshots.FindAsync(email, subject.SubjectId);

                    if (snapshot == null)
                    {
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
                body: $"Prosjek ti je pao na {newAverage:F1}. Pripremili smo {tasks.Count} zadataka koji ti mogu pomoći.",
                data: new Dictionary<string, string>
                {
                    ["type"] = "grade_drop",
                    ["subjectName"] = subjectName,
                    ["subjectId"] = subjectId
                });
        }
    }
}
