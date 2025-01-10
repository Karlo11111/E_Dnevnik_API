namespace E_Dnevnik_API.Models.SpecificSubject
{
    public class SubjectDetails
    {
        public List<EvaluationElement> EvaluationElements { get; set; }
        public List<MonthlyGrades> MonthlyGrades { get; set; }
    }

    public class EvaluationElement
    {
        public string Name { get; set; }
        public List<string> GradesByMonth { get; set; }
    }

    public class MonthlyGrades
    {
        public string Month { get; set; }
        public List<SpecificSubject> Grades { get; set; }

        public MonthlyGrades(string month)
        {
            Month = month;
            Grades = new List<SpecificSubject>();
        }
    }

    public class SpecificSubject
    {
        public string GradeDate { get; set; }
        public string GradeNote { get; set; }
        public string Grade { get; set; }

        public SpecificSubject(string gradeDate, string gradeNote, string grade)
        {
            GradeDate = gradeDate;
            GradeNote = gradeNote;
            Grade = grade;
        }
    }
}
