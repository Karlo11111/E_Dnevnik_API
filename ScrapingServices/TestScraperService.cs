using System.Text.RegularExpressions;
using E_Dnevnik_API.Models.ScrapeTests;
using HtmlAgilityPack;

namespace E_Dnevnik_API.ScrapingServices
{
    // skida raspored pisanih zadaća s /exam stranice, grupirane po mjesecima
    public class TestScraperService
    {
        public async Task<Dictionary<string, List<TestInfo>>> ScrapeTests(HttpClient client)
        {
            var scrapeResponse = await client.GetAsync("https://ocjene.skole.hr/exam");
            if (!scrapeResponse.IsSuccessStatusCode)
                throw new ScraperException(
                    (int)scrapeResponse.StatusCode,
                    "nije uspjelo dohvatiti ispite."
                );

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            return ExtractScrapeData(scrapeHtmlContent);
        }

        // svaki exam-table div je jedan mjesec, unutra su redovi s ispitima
        private Dictionary<string, List<TestInfo>> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var monthlyTests = new Dictionary<string, List<TestInfo>>();

            var examTableNodes = htmlDoc.DocumentNode.SelectNodes(
                "//div[contains(@class, 'exam-table')]"
            );

            foreach (var tableNode in examTableNodes)
            {
                // naziv mjeseca je u data-action-id atributu
                var monthName = tableNode.GetAttributeValue("data-action-id", string.Empty);
                if (string.IsNullOrWhiteSpace(monthName))
                    continue;

                var testsForMonth = new List<TestInfo>();

                var testNodes = tableNode.SelectNodes(
                    ".//div[contains(@class, 'row') and not(contains(@class, 'row header'))]"
                );

                if (testNodes != null)
                {
                    foreach (var rowNode in testNodes)
                    {
                        var dateCell = rowNode.SelectSingleNode(".//div[@class='cell'][1]");
                        var nameCell = rowNode.SelectSingleNode(
                            ".//div[@class='box']/div[@class='cell'][1]"
                        );
                        var descriptionCell = rowNode.SelectSingleNode(
                            ".//div[@class='box']/div[@class='cell'][2]"
                        );

                        if (dateCell != null && descriptionCell != null && nameCell != null)
                        {
                            var testDate = dateCell.InnerText.Trim();
                            var testDescription = descriptionCell.InnerText.Trim();
                            var testName = nameCell.InnerText.Trim();

                            testsForMonth.Add(new TestInfo(testName, testDescription, testDate));
                        }
                        else
                        {
                            continue;
                        }
                    }
                }

                monthlyTests[monthName] = testsForMonth;
            }

            return monthlyTests;
        }
    }
}
