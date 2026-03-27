using E_Dnevnik_API.Models.NewTests;
using HtmlAgilityPack;

namespace E_Dnevnik_API.ScrapingServices
{
    // skida ispite koji su nedavno dodani - stranica /exam/new
    public class NewTestsScraperService
    {
        public async Task<NewTestsResult> ScrapeNewTests(HttpClient client)
        {
            var scrapeResponse = await client.GetAsync("https://ocjene.skole.hr/exam/new");
            if (!scrapeResponse.IsSuccessStatusCode)
                throw new ScraperException(
                    (int)scrapeResponse.StatusCode,
                    "nije uspjelo dohvatiti nove ispite."
                );

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            return ExtractScrapeData(scrapeHtmlContent);
        }

        // svaki new-exam-table div je jedan novi ispit
        private NewTestsResult ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var newTestNodes = htmlDoc.DocumentNode.SelectNodes(
                "//div[@class='flex-table new-exam-table']"
            );
            var tests = new List<NewTests>();

            if (newTestNodes != null)
            {
                foreach (var testNode in newTestNodes)
                {
                    var dateOfGrade = testNode
                        .SelectSingleNode(".//div[@class='row']//div[@class='cell']/span")
                        ?.InnerText;

                    var testSubject = testNode
                        .SelectSingleNode(
                            ".//div[@class='row']//div[@class='box']/div[@class='cell'][1]/span"
                        )
                        ?.InnerText;

                    var description = testNode
                        .SelectSingleNode(
                            ".//div[@class='row']//div[@class='box']/div[@class='cell'][2]/span"
                        )
                        ?.InnerText;

                    tests.Add(
                        new NewTests
                        {
                            Date = dateOfGrade,
                            Description = description,
                            TestSubject = testSubject,
                        }
                    );
                }
            }

            return new NewTestsResult { Tests = tests.Count > 0 ? tests : null };
        }
    }
}
