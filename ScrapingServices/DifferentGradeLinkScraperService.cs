using System.Text.RegularExpressions;
using E_Dnevnik_API.Models.DifferentGradeLinks;
using E_Dnevnik_API.Models.ScrapeSubjects;
using HtmlAgilityPack;

namespace E_Dnevnik_API.ScrapingServices
{
    // skida predmete i ocjene za sve razrede koje je učenik pohađao, ne samo trenutni
    public class DifferentGradeLinkScraperService
    {
        public async Task<List<GradeSubjectDetails>> ScrapeDifferentGradeLink(string email, string password)
        {
            var loginResult = await EduHrLoginService.LoginAsync(email, password);
            if (loginResult.Client is null)
                throw new ScraperException(loginResult.StatusCode, loginResult.Error);

            using var httpClient = loginResult.Client;

            // prvo dohvaćamo listu svih razreda s /class stranice
            var gradeLinks = await FetchGradeLinks(httpClient);
            var allGradeSubjectDetails = new List<GradeSubjectDetails>();

            foreach (var grade in gradeLinks.DifferentGrade)
            {
                var classPageUrl = $"https://ocjene.skole.hr{grade.GradeLink}";

                var classPageResponse = await httpClient.GetAsync(classPageUrl);
                if (!classPageResponse.IsSuccessStatusCode)
                    continue;

                var classPageContent = await classPageResponse.Content.ReadAsStringAsync();

                var subjectDetails = await ExtractSubjectDetails(classPageContent, httpClient);
                allGradeSubjectDetails.Add(new GradeSubjectDetails(grade.GradeYear, subjectDetails.Subjects));
            }

            return allGradeSubjectDetails;
        }

        // dohvaća listu razreda i njihove linkove s /class stranice
        private async Task<DifferentGradeResult> FetchGradeLinks(HttpClient httpClient)
        {
            var response = await httpClient.GetAsync("https://ocjene.skole.hr/class");
            var htmlContent = await response.Content.ReadAsStringAsync();
            return await ExtractGradeLinks(htmlContent);
        }

        private async Task<DifferentGradeResult> ExtractGradeLinks(string htmlContent)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var subjectList = new DifferentGradeResult();

            // hvata i tekući i prošle razrede - oba imaju class-menu-vertical
            var differentGrade = htmlDoc.DocumentNode.SelectNodes(
                "//div[contains(@class, 'class-menu-vertical past-schoolyear') or contains(@class, 'class-menu-vertical')]"
            );

            try
            {
                if (differentGrade != null)
                {
                    foreach (var grade in differentGrade)
                    {
                        var gradeLink = grade
                            .SelectSingleNode(".//div[@class='class-info']/a")
                            ?.Attributes["href"]?.Value;
                        var gradeName = grade
                            .SelectSingleNode(
                                ".//div[@class='class-info']/a[@class='school-data']/div[@class='class']/span[@class='bold']"
                            )
                            ?.InnerText;

                        if (gradeLink != null && gradeName != null)
                        {
                            subjectList.DifferentGrade.Add(
                                new DifferentGrade { GradeYear = gradeName, GradeLink = gradeLink }
                            );
                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }

            return subjectList;
        }

        // ako predmet nema prosječnu ocjenu na listi, odlazimo na stranicu predmeta i tražimo zaključnu
        private async Task<SubjectScrapeResult> ExtractSubjectDetails(string htmlContent, HttpClient httpClient)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var subjectList = new List<SubjectInfo>();
            var courseNodes = htmlDoc.DocumentNode.SelectNodes("//ul[@class='list']/li/a");

            if (courseNodes != null)
            {
                foreach (var aNode in courseNodes)
                {
                    var subjectNameNode = aNode.SelectSingleNode(".//div[@class='course-info']/span[1]");
                    var professorNameNode = aNode.SelectSingleNode(".//div[@class='course-info']/span[2]");
                    var gradeNode = aNode.SelectSingleNode(".//div[@class='list-average-grade ']/span");

                    var subjectName = subjectNameNode != null ? CleanText(subjectNameNode.InnerText) : "N/A";
                    var professorName = professorNameNode != null ? CleanText(professorNameNode.InnerText) : "N/A";
                    var gradeText = gradeNode != null ? CleanText(gradeNode.InnerText) : "N/A";
                    var hrefValue = ExtractNumbers(aNode.GetAttributeValue("href", ""));

                    if (gradeText == "N/A")
                    {
                        var gradePageUrl = $"https://ocjene.skole.hr/grade/{hrefValue}";
                        var classPageContent = await httpClient.GetStringAsync(gradePageUrl);
                        var classDoc = new HtmlDocument();
                        classDoc.LoadHtml(classPageContent);

                        var alternativeGradeNode = classDoc.DocumentNode.SelectSingleNode(
                            "//div[@class='flex-table s  grades-table ']/div[@class='row final-grade ']/div[@class='cell']/span"
                        );
                        gradeText = alternativeGradeNode != null
                            ? ExtractNumbers(alternativeGradeNode.InnerText)
                            : "N/A";
                    }

                    subjectList.Add(new SubjectInfo(subjectName, professorName, gradeText, hrefValue));
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
