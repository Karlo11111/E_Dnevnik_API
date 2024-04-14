using E_Dnevnik_API.Models.ScrapeStudentProfile;
using E_Dnevnik_API.Models.ScrapeSubjects;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.RegularExpressions;

namespace E_Dnevnik_API.ScrapingServices
{
    public class StudentProfileScraperService : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public StudentProfileScraperService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ActionResult<StudentProfileResult>> ScrapeStudentProfile([FromBody] ScrapeRequest request)
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

        private async Task<StudentProfileResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

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
            var studentProfile = new StudentProfileInfo
            {
                StudentName = studentName,
                StudentGrade = studentGrade,
                StudentSchoolYear = studentSchoolYear,
                StudentSchool = studentSchool,
                StudentSchoolCity = studentSchoolCity
            };
            return new StudentProfileResult
            {
                StudentProfile = studentProfile
            };
        }
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
