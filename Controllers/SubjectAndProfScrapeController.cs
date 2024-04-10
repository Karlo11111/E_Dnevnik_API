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

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScraperController : ControllerBase
    {
        private readonly ScraperService _scraperService;

        public ScraperController(IHttpClientFactory httpClientFactory)
        {
            _scraperService = new ScraperService(httpClientFactory);
        }

        [HttpPost("ScrapeSubjectsAndProfessors")]
        public async Task<ActionResult<ScrapeResult>> ScrapeSubjects([FromBody] ScrapeRequest request)
        {
            // Call the service method that returns ScrapeResult
            var actionResult = await _scraperService.ScrapeSubjects(request);

            // Return the result
            return actionResult;
        }
    }



}