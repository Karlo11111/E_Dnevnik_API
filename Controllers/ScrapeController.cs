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

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScraperController : ControllerBase
    {
        private readonly ScraperService _scraperService;
        private readonly TestScraperService _testScraperService;


        public ScraperController(IHttpClientFactory httpClientFactory)
        {
            _scraperService = new ScraperService(httpClientFactory);
            _testScraperService = new TestScraperService(httpClientFactory);
        }

        [HttpPost("ScrapeSubjectsAndProfessors")]
        public async Task<ActionResult<ScrapeResult>> ScrapeSubjects([FromBody] ScrapeRequest request)
        {
            // Call the service method that returns ScrapeResult
            var actionResult = await _scraperService.ScrapeSubjects(request);

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
    }



}