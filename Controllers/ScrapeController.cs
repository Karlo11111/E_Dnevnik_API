using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using E_Dnevnik_API.Models.Absences_izostanci;
using E_Dnevnik_API.Models.DifferentGradeLinks;
using E_Dnevnik_API.Models.NewGrades;
using E_Dnevnik_API.Models.ScheduleTable;
using E_Dnevnik_API.Models.ScrapeStudentProfile;
using E_Dnevnik_API.Models.ScrapeSubjects;
using E_Dnevnik_API.Models.ScrapeTests;
using E_Dnevnik_API.Models.SpecificSubject;
using E_Dnevnik_API.ScrapingServices;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScraperController : ControllerBase
    {
        private readonly ScraperService _subjectScraperService;
        private readonly SpecificSubjectScraperService _specificSubjectScraperService;
        private readonly TestScraperService _testScraperService;
        private readonly StudentProfileScraperService _studentProfileScraperService;
        private readonly DifferentGradeLinkScraperService _differentGradeLinkScraperService;
        private readonly AbsenceScraperService _absenceScraperService;
        private readonly ScheduleTableScraperService _scheduleTableScraperService;
        private readonly NewGradesScraperService _newGradesScraperService;

        // Constructor to initialize the services
        public ScraperController(IHttpClientFactory httpClientFactory)
        {
            _subjectScraperService = new ScraperService(httpClientFactory);
            _testScraperService = new TestScraperService(httpClientFactory);
            _studentProfileScraperService = new StudentProfileScraperService(httpClientFactory);
            _differentGradeLinkScraperService = new DifferentGradeLinkScraperService(
                httpClientFactory
            );
            _specificSubjectScraperService = new SpecificSubjectScraperService(httpClientFactory);
            _absenceScraperService = new AbsenceScraperService(httpClientFactory);
            _scheduleTableScraperService = new ScheduleTableScraperService(httpClientFactory);
            _newGradesScraperService = new NewGradesScraperService(httpClientFactory);
        }

        //function to scrape subjects and professors
        [HttpPost("ScrapeSubjectsAndProfessors")]
        public async Task<ActionResult<SubjectScrapeResult>> ScrapeSubjects(
            [FromBody] ScrapeRequest request
        )
        {
            // Call the service method that returns ScrapeResult
            var actionResult = await _subjectScraperService.ScrapeSubjects(request);

            // Return the result
            return actionResult;
        }

        [HttpPost("ScrapeSpecificSubjectGrades")]
        public async Task<ActionResult<SubjectDetails>> ScrapeSpecificSubjectGrades(
            [FromBody] ScrapeRequest request,
            [FromQuery] string subjectId
        )
        {
            // Validate the subjectId
            if (string.IsNullOrEmpty(subjectId))
            {
                return BadRequest("Subject ID must be provided.");
            }

            // Call the service method that scrapes grades for the specific subject
            var actionResult = await _specificSubjectScraperService.ScrapeSubjects(
                request,
                subjectId
            );

            // Return the result
            return actionResult;
        }

        //function to scrape student profile
        [HttpPost("ScrapeStudentProfile")]
        public async Task<ActionResult<StudentProfileResult>> ScrapeStudentProfile(
            [FromBody] ScrapeRequest request
        )
        {
            // Call the service method that returns StudentProfileResult
            var actionResult = await _studentProfileScraperService.ScrapeStudentProfile(request);

            // Return the result
            return actionResult;
        }

        //function to scrape tests
        [HttpPost("ScrapeTests")]
        public async Task<ActionResult<TestResult>> ScrapeTests([FromBody] ScrapeRequest request)
        {
            // Call the service method that returns TestResult
            var actionResult = await _testScraperService.ScrapeTests(request);

            // Return the result
            return actionResult;
        }

        //function to scrape different grades
        [HttpPost("ScrapeDifferentGrades")]
        public async Task<ActionResult<List<GradeSubjectDetails>>> ScrapeDifferentGradeLink(
            [FromBody] ScrapeRequest request
        )
        {
            // Call the service method that returns List<GradeSubjectDetails>
            var actionResult = await _differentGradeLinkScraperService.ScrapeDifferentGradeLink(
                request
            );

            // Return the result
            return actionResult;
        }

        //function to scrape absences
        [HttpPost("ScrapeAbsences")]
        public async Task<ActionResult<AbsencesResult>> ActionResult(
            [FromBody] ScrapeRequest request
        )
        {
            // Call the service method that returns AbsencesResult
            var actionResult = await _absenceScraperService.ScrapeAbsences(request);

            // Return the result
            return actionResult;
        }

        //function to scrape schedule table
        [HttpPost("ScrapeScheduleTable")]
        public async Task<ActionResult<ScheduleResult>> ScrapeScheduleTable(
            [FromBody] ScrapeRequest request
        )
        {
            // Call the service method that returns ScheduleResult
            var actionResult = await _scheduleTableScraperService.ScrapeScheduleTable(request);

            // Return the result
            return actionResult;
        }

        //function to scrape new grades
        [HttpPost("ScrapeNewGrades")]
        public async Task<ActionResult<NewGradesResult>> ScrapeNewGrades(
            [FromBody] ScrapeRequest request
        )
        {
            // Call the service method that returns NewGradesResult
            var actionResult = await _newGradesScraperService.ScrapeNewGrades(request);

            // Return the result
            return actionResult;
        }

        //calculate missed class percentages
        [HttpPost("CalculateMissedClassPercentages")]
        public async Task<ActionResult<Dictionary<string, double>>> CalculateMissedClassPercentages(
            [FromBody] ScrapeRequest request
        )
        {
            // Retrieve schedule data to get total yearly hours per subject
            var scheduleActionResult = await _scheduleTableScraperService.ScrapeScheduleTable(
                request
            );
            if (!(scheduleActionResult.Result is OkObjectResult scheduleOkResult))
            {
                return BadRequest("Failed to retrieve schedule data.");
            }
            var scheduleData = scheduleOkResult.Value as ScheduleResult;
            var yearlyHours = _scheduleTableScraperService.CalculateYearlySubjectHours(
                scheduleData
            );

            // Retrieve absence data to calculate days missed
            var absencesActionResult = await _absenceScraperService.ScrapeAbsences(request);
            if (!(absencesActionResult.Result is OkObjectResult absencesOkResult))
            {
                return BadRequest("Failed to retrieve absences data.");
            }
            AbsencesResult? absencesData = absencesOkResult.Value as AbsencesResult;
            var daysMissed = _absenceScraperService.CalculateDaysMissed(absencesData);

            // Calculate percentages
            var missedPercentages = CalculateMissedPercentages(yearlyHours, daysMissed);

            return Ok(missedPercentages);
        }

        //function to calculate missed class percentages
        private Dictionary<string, string> CalculateMissedPercentages(
            Dictionary<string, int> yearlyHours,
            Dictionary<string, int> daysMissed
        )
        {
            var percentages = new Dictionary<string, string>();

            foreach (var subject in yearlyHours)
            {
                var totalHours = subject.Value;
                var missedDays = daysMissed.ContainsKey(subject.Key) ? daysMissed[subject.Key] : 0;
                var percentageMissed = (double)missedDays / totalHours * 100;
                // Format the result to two decimal places and add a percentage sign, N2 formats the number to two decimal places
                percentages[subject.Key] = $"{percentageMissed:N2}%";
            }

            return percentages;
        }
    }
}
