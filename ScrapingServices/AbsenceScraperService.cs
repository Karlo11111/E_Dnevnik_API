using E_Dnevnik_API.Models.Absences_izostanci;
using E_Dnevnik_API.Models.ScrapeSubjects;
using HtmlAgilityPack;

namespace E_Dnevnik_API.ScrapingServices
{
    // skida izostanke s /absent stranice
    public class AbsenceScraperService
    {
        public async Task<AbsencesResult> ScrapeAbsences(string email, string password)
        {
            var loginResult = await EduHrLoginService.LoginAsync(email, password);
            if (loginResult.Client is null)
                throw new ScraperException(loginResult.StatusCode, loginResult.Error);

            using var httpClient = loginResult.Client;

            var scrapeResponse = await httpClient.GetAsync("https://ocjene.skole.hr/absent");
            if (!scrapeResponse.IsSuccessStatusCode)
                throw new ScraperException(
                    (int)scrapeResponse.StatusCode,
                    "nije uspjelo dohvatiti izostanke."
                );

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            return await ExtractScrapeData(scrapeHtmlContent);
        }

        private async Task<AbsencesResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            // svaki AbsentTable div je jedan datum s popisom sati koje je učenik izostao
            var absenceDateNodes = htmlDoc.DocumentNode.SelectNodes(
                "//div[@aria-label='AbsentTable']"
            );
            var absences = new List<AbsanceRecord>();

            if (absenceDateNodes != null)
            {
                foreach (var dateNode in absenceDateNodes)
                {
                    var date = dateNode
                        .SelectSingleNode(".//div[@class='row header first']//div[@class='cell']")
                        ?.InnerText.Trim();
                    var subjects = dateNode
                        .SelectNodes(
                            ".//div[contains(@class, 'row')]/div[@class='box']/div[@class='cell'][1]/span[1]"
                        )
                        .Select(node => node.InnerText.Trim())
                        .ToList();
                    absences.Add(new AbsanceRecord { Date = date, Subjects = subjects });
                }
            }

            return new AbsencesResult { Absences = absences.ToArray() };
        }

        // broji koliko puta je svaki predmet izostao - koristi se za izračun postotka
        public Dictionary<string, int> CalculateDaysMissed(AbsencesResult absences)
        {
            var daysMissedPerSubject = new Dictionary<string, int>();

            foreach (var absence in absences.Absences)
            {
                foreach (var subject in absence.Subjects)
                {
                    if (daysMissedPerSubject.ContainsKey(subject))
                        daysMissedPerSubject[subject]++;
                    else
                        daysMissedPerSubject[subject] = 1;
                }
            }

            return daysMissedPerSubject;
        }
    }
}
