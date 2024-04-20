namespace E_Dnevnik_API.Models.NewGrades
{
    public class NewGrades
    {
        public string? Grade { get; set; }
        public string? Subject { get; set; }
        public string? Description { get; set; }
    }

    public class NewGradesResult
    {
        public List<NewGrades>? Grades { get; set; }
    }
}
