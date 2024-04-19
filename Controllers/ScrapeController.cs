﻿using HtmlAgilityPack;
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
using System.Runtime.CompilerServices;
using E_Dnevnik_API.Models.Absences_izostanci;
using E_Dnevnik_API.Models.ScheduleTable;

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
        private readonly AbsenceScraperService _absenceScraperService;
        private readonly ScheduleTableScraperService _scheduleTableScraperService;


        public ScraperController(IHttpClientFactory httpClientFactory)
        {
            _subjectScraperService = new ScraperService(httpClientFactory);
            _testScraperService = new TestScraperService(httpClientFactory);
            _studentProfileScraperService = new StudentProfileScraperService(httpClientFactory);
            _differentGradeLinkScraperService = new DifferentGradeLinkScraperService(httpClientFactory);
            _absenceScraperService = new AbsenceScraperService(httpClientFactory);
            _scheduleTableScraperService = new ScheduleTableScraperService(httpClientFactory);
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
        [HttpPost("ScrapeAbsences")]
        public async Task<ActionResult<AbsencesResult>> ActionResult([FromBody] ScrapeRequest request)
        {
            // Call the service method that returns AbsencesResult
            var actionResult = await _absenceScraperService.ScrapeAbsences(request);

            // Return the result
            return actionResult;
        }
        [HttpPost("ScrapeScheduleTable")]
        public async Task<ActionResult<ScheduleResult>> ScrapeScheduleTable([FromBody] ScrapeRequest request)
        {
            // Call the service method that returns ScheduleResult
            var actionResult = await _scheduleTableScraperService.ScrapeScheduleTable(request);

            // Return the result
            return actionResult;
        }

        [HttpPost("CalculateMissedClassPercentages")]
        public async Task<ActionResult<Dictionary<string, double>>> CalculateMissedClassPercentages([FromBody] ScrapeRequest request)
        {
            // Retrieve schedule data to get total yearly hours per subject
            var scheduleActionResult = await _scheduleTableScraperService.ScrapeScheduleTable(request);
            if (!(scheduleActionResult.Result is OkObjectResult scheduleOkResult))
            {
                return BadRequest("Failed to retrieve schedule data.");
            }
            var scheduleData = (ScheduleResult)scheduleOkResult.Value;
            var yearlyHours = _scheduleTableScraperService.CalculateYearlySubjectHours(scheduleData);

            // Retrieve absence data to calculate days missed
            var absencesActionResult = await _absenceScraperService.ScrapeAbsences(request);
            if (!(absencesActionResult.Result is OkObjectResult absencesOkResult))
            {
                return BadRequest("Failed to retrieve absences data.");
            }
            var absencesData = (AbsencesResult)absencesOkResult.Value;
            var daysMissed = _absenceScraperService.CalculateDaysMissed(absencesData);

            // Calculate percentages
            var missedPercentages = CalculateMissedPercentages(yearlyHours, daysMissed);

            return Ok(missedPercentages);
        }

        private Dictionary<string, string> CalculateMissedPercentages(Dictionary<string, int> yearlyHours, Dictionary<string, int> daysMissed)
        {
            var percentages = new Dictionary<string, string>();

            foreach (var subject in yearlyHours)
            {
                var totalHours = subject.Value;
                var missedDays = daysMissed.ContainsKey(subject.Key) ? daysMissed[subject.Key] : 0;
                var percentageMissed = (double)missedDays / totalHours * 100;
                // Format the result to two decimal places and add a percentage sign
                percentages[subject.Key] = $"{percentageMissed:N2}%";  // N2 formats the number to two decimal places
            }

            return percentages;
        }
    }

}