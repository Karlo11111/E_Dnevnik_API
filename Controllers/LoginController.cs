using E_Dnevnik_API.Models.ScrapeSubjects;
using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Mvc;

namespace E_Dnevnik_API.Controllers
{
    // prima email i lozinku, prijavljuje se na e-dnevnik i vraća token za daljnje zahtjeve
    // lozinka se nikad ne čuva - samo kolačići aktivne sesije
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly SessionStore _sessionStore;
        private readonly LoginBruteForceProtection _bruteForce;
        private readonly ILogger<LoginController> _logger;

        public LoginController(
            SessionStore sessionStore,
            LoginBruteForceProtection bruteForce,
            ILogger<LoginController> logger
        )
        {
            _sessionStore = sessionStore;
            _bruteForce = bruteForce;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult> Login([FromBody] ScrapeRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                return BadRequest("Email i lozinka moraju biti uneseni.");

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // provjera jeli email privremeno blokiran zbog previše neuspjelih pokušaja
            if (_bruteForce.IsBlocked(request.Email))
            {
                _logger.LogWarning(
                    "login blokiran (brute force) | email={Email} ip={Ip}",
                    request.Email,
                    ip
                );
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    "previše neuspjelih pokušaja prijave. pokušaj ponovo za 15 minuta."
                );
            }

            var result = await EduHrLoginService.LoginAsync(request.Email, request.Password);

            if (result.Client is null)
            {
                _bruteForce.RecordFailure(request.Email);
                _logger.LogWarning(
                    "login neuspješan | email={Email} ip={Ip} status={Status}",
                    request.Email,
                    ip,
                    result.StatusCode
                );
                return StatusCode(result.StatusCode, result.Error);
            }

            _bruteForce.RecordSuccess(request.Email);
            _logger.LogInformation("login uspješan | email={Email} ip={Ip}", request.Email, ip);

            // cookije smo dobili, klijent za login nam više ne treba
            result.Client.Dispose();

            var token = _sessionStore.CreateSession(result.Cookies!);
            return Ok(new { token });
        }

        // odjava - briše sesiju s poslužitelja odmah, token prestaje vrijediti
        [HttpDelete]
        public ActionResult Logout()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Unauthorized("Authorization header s Bearer tokenom je obavezan.");

            var token = authHeader["Bearer ".Length..].Trim();
            _sessionStore.RemoveSession(token);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogInformation("odjava | ip={Ip}", ip);

            return Ok();
        }
    }
}
