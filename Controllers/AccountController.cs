using E_Dnevnik_API.Database;
using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Mvc;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    public class AccountController : ApiBaseController
    {
        private readonly AppDbContext _db;

        public AccountController(SessionStore sessionStore, AppDbContext db)
            : base(sessionStore)
        {
            _db = db;
        }

        // Flutter calls this on startup and after any purchase to check subscription status.
        [HttpGet("Status")]
        public async Task<IActionResult> Status()
        {
            var email = TryGetEmail();
            if (email is null)
                return Unauthorized(
                    "Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava."
                );

            var cache = await _db.StudentCache.FindAsync(email);
            return Ok(
                new
                {
                    isOdlikasPlus = cache?.IsOdlikasPlus ?? false,
                    odlikasPlusSince = cache?.OdlikasPlusSince,
                }
            );
        }
    }
}
