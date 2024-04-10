namespace E_Dnevnik_API.Models.ScrapeSubjects
{
    //this class is used to store the information about the subjects
    public class SubjectInfo
    {
        public SubjectInfo(string name, string professor, string grade)
        {
            Name = name;
            Professor = professor;
            Grade = grade;
        }

        public string Name { get; set; }
        public string Professor { get; set; }
        public string Grade { get; set; }
    }
}
