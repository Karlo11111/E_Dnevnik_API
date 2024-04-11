using E_Dnevnik_API.Models.ScrapeSubjects;
using E_Dnevnik_API.Models.ScrapeTests;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net;

namespace E_Dnevnik_API.ScrapingServices
{
    public class TestScraperService : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public TestScraperService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ActionResult<TestResult>> ScrapeTests([FromBody] ScrapeRequest request)
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

            var scrapeResponse = await httpClient.GetAsync("https://ocjene.skole.hr/exam");
            if (!scrapeResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)scrapeResponse.StatusCode, "Failed to retrieve subject information.");
            }

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            var scrapeData = await ExtractScrapeData(scrapeHtmlContent);



            return Ok(scrapeData);
        }

        //extracting test info from the html content
        private async Task<TestResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent); 
            Console.WriteLine("Html content: " + htmlContent);


            try
            {
                var contentAreaNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='page-wrapper']//div[@class='content-wrapper']//div[@class='content ']//div[@class='table-wrapper show-all']//div[@aria-label='ExamTable']");

                Console.WriteLine("contentAreaNode: " + contentAreaNode.InnerHtml);
                if (contentAreaNode == null)
                {
                    Console.WriteLine("contentAreaNode is null");
                    return null; // Or handle the error as appropriate
                }

                // Rest of your code...
            }
    catch (Exception ex)
    {
                Console.WriteLine($"An error occurred: {ex.Message}");
                // Handle the exception as appropriate
            }


            var svibanjTableNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='flex-table small exam-table show' and @aria-label='ExamTable']");
            var testList = new List<TestInfo>();
            var scrapeData = new TestResult
            {
                Tests = testList
            };

            return scrapeData;
        }
    }
}
