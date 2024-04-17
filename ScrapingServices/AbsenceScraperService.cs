using E_Dnevnik_API.Models.Absences_izostanci;
using E_Dnevnik_API.Models.ScrapeStudentProfile;
using E_Dnevnik_API.Models.ScrapeSubjects;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace E_Dnevnik_API.ScrapingServices
{
    public class AbsenceScraperService : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public AbsenceScraperService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ActionResult<AbsencesResult>> ScrapeAbsences([FromBody] ScrapeRequest request)
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

            var scrapeResponse = await httpClient.GetAsync("https://ocjene.skole.hr/absent");
            if (!scrapeResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)scrapeResponse.StatusCode, "Failed to retrieve subject information.");
            }

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            var scrapeData = await ExtractScrapeData(scrapeHtmlContent);

            return Ok(scrapeData);
        }
        private async Task<AbsencesResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);


            var absenceDateNodes = htmlDoc.DocumentNode.SelectNodes("//div[@aria-label='AbsentTable']");
            var absences = new List<AbsanceRecord>();

            if (absenceDateNodes != null)
            {
                foreach (var dateNode in absenceDateNodes)
                {
                    var date = dateNode.SelectSingleNode(".//div[@class='row header first']//div[@class='cell']")?.InnerText.Trim(); // Modify XPath as needed
                    var subjects = dateNode.SelectNodes(".//div[contains(@class, 'row')]/div[@class='box']/div[@class='cell'][1]/span[1]") // Modify XPath as needed
                        .Select(node => node.InnerText.Trim())
                        .ToList();
                    absences.Add(new AbsanceRecord
                    {
                        Date = date,
                        Subjects = subjects
                    });
                }
            }

            return new AbsencesResult
            {
                Absences = absences.ToArray()
            };
        }

    }

}
