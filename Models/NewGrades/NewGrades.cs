namespace E_Dnevnik_API.Models.NewGrades
{
    public class NewGrades
    {
        public DateTime Date { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public string Grade { get; set; }
    }

    public class NewGradesResult
    {
        public List<NewGrades>? Grades { get; set; }
    }
}
