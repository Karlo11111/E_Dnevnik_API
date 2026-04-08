using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Database.Models
{
    [Index(nameof(Email), nameof(SessionDate), IsUnique = true)]
    public class PomodoroSession
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateOnly SessionDate { get; set; }
        public int SessionsCompleted { get; set; } = 0;
        public int TotalMinutes { get; set; } = 0;
    }
}
