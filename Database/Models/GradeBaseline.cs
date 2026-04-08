using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Database.Models
{
    [PrimaryKey(nameof(Email), nameof(SchoolYear))]
    public class GradeBaseline
    {
        public string Email { get; set; } = string.Empty;
        public string SchoolYear { get; set; } = string.Empty;
        public decimal BaselineAverage { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}
