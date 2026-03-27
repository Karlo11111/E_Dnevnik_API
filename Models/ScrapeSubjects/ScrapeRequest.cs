using System.ComponentModel.DataAnnotations;

namespace E_Dnevnik_API.Models.ScrapeSubjects
{
    // tijelo zahtjeva koji šalješ api-u - samo email i lozinka od e-dnevnika
    public class ScrapeRequest
    {
        // MaxLength sprječava slanje ogromnih payloada koji bi usporili ili srušili server
        [Required]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Required]
        [MaxLength(200)]
        public string? Password { get; set; }
    }
}
