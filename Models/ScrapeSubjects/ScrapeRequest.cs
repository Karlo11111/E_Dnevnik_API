namespace E_Dnevnik_API.Models.ScrapeSubjects
{
    // tijelo zahtjeva koji šalješ api-u - samo email i lozinka od e-dnevnika
    public class ScrapeRequest
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
    }
}
