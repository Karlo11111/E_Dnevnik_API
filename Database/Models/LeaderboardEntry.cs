using System.ComponentModel.DataAnnotations;

namespace E_Dnevnik_API.Database.Models
{
    public class LeaderboardEntry
    {
        [Key]
        public string Email { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Nickname { get; set; } = string.Empty;

        public string ClassId { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;

        public decimal GradeDeltaScore { get; set; } = 0;
        public decimal StreakScore { get; set; } = 0;
        public decimal CombinedScore { get; set; } = 0;
        public int CurrentStreak { get; set; } = 0;

        public string? StudentProgram { get; set; }

        public DateTime OptedInAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastScoreUpdate { get; set; }
    }
}
