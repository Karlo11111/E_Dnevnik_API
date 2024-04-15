using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net;
using E_Dnevnik_API.ScrapingServices;
using E_Dnevnik_API.Models.ScrapeSubjects;
using E_Dnevnik_API.Models.ScrapeTests;
using E_Dnevnik_API.Models.ScrapeStudentProfile;
using E_Dnevnik_API.Models.DifferentGradeLinks;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScraperController : ControllerBase
    {
        private readonly ScraperService _subjectScraperService;
        private readonly TestScraperService _testScraperService;
        private readonly StudentProfileScraperService _studentProfileScraperService;
        private readonly DifferentGradeLinkScraperService _differentGradeLinkScraperService;


        public ScraperController(IHttpClientFactory httpClientFactory)
        {
            _subjectScraperService = new ScraperService(httpClientFactory);
            _testScraperService = new TestScraperService(httpClientFactory);
            _studentProfileScraperService = new StudentProfileScraperService(httpClientFactory);
            _differentGradeLinkScraperService = new DifferentGradeLinkScraperService(httpClientFactory);
        }

        [HttpPost("ScrapeSubjectsAndProfessors")]
        public async Task<ActionResult<SubjectScrapeResult>> ScrapeSubjects([FromBody] ScrapeRequest request)
        {
            // Call the service method that returns ScrapeResult
            var actionResult = await _subjectScraperService.ScrapeSubjects(request);

            // Return the result
            return actionResult;
        }

        [HttpPost("ScrapeStudentProfile")]
        public async Task<ActionResult<StudentProfileResult>> ScrapeStudentProfile([FromBody] ScrapeRequest request)
        {
            // Call the service method that returns StudentProfileResult
            var actionResult = await _studentProfileScraperService.ScrapeStudentProfile(request);

            // Return the result
            return actionResult;
        }

        [HttpPost("ScrapeTests")]
        public async Task<ActionResult<TestResult>> ScrapeTests([FromBody] ScrapeRequest request)
        {
            // Call the service method that returns TestResult
            var actionResult = await _testScraperService.ScrapeTests(request);

            // Return the result
            return actionResult;
        }

        [HttpPost("ScrapeDifferentGrades")]
        public async Task<ActionResult<List<GradeSubjectDetails>>> ScrapeDifferentGradeLink([FromBody] ScrapeRequest request)
        {
            // Call the service method that returns List<GradeSubjectDetails>
            var actionResult = await _differentGradeLinkScraperService.ScrapeDifferentGradeLink(request);

            // Return the result
            return actionResult;
        }
    }



}