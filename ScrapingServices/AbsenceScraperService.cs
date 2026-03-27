using E_Dnevnik_API.Models.Absences_izostanci;
using HtmlAgilityPack;

namespace E_Dnevnik_API.ScrapingServices
{
    // skida izostanke s /absent stranice
    public class AbsenceScraperService
    {
        public async Task<AbsencesResult> ScrapeAbsences(HttpClient client)
        {
            var scrapeResponse = await client.GetAsync("https://ocjene.skole.hr/absent");
            if (!scrapeResponse.IsSuccessStatusCode)
                throw new ScraperException(
                    (int)scrapeResponse.StatusCode,
                    "nije uspjelo dohvatiti izostanke."
                );

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            return ExtractScrapeData(scrapeHtmlContent);
        }

        private AbsencesResult ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            // svaki AbsentTable div je jedan datum s popisom sati koje je učenik izostao
            var absenceDateNodes = htmlDoc.DocumentNode.SelectNodes(
                "//div[@aria-label='AbsentTable']"
            );
            var absences = new List<AbsenceRecord>();

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
                    absences.Add(new AbsenceRecord { Date = date, Subjects = subjects });
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
