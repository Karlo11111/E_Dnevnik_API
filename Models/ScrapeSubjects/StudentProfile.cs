using Newtonsoft.Json;

namespace E_Dnevnik_API.Models.ScrapeSubjects
{
    public class StudentProfile
    {
        public string StudentSchool { get; set; }
        public string StudentSchoolCity { get; set; }
        public string StudentSchoolYear { get; set; }
        public string StudentGrade { get; set; }
        public string StudentName { get; set; }
    }
    public class ScrapeResult
    {
        [JsonProperty(Order = 1)]
        public List<SubjectInfo> Subjects { get; set; }

        [JsonProperty(Order = 2)]
        public StudentProfile StudentProfile { get; set; }
    }
}
