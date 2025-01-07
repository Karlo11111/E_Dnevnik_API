using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using E_Dnevnik_API.Models.ScrapeStudentProfile;
using E_Dnevnik_API.Models.ScrapeSubjects;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace E_Dnevnik_API.ScrapingServices
{
    // This class is responsible for scraping the subjects and professors from the website
    public class ScraperService : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ScraperService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ActionResult<SubjectScrapeResult>> ScrapeSubjects(
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

            var scrapeResponse = await httpClient.GetAsync("https://ocjene.skole.hr/course");
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

        //extracting subject info from the html content
        private async Task<SubjectScrapeResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var courseNodes = htmlDoc.DocumentNode.SelectNodes("//ul[@class='list']/li/a");
            var subjectList = new List<SubjectInfo>();

            //getting the each nodes of the course class
            if (courseNodes != null)
            {
                foreach (var aNode in courseNodes)
                {
                    var subjectNameNode = aNode.SelectSingleNode(
                        ".//div[@class='course-info']/span[1]"
                    );
                    var professorNameNode = aNode.SelectSingleNode(
                        ".//div[@class='course-info']/span[2]"
                    );
                    var gradeNode = aNode.SelectSingleNode(
                        ".//div[@class='list-average-grade ']/span"
                    );

                    //subject id
                    string hrefValue = ExtractNumbers(
                        aNode.GetAttributeValue("href", string.Empty)
                    );

                    var subjectName =
                        subjectNameNode != null ? CleanText(subjectNameNode.InnerText) : "N/A";
                    var professorName =
                        professorNameNode != null ? CleanText(professorNameNode.InnerText) : "N/A";
                    var gradeText = gradeNode != null ? CleanText(gradeNode.InnerText) : "N/A";

                    subjectList.Add(
                        new SubjectInfo(subjectName, professorName, gradeText, hrefValue)
                    );
                }
            }

            return new SubjectScrapeResult
            {
                Subjects = subjectList, // The extracted list of subjects
            };
        }

        //function that returns only numbers from the string (for subject id)
        public static string ExtractNumbers(string input)
        {
            // This match any digits in the input string
            Regex regex = new Regex(@"\d+");
            Match match = regex.Match(input);

            // Concatenate all matches into one string
            string number = "";
            while (match.Success)
            {
                number += match.Value;
                match = match.NextMatch();
            }

            return number;
        }

        // Cleans the text by removing leading and trailing whitespace and replacing sequences of whitespace characters with a single space
        private string CleanText(string text)
        {
            // Removes leading and trailing whitespace
            text = text.Trim();

            // Replaces sequences of whitespace characters with a single space
            text = Regex.Replace(text, @"\s+", " ");

            return text;
        }
    }
}
