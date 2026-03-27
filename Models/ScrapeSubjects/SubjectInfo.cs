namespace E_Dnevnik_API.Models.ScrapeSubjects
{
    // podaci o jednom predmetu - ime, profesor, prosječna ocjena i id za dohvacanje detalja
    public class SubjectInfo
    {
        public SubjectInfo(string subjectName, string professor, string grade, string subjectId)
        {
            SubjectName = subjectName;
            Professor = professor;
            Grade = grade;
            SubjectId = subjectId;
        }

        public string SubjectId { get; set; }
        public string SubjectName { get; set; }
        public string Professor { get; set; }
        public string Grade { get; set; }
    }
}
