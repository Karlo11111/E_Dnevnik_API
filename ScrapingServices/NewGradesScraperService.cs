using System.Net;
using E_Dnevnik_API.Models.Absences_izostanci;
using E_Dnevnik_API.Models.NewGrades;
using E_Dnevnik_API.Models.ScrapeSubjects;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;

namespace E_Dnevnik_API.ScrapingServices
{
    public class NewGradesScraperService : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public NewGradesScraperService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ActionResult<NewGradesResult>> ScrapeNewGrades(
            [FromBody] ScrapeRequest request
        )
        {
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
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
            var csrfToken = htmlDoc
                .DocumentNode.SelectSingleNode("//input[@name='csrf_token']")
                ?.Attributes["value"]
                ?.Value;

            if (string.IsNullOrEmpty(csrfToken))
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "CSRF token not found."
                );
            }

            var formData = new Dictionary<string, string>
            {
                ["username"] = request.Email,
                ["password"] = request.Password,
                ["csrf_token"] = csrfToken,
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
                return StatusCode(
                    (int)scrapeResponse.StatusCode,
                    "Failed to retrieve subject information."
                );
            }

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            var scrapeData = await ExtractScrapeData(scrapeHtmlContent);

            return Ok(scrapeData);
        }

        //method that extracts the data from the HTML content once I get access to a new grade link that actually has a new grade
        private async Task<NewGradesResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var newGradeNodes = htmlDoc.DocumentNode.SelectNodes(
                "//div[@id='flex-table new-grades-table']"
            );
            var grades = new List<NewGrades>();

            if (newGradeNodes != null)
            {
                foreach (var gradeNode in newGradeNodes)
                {
                    // Extract grade details (date, note, element of grading, and grade)
                    var subjectName = gradeNode
                        .SelectSingleNode(".//div[@class='row header first']//div[@class='cell']")
                        ?.InnerText;

                    var dateOfGrade = gradeNode
                        .SelectSingleNode(".//div[@class='row ']//div[@class='cell']/span")
                        ?.InnerText;

                    var description = gradeNode
                        .SelectSingleNode(
                            ".//div[@class='row ']//div[@class='box']//div[@class='cell ']/span"
                        )
                        ?.InnerText;

                    var grade = gradeNode
                        .SelectSingleNode(
                            ".//div[@class='row ']//div[@class='box']//div[@class='cell'][2]"
                        )
                        ?.InnerText;

                    var elementOfEvaluation = gradeNode
                        .SelectSingleNode(
                            ".//div[@class='row ']//div[@class='box']//div[@class='cell'][1]"
                        )
                        ?.InnerText;

                    grades.Add(
                        new NewGrades
                        {
                            Date = dateOfGrade,
                            Description = description,
                            SubjectName = subjectName,
                            GradeNumber = grade,
                            ElementOfEvaluation = elementOfEvaluation,
                        }
                    );
                }
            }
            else
            {
                // No new grades found
                Console.WriteLine("No new grades found.");
            }

            return new NewGradesResult { Grades = grades.Count > 0 ? grades : null };
        }
    }
}
