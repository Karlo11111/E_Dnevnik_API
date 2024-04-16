using E_Dnevnik_API.Models.DifferentGradeLinks;
using E_Dnevnik_API.Models.ScrapeSubjects;
using E_Dnevnik_API.Models.ScrapeTests;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Mvc;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Net;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace E_Dnevnik_API.ScrapingServices
{
    public class DifferentGradeLinkScraperService : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        public DifferentGradeLinkScraperService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }
        public async Task<ActionResult<List<GradeSubjectDetails>>> ScrapeDifferentGradeLink([FromBody] ScrapeRequest request)
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

            var gradeLinks = await FetchGradeLinks(httpClient);
            List<GradeSubjectDetails> allGradeSubjectDetails = new List<GradeSubjectDetails>();

            foreach (var grade in gradeLinks.DifferentGrade)
            {
                var classPageUrl = $"https://ocjene.skole.hr{grade.GradeLink}";

                // Navigate to the class page
                var classPageResponse = await httpClient.GetAsync(classPageUrl);
                if (!classPageResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed to navigate to class page: " + classPageUrl);
                    continue;
                }

                var classPageContent = await classPageResponse.Content.ReadAsStringAsync();

                var subjectDetails = await ExtractSubjectDetails(classPageContent, httpClient);
                allGradeSubjectDetails.Add(new GradeSubjectDetails(grade.GradeYear, subjectDetails.Subjects));
            }

                return Ok(allGradeSubjectDetails);
        }

        private async Task<DifferentGradeResult> ExtractGradeLinks(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var subjectList = new DifferentGradeResult();

            var differentGrade = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'class-menu-vertical past-schoolyear') or contains(@class, 'class-menu-vertical')]");
            try
            {
                if (differentGrade != null)
                {
                    foreach (var grade in differentGrade)
                    {
                        var gradeLink = grade.SelectSingleNode(".//div[@class='class-info']/a")?.Attributes["href"]?.Value;
                        var gradeName = grade.SelectSingleNode(".//div[@class='class-info']/a[@class='school-data']/div[@class='class']/span[@class='bold']")?.InnerText;
                        if (gradeLink != null && gradeName != null)
                        {
                            
                            subjectList.DifferentGrade.Add(new DifferentGrade
                            {
                                GradeYear = gradeName,
                                GradeLink = gradeLink
                            });
                            Console.WriteLine("Grade year: " + gradeName + " Grade link: " + gradeLink);
                        }
                    }
                }
                

            } catch (Exception e)
            {
                Console.WriteLine("No different grade found, error is: " + e.Message);
            }
            

            return subjectList;
        }

        private async Task<SubjectScrapeResult> ExtractSubjectDetails(string htmlContent, HttpClient httpClient)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var subjectList = new List<SubjectInfo>();
            var courseNodes = htmlDoc.DocumentNode.SelectNodes("//ul[@class='list']/li/a");

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
                    var hrefValue = ExtractNumbers(aNode.GetAttributeValue("href", ""));

                    var gradePageUrl = $"https://ocjene.skole.hr/grade/{hrefValue}";
                    
                    if (gradeText == "N/A")
                    {
                        var classPageContent = await httpClient.GetStringAsync(gradePageUrl);
                        var classDoc = new HtmlDocument();
                        classDoc.LoadHtml(classPageContent);
                        // You need to define the correct XPath for the alternative grade location
                        var alternativeGradeNode = classDoc.DocumentNode.SelectSingleNode("//div[@class='flex-table s  grades-table ']/div[@class='row final-grade ']/div[@class='cell']/span");
                        gradeText = alternativeGradeNode != null ? ExtractNumbers(alternativeGradeNode.InnerText) : "N/A";
                    }

                    subjectList.Add(new SubjectInfo(
                        subjectName,
                        professorName,
                        gradeText,
                        hrefValue
                    ));
                }
            }

            return new SubjectScrapeResult { Subjects = subjectList };
        }

        private async Task<DifferentGradeResult> FetchGradeLinks(HttpClient httpClient)
        {
            var response = await httpClient.GetAsync("https://ocjene.skole.hr/class");
            var htmlContent = await response.Content.ReadAsStringAsync();
            return await ExtractGradeLinks(htmlContent); 
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
