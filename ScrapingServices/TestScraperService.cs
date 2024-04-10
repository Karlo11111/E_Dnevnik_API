using E_Dnevnik_API.Models.ScrapeTests;
using HtmlAgilityPack;

namespace E_Dnevnik_API.ScrapingServices
{
    public class TestScraperService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public TestScraperService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<List<TestInfo>> ScrapeTests(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var testNodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class,'flex-table') and contains(@class,'exam-table')]/div[contains(@class,'row')]");
            var testInfoList = new List<TestInfo>();

            if (testNodes != null)
            {
                foreach (var node in testNodes)
                {
                    var testName = node.SelectSingleNode(".//some/path/for/name")?.InnerText.Trim();
                    var testDescription = node.SelectSingleNode(".//some/path/for/description")?.InnerText.Trim();
                    var testDate = node.SelectSingleNode(".//some/path/for/date")?.InnerText.Trim();

                    testInfoList.Add(new TestInfo(testName, testDescription, testDate));
                }
            }

            return testInfoList;
        }
    }
}
