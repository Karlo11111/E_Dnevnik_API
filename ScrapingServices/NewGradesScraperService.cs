using E_Dnevnik_API.Models.Absences_izostanci;
using E_Dnevnik_API.Models.NewGrades;
using E_Dnevnik_API.Models.ScrapeSubjects;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace E_Dnevnik_API.ScrapingServices
{
    public class NewGradesScraperService : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public NewGradesScraperService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ActionResult<NewGradesResult>> ScrapeAbsences([FromBody] ScrapeRequest request)
        {
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };

            using var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.Clear();
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Email and password must be provided.");
            }

            var loginPageResponse = await httpClient.GetAsync("https://ocjene.skole.hr/login");
            var loginPageContent = await loginPageResponse.Content.ReadAsStringAsync();
            var loginUrl = "https://ocjene.skole.hr/login";

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(loginPageContent);
            var csrfToken = htmlDoc.DocumentNode.SelectSingleNode("//input[@name='csrf_token']")
                            ?.Attributes["value"]?.Value;

            if (string.IsNullOrEmpty(csrfToken))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "CSRF token not found.");
            }

            var formData = new Dictionary<string, string>
            {
                ["username"] = request.Email,
                ["password"] = request.Password,
                ["csrf_token"] = csrfToken
            };

            var loginContent = new FormUrlEncodedContent(formData);
            httpClient.DefaultRequestHeaders.Referrer = new Uri("https://ocjene.skole.hr/login");
            var loginResponse = await httpClient.PostAsync(loginUrl, loginContent);

            if (!loginResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)loginResponse.StatusCode, "Failed to log in.");
            }

            var scrapeResponse = await httpClient.GetAsync("https://ocjene.skole.hr/grade/new");
            if (!scrapeResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)scrapeResponse.StatusCode, "Failed to retrieve subject information.");
            }

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            var scrapeData = await ExtractScrapeData(scrapeHtmlContent);

            return Ok(scrapeData);
        }

        //TODO: Implement the method that extracts the data from the HTML content once I get access to a new grade link that actually has a new grade
        private async Task<NewGradesResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var date = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='section-text no-title']").InnerText;

            var grades = new List<NewGrades>();
            return new NewGradesResult
            {
                Grades = grades
            };
        }

    }
}
