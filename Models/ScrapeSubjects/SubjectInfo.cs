namespace E_Dnevnik_API.Models.ScrapeSubjects
{
    //this class is used to store the information about the subjects
    public class SubjectInfo
    {
        public SubjectInfo(string subjectName, string professor, string grade)
        {
            SubjectName = subjectName;
            Professor = professor;
            Grade = grade;
        }
        public string SubjectName { get; set; }
        public string Professor { get; set; }
        public string Grade { get; set; }
    }
}
