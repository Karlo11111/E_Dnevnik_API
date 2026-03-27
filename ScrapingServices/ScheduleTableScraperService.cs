using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using E_Dnevnik_API.Models.ScheduleTable;
using E_Dnevnik_API.Models.ScrapeSubjects;
using HtmlAgilityPack;

namespace E_Dnevnik_API.ScrapingServices
{
    // skida raspored sati s /schedule stranice i računa godišnji fond sati po predmetu
    public class ScheduleTableScraperService
    {
        public async Task<ScheduleResult> ScrapeScheduleTable(string email, string password)
        {
            var loginResult = await EduHrLoginService.LoginAsync(email, password);
            if (loginResult.Client is null)
                throw new ScraperException(loginResult.StatusCode, loginResult.Error);

            using var httpClient = loginResult.Client;

            var scrapeResponse = await httpClient.GetAsync("https://ocjene.skole.hr/schedule");
            if (!scrapeResponse.IsSuccessStatusCode)
                throw new ScraperException((int)scrapeResponse.StatusCode, "nije uspjelo dohvatiti raspored.");

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            return await ExtractScrapeData(scrapeHtmlContent);
        }

        private async Task<ScheduleResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var scheduleResult = new ScheduleResult() { Schedule = new List<ScheduleTable>() };

            var daysOfWeek = new string[] { "PON", "UTO", "SRI", "ČET", "PET", "SUB" };

            foreach (var day in daysOfWeek)
            {
                var daySchedule = new ScheduleTable() { Day = day, Subjects = new List<string>() };

                // raspored je podijeljen po danima, svaki dan ima svoj flex-table-schedule div s data-action-id
                var scheduleTablesXPath = $"//div[contains(@class, 'flex-table-schedule') and @data-action-id=\"{day}\"]";
                var scheduleTables = htmlDoc.DocumentNode.SelectNodes(scheduleTablesXPath);

                if (scheduleTables != null)
                {
                    foreach (var table in scheduleTables)
                    {
                        var periodRows = table.SelectNodes(
                            ".//div[contains(@class, 'row') and not(contains(@class, 'header'))]"
                        );
                        foreach (var row in periodRows)
                        {
                            var subjectCell = row.SelectSingleNode(
                                ".//div[contains(@class, 'cell') and not(contains(@class, 'no-box'))]"
                            );
                            if (subjectCell != null)
                            {
                                var subject = WebUtility.HtmlDecode(subjectCell.InnerText.Trim());
                                if (!string.IsNullOrWhiteSpace(subject))
                                    daySchedule.Subjects.Add(CleanText(subject));
                            }
                        }
                    }
                }

                // ako ima više od 7 sati, škola ima jutarnju i poslijepodnevnu smjenu - dijelimo po tome
                const int morningPeriods = 7;
                if (daySchedule.Subjects.Count > morningPeriods)
                {
                    var morningSchedule = new ScheduleTable
                    {
                        Day = day + " Morning",
                        Subjects = daySchedule.Subjects.Take(morningPeriods).ToList(),
                    };
                    var afternoonSchedule = new ScheduleTable
                    {
                        Day = day + " Afternoon",
                        Subjects = daySchedule.Subjects.Skip(morningPeriods).ToList(),
                    };

                    scheduleResult.Schedule.Add(morningSchedule);
                    scheduleResult.Schedule.Add(afternoonSchedule);
                }
                else
                {
                    scheduleResult.Schedule.Add(daySchedule);
                }
            }

            return scheduleResult;
        }

        private string CleanText(string text)
        {
            text = text.Trim();
            text = Regex.Replace(text, "[^a-zA-Z0-9\\s/,čćđšžČĆĐŠŽ]", "");
            text = Regex.Replace(text, "\\s+", " ");
            return text;
        }

        // broji koliko sati tjedno ima svaki predmet pa množi s 35 tjedana da dobijemo godišnji fond
        public Dictionary<string, int> CalculateYearlySubjectHours(ScheduleResult schedule)
        {
            var subjectCounts = new Dictionary<string, int>();

            foreach (var day in schedule.Schedule)
            {
                foreach (var subject in day.Subjects)
                {
                    string cleanSubject = CleanText(subject);

                    // neki sati imaju više predmeta odjednom, npr. "Kemija s vježbama, Fizika"
                    var subjectsSplit = cleanSubject.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var singleSubject in subjectsSplit)
                    {
                        var finalSubject = singleSubject.Trim();
                        if (!string.IsNullOrWhiteSpace(finalSubject))
                        {
                            if (subjectCounts.ContainsKey(finalSubject))
                                subjectCounts[finalSubject]++;
                            else
                                subjectCounts[finalSubject] = 1;
                        }
                    }
                }
            }

            // množimo s 35/2 jer je školska godina ~35 tjedana a raspored se mijenja po polugođu
            var yearlySubjectHours = new Dictionary<string, int>();
            foreach (var pair in subjectCounts)
                yearlySubjectHours[pair.Key] = pair.Value * 35 / 2;

            return yearlySubjectHours;
        }

        public string FormatYearlySubjectHours(Dictionary<string, int> yearlySubjectHours)
        {
            var result = new StringBuilder();
            foreach (var pair in yearlySubjectHours)
                result.AppendLine($"{pair.Key}: {pair.Value} hours");

            return result.ToString();
        }
    }
}
