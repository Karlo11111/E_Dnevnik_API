using System.Text.RegularExpressions;
using E_Dnevnik_API.Models.SpecificSubject;
using HtmlAgilityPack;

namespace E_Dnevnik_API.ScrapingServices
{
    // skida detalje jednog predmeta - ocjene po elementima vrednovanja i po mjesecima
    public class SpecificSubjectScraperService
    {
        public async Task<SubjectDetails> ScrapeSubjects(HttpClient client, string subjectId)
        {
            // id predmeta dolazi iz url-a s liste predmeta, npr. 75229928950
            var scrapeResponse = await client.GetAsync(
                $"https://ocjene.skole.hr/grade/{subjectId}"
            );
            if (!scrapeResponse.IsSuccessStatusCode)
                throw new ScraperException(
                    (int)scrapeResponse.StatusCode,
                    "nije uspjelo dohvatiti stranicu predmeta."
                );

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            return ExtractSubjectDetails(scrapeHtmlContent);
        }

        private SubjectDetails ExtractSubjectDetails(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var finalGradeRow = htmlDoc.DocumentNode.SelectSingleNode(
                "//div[@class='flex-table s  grades-table ']//div[@class='row final-grade ']/span"
            );

            var evaluationElements = ExtractEvaluationElements(htmlDoc);
            var monthlyGrades = ExtractGradesByMonth(htmlDoc);

            return new SubjectDetails
            {
                EvaluationElements = evaluationElements,
                MonthlyGrades = monthlyGrades,
                FinalGrade = finalGradeRow?.InnerText.Trim() ?? "N/A",
            };
        }

        // vadi ocjene po elementima vrednovanja (npr. usmeno, pismeno, projekt...)
        private List<EvaluationElement> ExtractEvaluationElements(HtmlDocument htmlDoc)
        {
            var rows = htmlDoc.DocumentNode.SelectNodes(
                "//div[@class='flex-table s  grades-table ']//div[@class='row']"
            );
            var evaluationElements = new List<EvaluationElement>();

            if (rows != null)
            {
                foreach (var row in rows)
                {
                    var nameNode = row.SelectSingleNode(".//div[@class='cell first']");
                    if (nameNode == null || nameNode.InnerText.Contains("ZAKLJUČENO"))
                        continue;

                    var grades = row.SelectNodes(".//div[@class='cell grade']");
                    var monthlyGrades = grades
                        ?.Select(gradeNode => gradeNode.InnerText.Trim())
                        .ToList();

                    evaluationElements.Add(
                        new EvaluationElement
                        {
                            Name = nameNode.InnerText.Trim(),
                            GradesByMonth = CleanText(monthlyGrades) ?? new List<string>(),
                        }
                    );
                }
            }

            return evaluationElements;
        }

        // vadi ocjene grupirane po mjesecu - svaki redak je jedna ocjena s datumom i bilješkom
        private List<MonthlyGrades> ExtractGradesByMonth(HtmlDocument htmlDoc)
        {
            var rows = htmlDoc.DocumentNode.SelectNodes("//div[@class='row  ']");
            var monthlyGrades = new Dictionary<string, MonthlyGrades>();

            if (rows == null)
                return new List<MonthlyGrades>();

            foreach (var row in rows)
            {
                var dateNode = row.SelectSingleNode(".//div[@data-date]");
                var gradeNode = row.SelectSingleNode(".//div[@class='box']/div[1]/span");
                var noteNode = row.SelectSingleNode(".//div[@class='box']/div[2]/span");

                if (dateNode == null || noteNode == null)
                    continue;

                var dateText = dateNode.InnerText.Trim();
                var gradeText = gradeNode?.InnerText.Trim() ?? "N/A";
                var noteText = noteNode.InnerText.Trim();

                // izvlačimo mjesec iz datuma, npr. "10.12." -> "12"
                var parts = dateText.Split('.');
                if (parts.Length < 2)
                    continue;
                var month = parts[1];

                if (!monthlyGrades.ContainsKey(month))
                    monthlyGrades[month] = new MonthlyGrades(month);

                var specificSubject = new SpecificSubject(dateText, noteText, gradeText);
                monthlyGrades[month].Grades.Add(specificSubject);
            }

            return monthlyGrades.Values.ToList();
        }

        private List<string> CleanText(List<string> texts)
        {
            return texts
                .Select(text =>
                {
                    if (string.IsNullOrEmpty(text))
                        return string.Empty;

                    text = text.Trim();
                    text = Regex.Replace(text, @"[^a-zA-Z0-9čćšđžČĆŠĐŽ\s/,]", "");
                    return Regex.Replace(text, "\\s+", " ");
                })
                .ToList();
        }
    }
}
