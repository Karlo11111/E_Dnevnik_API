using Microsoft.EntityFrameworkCore;

namespace E_Dnevnik_API.Database.Models
{
    [PrimaryKey(nameof(Email), nameof(SubjectId))]
    public class MonitoredSubject
    {
        public string Email { get; set; } = string.Empty;
        public string SubjectId { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
    }
}
