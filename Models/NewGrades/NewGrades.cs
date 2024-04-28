namespace E_Dnevnik_API.Models.NewGrades
{
    public class NewGrades
    {
        public string? Date { get; set; }
        public string? SubjectName { get; set; }
        public string? Description { get; set; }
        public string? GradeNumber { get; set; }
        public string? ElementOfEvaluation { get; set; }
    }

    public class NewGradesResult
    {
        public List<NewGrades>? Grades { get; set; }
    }
}
