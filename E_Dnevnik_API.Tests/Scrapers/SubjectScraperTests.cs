using E_Dnevnik_API.ScrapingServices;
using RichardSzalay.MockHttp;

namespace E_Dnevnik_API.Tests.Scrapers;

public class SubjectScraperTests
{
    private const string SubjectHtml = """
        <html><body>
        <ul class="list">
          <li>
            <a href="/grade/12345">
              <div class="course-info">
                <span>Matematika</span>
                <span>Prof Horvat</span>
              </div>
              <div class="list-average-grade "><span>4</span></div>
            </a>
          </li>
          <li>
            <a href="/grade/67890">
              <div class="course-info">
                <span>Fizika</span>
                <span>Prof Kovac</span>
              </div>
              <div class="list-average-grade "><span>3</span></div>
            </a>
          </li>
        </ul>
        </body></html>
        """;

    [Fact]
    public async Task ScrapeSubjects_ParsesSubjectNamesAndProfessors()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://ocjene.skole.hr/course").Respond("text/html", SubjectHtml);
        var client = mockHttp.ToHttpClient();

        var service = new ScraperService();
        var result = await service.ScrapeSubjects(client);

        Assert.Equal(2, result.Subjects!.Count);
        Assert.Equal("Matematika", result.Subjects[0].SubjectName);
        Assert.Equal("Prof Horvat", result.Subjects[0].Professor);
        Assert.Equal("4", result.Subjects[0].Grade);
        Assert.Equal("12345", result.Subjects[0].SubjectId);
        Assert.Equal("Fizika", result.Subjects[1].SubjectName);
        Assert.Equal("67890", result.Subjects[1].SubjectId);
    }

    [Fact]
    public void ExtractNumbers_ExtractsFromHref()
    {
        Assert.Equal("75229928950", ScraperService.ExtractNumbers("/grade/75229928950"));
        Assert.Equal("123", ScraperService.ExtractNumbers("/grade/123"));
        Assert.Equal("", ScraperService.ExtractNumbers("/no-numbers-here"));
    }
}
