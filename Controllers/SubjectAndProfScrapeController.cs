using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScraperController : ControllerBase
    {
        
        private readonly IHttpClientFactory _httpClientFactory;


        public ScraperController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }



        [HttpPost("ScrapeSubjectsAndProfessors")]
        public async Task<ActionResult<List<SubjectInfo>>> ScrapeSubjects([FromBody] ScrapeRequest request)
        {
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };

            // create a HttpClient instance using the handler
            using var httpClient = new HttpClient(handler);

            // Ensure the default headers are cleared
            httpClient.DefaultRequestHeaders.Clear();


            // Input validation
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Email and password must be provided.");
            }

            // Authentication process with the website
            var loginData = new { email = request.Email, password = request.Password };


            var loginPageResponse = await httpClient.GetAsync("https://ocjene.skole.hr/login");
            var loginPageContent = await loginPageResponse.Content.ReadAsStringAsync();

            // find the CSRF token
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(loginPageContent);
            var csrfToken = htmlDoc.DocumentNode.SelectSingleNode("//input[@name='csrf_token']")
                            ?.Attributes["value"]?.Value;

            if (string.IsNullOrEmpty(csrfToken))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "CSRF token not found.");
            }

            // Construct the login request with the CSRF token
            var formData = new Dictionary<string, string>
            {
                ["username"] = request.Email, 
                ["password"] = request.Password,
                ["csrf_token"] = csrfToken
            };

            var loginContent = new FormUrlEncodedContent(formData);

            // Include additional headers if necessary (e.g., Referer)
            httpClient.DefaultRequestHeaders.Referrer = new Uri("https://ocjene.skole.hr/login");

            var loginResponse = await httpClient.PostAsync("https://ocjene.skole.hr/login", loginContent);


            // Continue with scraping process
            var scrapeResponse = await httpClient.GetAsync("https://ocjene.skole.hr/course"); 
            if (!scrapeResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)scrapeResponse.StatusCode, "Failed to retrieve subject information.");
            }

            var htmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            return await ExtractSubjectInfo(htmlContent, httpClient);

        }

        private async Task<List<SubjectInfo>> ExtractSubjectInfo(string htmlContent, HttpClient httpClient)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var courseNodes = htmlDoc.DocumentNode.SelectNodes("//ul[@class='list']/li/a");

            var subjectList = new List<SubjectInfo>();

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

            return subjectList;
        }


        // method to clean up text and empty spaces
        private string CleanText(string text)
        {
            // Removes leading and trailing whitespace
            text = text.Trim();

            // Replaces sequences of whitespace characters with a single space
            text = Regex.Replace(text, @"\s+", " ");

            return text;
        }
    }

    public class ScrapeRequest
    {
        public string? Email { get; set; }
        public string? Password { get; set; }

        public string? Grade { get; set; }

    }

    public class SubjectInfo
    {
        public SubjectInfo(string name, string professor, string grade)
        {
            Name = name;
            Professor = professor;
            Grade = grade;
        }
        public string Name { get; set; }
        public string Professor { get; set; }

        public string Grade { get; set; }
    }

    
}