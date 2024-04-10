using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net;
using E_Dnevnik_API.Models.ScrapeSubjects;

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

        public async Task<ActionResult<ScrapeResult>> ScrapeSubjects([FromBody] ScrapeRequest request)
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

            var scrapeResponse = await httpClient.GetAsync("https://ocjene.skole.hr/course");
            if (!scrapeResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)scrapeResponse.StatusCode, "Failed to retrieve subject information.");
            }

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            var scrapeData = await ExtractScrapeData(scrapeHtmlContent);



            return Ok(scrapeData);
        }

        //extracting subject info from the html content
        private async Task<ScrapeResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var courseNodes = htmlDoc.DocumentNode.SelectNodes("//ul[@class='list']/li/a");
            var subjectList = new List<SubjectInfo>();

            // Extract the student's name
            var studentNameNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='page-wrapper']//div[@class='logged-in-user']//div[@class='user-name']/span");
            var studentName = studentNameNode != null ? CleanText(studentNameNode.InnerText) : "N/A";

            //Extract the student school, year, and city
            var studentGradeNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='page-wrapper']//div[@class='school-data']//div[@class='class']//span[@class='bold']");
            var studentGrade = studentGradeNode != null ? CleanText(studentGradeNode.InnerText) : "N/A";

            var studentSchoolYearNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='page-wrapper']//div[@class='school-data']//div[@class='class']//span[@class='class-schoolyear']");
            var studentSchoolYear = studentSchoolYearNode != null ? CleanText(studentSchoolYearNode.InnerText) : "N/A";

            var studentSchoolNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='page-wrapper']//div[@class='school-data']//div[@class='school']//span[@class='school-name']");
            var studentSchool = studentSchoolNode != null ? CleanText(studentSchoolNode.InnerText) : "N/A";

            var studentSchoolCityNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='page-wrapper']//div[@class='school-data']//div[@class='school']//span[@class='school-city']");
            var studentSchoolCity = studentSchoolCityNode != null ? CleanText(studentSchoolCityNode.InnerText) : "N/A";

            // Initialize student profile
            var studentProfile = new StudentProfile
            {
                StudentName = studentName,
                StudentGrade = studentGrade,
                StudentSchoolYear = studentSchoolYear,
                StudentSchool = studentSchool,
                StudentSchoolCity = studentSchoolCity
            };

            //getting the each nodes of the course class
            if (courseNodes != null)
            {
                foreach (var aNode in courseNodes)
                {
                    var subjectNameNode = aNode.SelectSingleNode(".//div[@class='course-info']/span[1]");
                    var professorNameNode = aNode.SelectSingleNode(".//div[@class='course-info']/span[2]");
                    var gradeNode = aNode.SelectSingleNode(".//div[@class='list-average-grade ']/span");

                    var subjectName = subjectNameNode != null ? CleanText(subjectNameNode.InnerText) : "N/A";
                    var professorName = professorNameNode != null ? CleanText(professorNameNode.InnerText) : "N/A";
                    var gradeText = gradeNode != null ? CleanText(gradeNode.InnerText) : "N/A";

                    subjectList.Add(new SubjectInfo(subjectName, professorName, gradeText));
                }
            }

            return new ScrapeResult
            {
                Subjects = subjectList, // The extracted list of subjects
                StudentProfile = studentProfile // The extracted student profile
            };
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
