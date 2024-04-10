namespace E_Dnevnik_API.Models.ScrapeSubjects
{
    //this class is used to store the information about the subjects
    public class SubjectInfo
    {
        public SubjectInfo(string subjectName, string professor, string grade, string studentName, string studentGrade, string studentSchoolYear, string studentSchool, string studentSchoolCity)
        {
            SubjectName = subjectName;
            Professor = professor;
            Grade = grade;
            StudentName = studentName;
            StudentGrade = studentGrade;
            StudentSchoolYear = studentSchoolYear;
            StudentSchool = studentSchool;
            StudentSchoolCity = studentSchoolCity;
        }
        public string StudentSchool { get; set; }
        public string StudentSchoolCity { get; set; }
        public string StudentSchoolYear { get; set; }
        public string StudentGrade { get; set; }

        public string StudentName { get; set; }
        public string SubjectName { get; set; }
        public string Professor { get; set; }
        public string Grade { get; set; }
    }
}
