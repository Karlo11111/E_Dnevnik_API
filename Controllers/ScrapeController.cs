using E_Dnevnik_API.Models.Absences_izostanci;
using E_Dnevnik_API.Models.DifferentGradeLinks;
using E_Dnevnik_API.Models.NewGrades;
using E_Dnevnik_API.Models.NewTests;
using E_Dnevnik_API.Models.ScheduleTable;
using E_Dnevnik_API.Models.ScrapeStudentProfile;
using E_Dnevnik_API.Models.ScrapeSubjects;
using E_Dnevnik_API.Models.ScrapeTests;
using E_Dnevnik_API.Models.SpecificSubject;
using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Mvc;

namespace E_Dnevnik_API.Controllers
{
    // jedini kontroler u projektu - prima http zahtjeve i prosljeđuje ih odgovarajućem servisu
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
        private readonly NewTestsScraperService _newTestsScraperService;

        public ScraperController()
        {
            _subjectScraperService = new ScraperService();
            _testScraperService = new TestScraperService();
            _studentProfileScraperService = new StudentProfileScraperService();
            _differentGradeLinkScraperService = new DifferentGradeLinkScraperService();
            _specificSubjectScraperService = new SpecificSubjectScraperService();
            _absenceScraperService = new AbsenceScraperService();
            _scheduleTableScraperService = new ScheduleTableScraperService();
            _newGradesScraperService = new NewGradesScraperService();
            _newTestsScraperService = new NewTestsScraperService();
        }

        // pomoćna metoda koja validira podatke, poziva servis i hvata ScraperException
        // svi endpointi koji primaju samo email i lozinku prolaze kroz ovu metodu
        private async Task<ActionResult<T>> Execute<T>(
            ScrapeRequest request,
            Func<string, string, Task<T>> action
        )
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                return BadRequest("Email i lozinka moraju biti uneseni.");

            try
            {
                return Ok(await action(request.Email, request.Password));
            }
            catch (ScraperException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }

        [HttpPost("ScrapeSubjectsAndProfessors")]
        public Task<ActionResult<SubjectScrapeResult>> ScrapeSubjects(
            [FromBody] ScrapeRequest request
        ) => Execute(request, _subjectScraperService.ScrapeSubjects);

        [HttpPost("ScrapeSpecificSubjectGrades")]
        public async Task<ActionResult<SubjectDetails>> ScrapeSpecificSubjectGrades(
            [FromBody] ScrapeRequest request,
            [FromQuery] string subjectId
        )
        {
            if (string.IsNullOrEmpty(subjectId))
                return BadRequest("Subject ID mora biti unesen.");

            return await Execute(
                request,
                (email, password) =>
                    _specificSubjectScraperService.ScrapeSubjects(email, password, subjectId)
            );
        }

        [HttpPost("ScrapeStudentProfile")]
        public Task<ActionResult<StudentProfileResult>> ScrapeStudentProfile(
            [FromBody] ScrapeRequest request
        ) => Execute(request, _studentProfileScraperService.ScrapeStudentProfile);

        [HttpPost("ScrapeTests")]
        public Task<ActionResult<Dictionary<string, List<TestInfo>>>> ScrapeTests(
            [FromBody] ScrapeRequest request
        ) => Execute(request, _testScraperService.ScrapeTests);

        [HttpPost("ScrapeDifferentGrades")]
        public Task<ActionResult<List<GradeSubjectDetails>>> ScrapeDifferentGradeLink(
            [FromBody] ScrapeRequest request
        ) => Execute(request, _differentGradeLinkScraperService.ScrapeDifferentGradeLink);

        [HttpPost("ScrapeAbsences")]
        public Task<ActionResult<AbsencesResult>> ScrapeAbsences(
            [FromBody] ScrapeRequest request
        ) => Execute(request, _absenceScraperService.ScrapeAbsences);

        [HttpPost("ScrapeScheduleTable")]
        public Task<ActionResult<ScheduleResult>> ScrapeScheduleTable(
            [FromBody] ScrapeRequest request
        ) => Execute(request, _scheduleTableScraperService.ScrapeScheduleTable);

        [HttpPost("ScrapeNewGrades")]
        public Task<ActionResult<NewGradesResult>> ScrapeNewGrades(
            [FromBody] ScrapeRequest request
        ) => Execute(request, _newGradesScraperService.ScrapeNewGrades);

        [HttpPost("ScrapeNewTests")]
        public Task<ActionResult<NewTestsResult>> ScrapeNewTests(
            [FromBody] ScrapeRequest request
        ) => Execute(request, _newTestsScraperService.ScrapeNewTests);

        // ovaj endpoint kombinira raspored i izostanke pa ne može kroz Execute helper
        [HttpPost("CalculateMissedClassPercentages")]
        public async Task<ActionResult<Dictionary<string, string>>> CalculateMissedClassPercentages(
            [FromBody] ScrapeRequest request
        )
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                return BadRequest("Email i lozinka moraju biti uneseni.");

            try
            {
                var scheduleData = await _scheduleTableScraperService.ScrapeScheduleTable(
                    request.Email,
                    request.Password
                );
                var yearlyHours = _scheduleTableScraperService.CalculateYearlySubjectHours(
                    scheduleData
                );

                var absencesData = await _absenceScraperService.ScrapeAbsences(
                    request.Email,
                    request.Password
                );
                var daysMissed = _absenceScraperService.CalculateDaysMissed(absencesData);

                return Ok(CalculateMissedPercentages(yearlyHours, daysMissed));
            }
            catch (ScraperException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }

        // dijeli broj izostanaka s godišnjim fondom sati i vraća postotak po predmetu
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
                percentages[subject.Key] = $"{percentageMissed:N2}%";
            }

            return percentages;
        }
    }
}
