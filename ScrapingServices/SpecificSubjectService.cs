using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using E_Dnevnik_API.Models.ScrapeStudentProfile;
using E_Dnevnik_API.Models.ScrapeSubjects;
using E_Dnevnik_API.Models.SpecificSubject;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;

namespace E_Dnevnik_API.ScrapingServices
{
    public class SpecificSubjectScraperService : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public SpecificSubjectScraperService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ActionResult<SubjectDetails>> ScrapeSubjects(
            [FromBody] ScrapeRequest request,
            string subjectId
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

            // Access the specific subject's grade page
            var scrapeResponse = await httpClient.GetAsync(
                $"https://ocjene.skole.hr/grade/{subjectId}"
            );
            if (!scrapeResponse.IsSuccessStatusCode)
            {
                return StatusCode(
                    (int)scrapeResponse.StatusCode,
                    "Failed to retrieve subject information."
                );
            }

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();

            // Extract data for both tables
            var subjectDetails = ExtractSubjectDetails(scrapeHtmlContent);

            return Ok(subjectDetails);
        }

        private SubjectDetails ExtractSubjectDetails(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            // Extract the "Elementi vrednovanja" table
            var evaluationElements = ExtractEvaluationElements(htmlDoc);

            // Extract the "Bilješke" table
            var monthlyGrades = ExtractGradesByMonth(htmlDoc);

            return new SubjectDetails
            {
                EvaluationElements = evaluationElements,
                MonthlyGrades = monthlyGrades,
            };
        }

        private List<EvaluationElement> ExtractEvaluationElements(HtmlDocument htmlDoc)
        {
            var rows = htmlDoc.DocumentNode.SelectNodes(
                "//div[@class='flex-table s  grades-table ']//div[@class='row']"
            );
            var evaluationElements = new List<EvaluationElement>();

            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var nameNode = row.SelectSingleNode(".//div[@class='cell first']");
                    if (nameNode == null || nameNode.InnerText.Contains("ZAKLJUČENO"))
                        continue;

                    var grades = row.SelectNodes(".//div[@class='cell grade']");
                    var monthlyGrades = grades
                        ?.Select(gradeNode => gradeNode.InnerText.Trim())
                        .ToList();

                    evaluationElements.Add(
                        new EvaluationElement
                        {
                            Name = nameNode.InnerText.Trim(),
                            GradesByMonth = CleanText(monthlyGrades) ?? new List<string>(),
                        }
                    );
                }
            }

            return evaluationElements;
        }

        private List<MonthlyGrades> ExtractGradesByMonth(HtmlDocument htmlDoc)
        {
            var rows = htmlDoc.DocumentNode.SelectNodes("//div[@class='row  ']");
            var monthlyGrades = new Dictionary<string, MonthlyGrades>();

            if (rows == null)
            {
                return new List<MonthlyGrades>(); // Return an empty list if no grades are found
            }

            foreach (var row in rows)
            {
                var dateNode = row.SelectSingleNode(".//div[@data-date]");
                var gradeNode = row.SelectSingleNode(".//div[@class='box']/div[1]/span");
                var noteNode = row.SelectSingleNode(".//div[@class='box']/div[2]/span");

                if (dateNode == null || noteNode == null)
                    continue;

                var dateText = dateNode.InnerText.Trim();
                var gradeText = gradeNode?.InnerText.Trim() ?? "N/A";
                var noteText = noteNode.InnerText.Trim();

                // Extract the month from the date (e.g., "10.12." -> "12")
                var month = dateText.Split('.')[1];

                if (!monthlyGrades.ContainsKey(month))
                {
                    monthlyGrades[month] = new MonthlyGrades(month);
                }

                var specificSubject = new SpecificSubject(dateText, noteText, gradeText);
                monthlyGrades[month].Grades.Add(specificSubject);
            }

            return monthlyGrades.Values.ToList();
        }

        private List<string> CleanText(List<string> texts)
        {
            return texts
                .Select(text =>
                {
                    if (string.IsNullOrEmpty(text))
                        return string.Empty;

                    // Removes leading and trailing whitespace
                    text = text.Trim();

                    // Allow Croatian letters (č, ć, š, đ, ž) along with basic Latin characters and digits
                    text = Regex.Replace(text, @"[^a-zA-Z0-9čćšđžČĆŠĐŽ\s/]", "");

                    // Replaces sequences of whitespace characters with a single space
                    return Regex.Replace(text, "\\s+", " ");
                })
                .ToList();
        }
    }
}
