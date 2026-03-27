using E_Dnevnik_API.Models.NewGrades;
using E_Dnevnik_API.Models.ScrapeSubjects;
using HtmlAgilityPack;

namespace E_Dnevnik_API.ScrapingServices
{
    // skida ocjene koje su nedavno upisane - stranica /grade/new pokazuje što je novo
    public class NewGradesScraperService
    {
        public async Task<NewGradesResult> ScrapeNewGrades(string email, string password)
        {
            var loginResult = await EduHrLoginService.LoginAsync(email, password);
            if (loginResult.Client is null)
                throw new ScraperException(loginResult.StatusCode, loginResult.Error);

            using var httpClient = loginResult.Client;

            var scrapeResponse = await httpClient.GetAsync("https://ocjene.skole.hr/grade/new");
            if (!scrapeResponse.IsSuccessStatusCode)
                throw new ScraperException((int)scrapeResponse.StatusCode, "nije uspjelo dohvatiti nove ocjene.");

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            return await ExtractScrapeData(scrapeHtmlContent);
        }

        // svaki new-grades-table div je jedna nova ocjena s detaljima
        private async Task<NewGradesResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var newGradeNodes = htmlDoc.DocumentNode.SelectNodes("//div[@class='flex-table new-grades-table']");
            var grades = new List<NewGrades>();

            if (newGradeNodes != null)
            {
                foreach (var gradeNode in newGradeNodes)
                {
                    var subjectName = gradeNode
                        .SelectSingleNode(".//div[@class='row header first']//div[@class='cell']")
                        ?.InnerText;

                    var dateOfGrade = gradeNode
                        .SelectSingleNode(".//div[@class='row ']//div[@class='cell']/span")
                        ?.InnerText;

                    var description = gradeNode
                        .SelectSingleNode(".//div[@class='row ']//div[@class='box']//div[@class='cell ']/span")
                        ?.InnerText;

                    var grade = gradeNode
                        .SelectSingleNode(".//div[@class='row ']//div[@class='box']//div[@class='cell'][2]")
                        ?.InnerText;

                    var elementOfEvaluation = gradeNode
                        .SelectSingleNode(".//div[@class='row ']//div[@class='box']//div[@class='cell'][1]")
                        ?.InnerText;

                    grades.Add(new NewGrades
                    {
                        Date = dateOfGrade,
                        Description = description,
                        SubjectName = subjectName,
                        GradeNumber = grade,
                        ElementOfEvaluation = elementOfEvaluation,
                    });
                }
            }

            return new NewGradesResult { Grades = grades.Count > 0 ? grades : null };
        }
    }
}
