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
        private async Task<Dictionary<string, List<TestInfo>>> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            // Dictionary to hold each month's tests
            var monthlyTests = new Dictionary<string, List<TestInfo>>();

            // Select all the exam table divs
            var examTableNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'exam-table')]");

            foreach (var tableNode in examTableNodes)
            {
                // Extract the month name using the action-id attribute
                var monthName = tableNode.GetAttributeValue("data-action-id", string.Empty);
                if (string.IsNullOrWhiteSpace(monthName))
                {
                    continue; // Skip if the month name is not found
                }

                // Initialize the list of tests for the month
                var testsForMonth = new List<TestInfo>();

                // Select all the test rows for the month
                var testNodes = tableNode.SelectNodes(".//div[contains(@class, 'row') and not(contains(@class, 'row header'))]");

                if (testNodes != null)
                {
                    foreach (var rowNode in testNodes)
                    {
                        var dateCell = rowNode.SelectSingleNode(".//div[@class='cell'][1]");
                        var nameCell = rowNode.SelectSingleNode(".//div[@class='box']/div[@class='cell'][1]");
                        var descriptionCell = rowNode.SelectSingleNode(".//div[@class='box']/div[@class='cell'][2]");

                        // Ensure none of the critical cells are null before proceeding
                        if (dateCell != null && descriptionCell != null && nameCell != null)
                        {
                            var testDate = dateCell.InnerText.Trim();
                            var testDescription = descriptionCell.InnerText.Trim();
                            var testName = nameCell.InnerText.Trim();

                            testsForMonth.Add(new TestInfo(testName, testDescription, testDate));
                        }
                        else
                        {
                            // Log the issue and continue with the next iteration
                            Console.WriteLine("A required cell is missing in the test node.");
                            continue;
                        }
                    }
                }

                // Add the tests for the month to the dictionary
                monthlyTests[monthName] = testsForMonth;
            }

            return monthlyTests;
        }

    }
}
