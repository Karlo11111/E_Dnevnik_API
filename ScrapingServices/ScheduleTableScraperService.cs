using E_Dnevnik_API.Models.Absences_izostanci;
using E_Dnevnik_API.Models.ScheduleTable;
using E_Dnevnik_API.Models.ScrapeStudentProfile;
using E_Dnevnik_API.Models.ScrapeSubjects;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace E_Dnevnik_API.ScrapingServices
{
    public class ScheduleTableScraperService : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public  ScheduleTableScraperService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ActionResult<ScheduleResult>> ScrapeScheduleTable([FromBody] ScrapeRequest request)
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

            var scrapeResponse = await httpClient.GetAsync("https://ocjene.skole.hr/schedule");
            if (!scrapeResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)scrapeResponse.StatusCode, "Failed to retrieve subject information.");
            }

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            var scrapeData = await ExtractScrapeData(scrapeHtmlContent);



            return Ok(scrapeData);
        }
        private async Task<ScheduleResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var scheduleResult = new ScheduleResult()
            {
                Schedule = new List<ScheduleTable>()
            };

            var daysOfWeek = new string[] { "PON", "UTO", "SRI", "ČET", "PET", "SUB" };

            foreach (var day in daysOfWeek)
            {
                var daySchedule = new ScheduleTable()
                {
                    Day = day,
                    Subjects = new List<string>()
                };

                // Find all schedule tables, both shown and hidden, for the current day
                var scheduleTablesXPath = $"//div[contains(@class, 'flex-table-schedule') and @data-action-id=\"{day}\"]";
                var scheduleTables = htmlDoc.DocumentNode.SelectNodes(scheduleTablesXPath);

                if (scheduleTables != null)
                {
                    foreach (var table in scheduleTables)
                    {
                        // This will get all the rows for each schedule (visible and hidden)
                        var periodRows = table.SelectNodes(".//div[contains(@class, 'row') and not(contains(@class, 'header'))]");
                        foreach (var row in periodRows)
                        {
                            var subjectCell = row.SelectSingleNode(".//div[contains(@class, 'cell') and not(contains(@class, 'no-box'))]");
                            if (subjectCell != null)
                            {
                                var subject = WebUtility.HtmlDecode(subjectCell.InnerText.Trim());
                                if (!string.IsNullOrWhiteSpace(subject))  // Only add non-empty subjects
                                {
                                    daySchedule.Subjects.Add(CleanText(subject));
                                }
                            }
                        }
                    }
                }


                // Split subjects into morning and afternoon if there are more subjects than morning periods
                const int morningPeriods = 7; // Number of periods in the morning
                if (daySchedule.Subjects.Count > morningPeriods)
                {
                    var morningSubjects = daySchedule.Subjects.Take(morningPeriods).ToList();
                    var afternoonSubjects = daySchedule.Subjects.Skip(morningPeriods).ToList();

                    // Create morning and afternoon schedules
                    var morningSchedule = new ScheduleTable
                    {
                        Day = day + " Morning",
                        Subjects = morningSubjects
                    };
                    var afternoonSchedule = new ScheduleTable
                    {
                        Day = day + " Afternoon",
                        Subjects = afternoonSubjects
                    };

                    // Add both to the schedule result
                    scheduleResult.Schedule.Add(morningSchedule);
                    scheduleResult.Schedule.Add(afternoonSchedule);
                }
                else
                {
                    // If there are only morning periods, add the day schedule as is
                    scheduleResult.Schedule.Add(daySchedule);
                }
            }
            var yearlyHours = CalculateYearlySubjectHours(scheduleResult);
            Console.WriteLine(FormatYearlySubjectHours(yearlyHours));
            return scheduleResult;
        }
        private string CleanText(string text)
        {
            // Removes leading and trailing whitespace
            text = text.Trim();

            text = Regex.Replace(text, "[^a-zA-Z0-9\\s/,čćđšžČĆĐŠŽ]", "");

            // Replaces sequences of whitespace characters with a single space
            text = Regex.Replace(text, "\\s+", " ");


            return text;
        }

        
        public Dictionary<string, int> CalculateYearlySubjectHours(ScheduleResult schedule)
        {
            Dictionary<string, int> subjectCounts = new Dictionary<string, int>();

            // Loop through each day's schedule
            foreach (var day in schedule.Schedule)
            {
                foreach (var subject in day.Subjects)
                {
                    // Clean up the subject name to remove any extraneous characters
                    string cleanSubject = CleanText(subject);  

                    // Handle subjects listed together like "Kemija s vježbama, Fizika"
                    var subjectsSplit = cleanSubject.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var singleSubject in subjectsSplit)
                    {
                        var finalSubject = singleSubject.Trim();
                        if (!string.IsNullOrWhiteSpace(finalSubject))
                        {
                            if (subjectCounts.ContainsKey(finalSubject))
                            {
                                subjectCounts[finalSubject]++;
                            }
                            else
                            {
                                subjectCounts[finalSubject] = 1;
                            }
                        }
                    }
                }
            }

            // Convert weekly counts to yearly counts
            var yearlySubjectHours = new Dictionary<string, int>();
            foreach (var pair in subjectCounts)
            {
                // Multiply weekly occurrences by 35 working weeks
                yearlySubjectHours[pair.Key] = pair.Value * 35 / 2;  
            }

            return yearlySubjectHours;
        }
        public string FormatYearlySubjectHours(Dictionary<string, int> yearlySubjectHours)
        {
            StringBuilder result = new StringBuilder();
            foreach (var pair in yearlySubjectHours)
            {
                // Append each subject and its hours to the StringBuilder object
                result.AppendLine($"{pair.Key}: {pair.Value} hours");
            }

            // Return the formatted string
            return result.ToString();
        }
    }
}
