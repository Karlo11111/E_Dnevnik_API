using System.Net;
using E_Dnevnik_API.Models.Absences_izostanci;
using E_Dnevnik_API.Models.DifferentGradeLinks;
using E_Dnevnik_API.Models.NewGrades;
using E_Dnevnik_API.Models.NewTests;
using E_Dnevnik_API.Models.ScheduleTable;
using E_Dnevnik_API.Models.ScrapeStudentProfile;
using E_Dnevnik_API.Models.ScrapeTests;
using E_Dnevnik_API.Models.SpecificSubject;
using E_Dnevnik_API.Models.ScrapeSubjects;
using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace E_Dnevnik_API.Controllers
{
    // prima zahtjeve s Bearer tokenom, dohvaća sesiju iz SessionStore i poziva odgovarajući servis
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
        private readonly SessionStore _sessionStore;
        private readonly IMemoryCache _cache;

        public ScraperController(
            ScraperService subjectScraperService,
            SpecificSubjectScraperService specificSubjectScraperService,
            TestScraperService testScraperService,
            StudentProfileScraperService studentProfileScraperService,
            DifferentGradeLinkScraperService differentGradeLinkScraperService,
            AbsenceScraperService absenceScraperService,
            ScheduleTableScraperService scheduleTableScraperService,
            NewGradesScraperService newGradesScraperService,
            NewTestsScraperService newTestsScraperService,
            SessionStore sessionStore,
            IMemoryCache cache
        )
        {
            _subjectScraperService = subjectScraperService;
            _specificSubjectScraperService = specificSubjectScraperService;
            _testScraperService = testScraperService;
            _studentProfileScraperService = studentProfileScraperService;
            _differentGradeLinkScraperService = differentGradeLinkScraperService;
            _absenceScraperService = absenceScraperService;
            _scheduleTableScraperService = scheduleTableScraperService;
            _newGradesScraperService = newGradesScraperService;
            _newTestsScraperService = newTestsScraperService;
            _sessionStore = sessionStore;
            _cache = cache;
        }

        // rješava token iz headera, kreira klijent s cookijima iz sesije i poziva servis
        // svi endpointi prolaze kroz ovu metodu osim CalculateMissedClassPercentages koji treba dva servisa
        private async Task<ActionResult<T>> Execute<T>(Func<HttpClient, Task<T>> action)
        {
            var token = GetBearerToken();
            if (token is null)
                return Unauthorized("Authorization header s Bearer tokenom je obavezan.");

            var cookies = _sessionStore.GetCookies(token);
            if (cookies is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            using var client = CreateClient(cookies);
            try
            {
                return Ok(await action(client));
            }
            catch (ScraperException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }

        // vadi Bearer token iz Authorization headera, vraća null ako header ne postoji ili je krivi format
        private string? GetBearerToken()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return null;
            return authHeader["Bearer ".Length..].Trim();
        }

        // svaki zahtjev dobiva novi HttpClient s istim cookijima - čisto, bez dijeljenja stanja između zahtjeva
        private static HttpClient CreateClient(CookieContainer cookies)
        {
            var handler = new HttpClientHandler { UseCookies = true, CookieContainer = cookies };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
        }

        [HttpGet("ScrapeSubjectsAndProfessors")]
        public Task<ActionResult<SubjectScrapeResult>> ScrapeSubjects()
            => Execute(client => _subjectScraperService.ScrapeSubjects(client));

        [HttpGet("ScrapeSpecificSubjectGrades")]
        public async Task<ActionResult<SubjectDetails>> ScrapeSpecificSubjectGrades([FromQuery] string subjectId)
        {
            if (string.IsNullOrEmpty(subjectId))
                return BadRequest("Subject ID mora biti unesen.");

            if (!subjectId.All(char.IsDigit))
                return BadRequest("Subject ID mora biti broj.");

            return await Execute(client => _specificSubjectScraperService.ScrapeSubjects(client, subjectId));
        }

        [HttpGet("ScrapeStudentProfile")]
        public Task<ActionResult<StudentProfileResult>> ScrapeStudentProfile()
            => Execute(client => _studentProfileScraperService.ScrapeStudentProfile(client));

        [HttpGet("ScrapeTests")]
        public Task<ActionResult<Dictionary<string, List<TestInfo>>>> ScrapeTests()
            => Execute(client => _testScraperService.ScrapeTests(client));

        [HttpGet("ScrapeDifferentGrades")]
        public Task<ActionResult<List<GradeSubjectDetails>>> ScrapeDifferentGradeLink()
            => Execute(client => _differentGradeLinkScraperService.ScrapeDifferentGradeLink(client));

        [HttpGet("ScrapeAbsences")]
        public Task<ActionResult<AbsencesResult>> ScrapeAbsences()
            => Execute(client => _absenceScraperService.ScrapeAbsences(client));

        [HttpGet("ScrapeScheduleTable")]
        public Task<ActionResult<ScheduleResult>> ScrapeScheduleTable()
            => Execute(client => _scheduleTableScraperService.ScrapeScheduleTable(client));

        [HttpGet("ScrapeNewGrades")]
        public async Task<ActionResult<NewGradesResult>> ScrapeNewGrades()
        {
            var token = GetBearerToken();
            if (token is null)
                return Unauthorized("Authorization header s Bearer tokenom je obavezan.");

            var cookies = _sessionStore.GetCookies(token);
            if (cookies is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            if (_cache.TryGetValue<NewGradesResult>($"newgrades:{token}", out var cached) && cached is not null)
                return Ok(cached);

            using var client = CreateClient(cookies);
            try
            {
                var result = await _newGradesScraperService.ScrapeNewGrades(client);
                _cache.Set($"newgrades:{token}", result, TimeSpan.FromMinutes(10));
                return Ok(result);
            }
            catch (ScraperException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }

        [HttpGet("ScrapeNewTests")]
        public async Task<ActionResult<NewTestsResult>> ScrapeNewTests()
        {
            var token = GetBearerToken();
            if (token is null)
                return Unauthorized("Authorization header s Bearer tokenom je obavezan.");

            var cookies = _sessionStore.GetCookies(token);
            if (cookies is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            if (_cache.TryGetValue<NewTestsResult>($"newtests:{token}", out var cached) && cached is not null)
                return Ok(cached);

            using var client = CreateClient(cookies);
            try
            {
                var result = await _newTestsScraperService.ScrapeNewTests(client);
                _cache.Set($"newtests:{token}", result, TimeSpan.FromMinutes(10));
                return Ok(result);
            }
            catch (ScraperException ex)
            {
                return StatusCode(ex.StatusCode, ex.Message);
            }
        }

        // ovaj endpoint kombinira raspored i izostanke pa ih pozivamo s istim klijentom
        [HttpGet("CalculateMissedClassPercentages")]
        public async Task<ActionResult<Dictionary<string, string>>> CalculateMissedClassPercentages()
        {
            var token = GetBearerToken();
            if (token is null)
                return Unauthorized("Authorization header s Bearer tokenom je obavezan.");

            var cookies = _sessionStore.GetCookies(token);
            if (cookies is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            using var client = CreateClient(cookies);
            try
            {
                var scheduleData = await _scheduleTableScraperService.ScrapeScheduleTable(client);
                var yearlyHours = _scheduleTableScraperService.CalculateYearlySubjectHours(scheduleData);

                var absencesData = await _absenceScraperService.ScrapeAbsences(client);
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
                if (totalHours == 0) { percentages[subject.Key] = "0.00%"; continue; }
                var missedDays = daysMissed.ContainsKey(subject.Key) ? daysMissed[subject.Key] : 0;
                var percentageMissed = (double)missedDays / totalHours * 100;
                percentages[subject.Key] = $"{percentageMissed:N2}%";
            }

            return percentages;
        }
    }
}
