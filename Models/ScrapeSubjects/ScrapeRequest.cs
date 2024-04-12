namespace E_Dnevnik_API.Models.ScrapeSubjects
{
    // this is what is required in the request, basically the body of the request youre sending to the api
    public class ScrapeRequest
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

}
