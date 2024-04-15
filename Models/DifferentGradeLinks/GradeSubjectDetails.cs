using E_Dnevnik_API.Models.ScrapeSubjects;

namespace E_Dnevnik_API.Models.DifferentGradeLinks
{
    public class GradeSubjectDetails
    {
        public string GradeName { get; set; }
        public List<SubjectInfo> Subjects { get; set; }

        public GradeSubjectDetails(string gradeName, List<SubjectInfo> subjects)
        {
            GradeName = gradeName;
            Subjects = subjects;
        }
    }
}
