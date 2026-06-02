using E_Dnevnik_API.Models.Absences_izostanci;
using E_Dnevnik_API.ScrapingServices;
using RichardSzalay.MockHttp;

namespace E_Dnevnik_API.Tests.Scrapers;

public class AbsenceScraperTests
{
    private const string AbsenceHtml = """
        <html><body>
        <div aria-label="AbsentTable">
          <div class="row header first"><div class="cell">15.01.2026.</div></div>
          <div class="row">
            <div class="box">
              <div class="cell"><span>Matematika</span></div>
            </div>
          </div>
          <div class="row">
            <div class="box">
              <div class="cell"><span>Fizika</span></div>
            </div>
          </div>
        </div>
        <div aria-label="AbsentTable">
          <div class="row header first"><div class="cell">20.01.2026.</div></div>
          <div class="row">
            <div class="box">
              <div class="cell"><span>Matematika</span></div>
            </div>
          </div>
        </div>
        </body></html>
        """;

    [Fact]
    public async Task ScrapeAbsences_ParsesDatesAndSubjects()
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("https://ocjene.skole.hr/absent").Respond("text/html", AbsenceHtml);
        var client = mockHttp.ToHttpClient();

        var service = new AbsenceScraperService();
        var result = await service.ScrapeAbsences(client);

        Assert.Equal(2, result.Absences!.Length);
        Assert.Equal("15.01.2026.", result.Absences[0].Date);
        Assert.Contains("Matematika", result.Absences[0].Subjects!);
        Assert.Contains("Fizika", result.Absences[0].Subjects!);
        Assert.Single(result.Absences[1].Subjects!);
        Assert.Equal("Matematika", result.Absences[1].Subjects![0]);
    }

    [Fact]
    public void CalculateDaysMissed_CountsSubjectOccurrences()
    {
        var absences = new AbsencesResult
        {
            Absences =
            [
                new AbsenceRecord { Date = "15.01.2026.", Subjects = ["Matematika", "Fizika"] },
                new AbsenceRecord { Date = "20.01.2026.", Subjects = ["Matematika"] },
            ]
        };

        var service = new AbsenceScraperService();
        var result = service.CalculateDaysMissed(absences);

        Assert.Equal(2, result["Matematika"]);
        Assert.Equal(1, result["Fizika"]);
    }
}
