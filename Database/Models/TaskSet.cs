using System.ComponentModel.DataAnnotations;

namespace E_Dnevnik_API.Database.Models
{
    public class TaskSet
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string SubjectId { get; set; } = string.Empty;
        public string TasksJson { get; set; } = "[]";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public bool IsCompleted { get; set; } = false;
    }
}
