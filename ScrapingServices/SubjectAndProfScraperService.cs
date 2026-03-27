using System.Text.RegularExpressions;
using E_Dnevnik_API.Models.ScrapeSubjects;
using HtmlAgilityPack;

namespace E_Dnevnik_API.ScrapingServices
{
    // skida listu predmeta s ocjenama s /course stranice
    public class ScraperService
    {
        public async Task<SubjectScrapeResult> ScrapeSubjects(string email, string password)
        {
            var loginResult = await EduHrLoginService.LoginAsync(email, password);
            if (loginResult.Client is null)
                throw new ScraperException(loginResult.StatusCode, loginResult.Error);

            using var httpClient = loginResult.Client;

            var scrapeResponse = await httpClient.GetAsync("https://ocjene.skole.hr/course");
            if (!scrapeResponse.IsSuccessStatusCode)
                throw new ScraperException(
                    (int)scrapeResponse.StatusCode,
                    "nije uspjelo dohvatiti stranicu s predmetima."
                );

            var scrapeHtmlContent = await scrapeResponse.Content.ReadAsStringAsync();
            return await ExtractScrapeData(scrapeHtmlContent);
        }

        // vadi predmete iz html-a - svaki <li> u listi je jedan predmet
        private async Task<SubjectScrapeResult> ExtractScrapeData(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var courseNodes = htmlDoc.DocumentNode.SelectNodes("//ul[@class='list']/li/a");
            var subjectList = new List<SubjectInfo>();

            if (courseNodes != null)
            {
                foreach (var aNode in courseNodes)
                {
                    var subjectNameNode = aNode.SelectSingleNode(
                        ".//div[@class='course-info']/span[1]"
                    );
                    var professorNameNode = aNode.SelectSingleNode(
                        ".//div[@class='course-info']/span[2]"
                    );
                    var gradeNode = aNode.SelectSingleNode(
                        ".//div[@class='list-average-grade ']/span"
                    );

                    // id predmeta je broj iz href atributa, npr. /grade/75229928950 -> 75229928950
                    string hrefValue = ExtractNumbers(
                        aNode.GetAttributeValue("href", string.Empty)
                    );

                    var subjectName =
                        subjectNameNode != null ? CleanText(subjectNameNode.InnerText) : "N/A";
                    var professorName =
                        professorNameNode != null ? CleanText(professorNameNode.InnerText) : "N/A";
                    var gradeText = gradeNode != null ? CleanText(gradeNode.InnerText) : "N/A";

                    subjectList.Add(
                        new SubjectInfo(subjectName, professorName, gradeText, hrefValue)
                    );
                }
            }

            return new SubjectScrapeResult { Subjects = subjectList };
        }

        // izvlači samo brojeve iz stringa - koristi se za id predmeta iz linka
        public static string ExtractNumbers(string input)
        {
            Regex regex = new Regex(@"\d+");
            Match match = regex.Match(input);

            string number = "";
            while (match.Success)
            {
                number += match.Value;
                match = match.NextMatch();
            }

            return number;
        }

        private string CleanText(string text)
        {
            text = text.Trim();
            text = Regex.Replace(text, @"\s+", " ");
            return text;
        }
    }
}
