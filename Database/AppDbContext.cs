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
