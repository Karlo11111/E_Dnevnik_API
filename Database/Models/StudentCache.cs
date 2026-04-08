using System.ComponentModel.DataAnnotations;

namespace E_Dnevnik_API.Database.Models
{
    public class StudentCache
    {
        [Key]
        public string Email { get; set; } = string.Empty;

        public string? ActiveToken { get; set; }
        public DateTime? TokenStoredAt { get; set; }

        public string? FcmToken { get; set; }
        public DateTime? FcmTokenUpdatedAt { get; set; }

        public bool IsOdlikasPlus { get; set; } = false;
        public DateTime? OdlikasPlusSince { get; set; }

        public string? ProfileData { get; set; }
        public DateTime? ProfileCachedAt { get; set; }

        public string? GradesData { get; set; }
        public DateTime? GradesCachedAt { get; set; }

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
