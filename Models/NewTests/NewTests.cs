namespace E_Dnevnik_API.Models.NewTests
{
    public class NewTests
    {
        public string? Date { get; set; }
        public string? TestSubject { get; set; }
        public string? Description { get; set; }
    }

    public class NewTestsResult
    {
        public List<NewTests>? Tests { get; set; }
    }
}
