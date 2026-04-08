using E_Dnevnik_API.Database;
using E_Dnevnik_API.Database.Models;
using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Mvc;

namespace E_Dnevnik_API.Controllers
{
    [Route("api/[controller]")]
    public class DeviceController : ApiBaseController
    {
        private readonly AppDbContext _db;

        public DeviceController(SessionStore sessionStore, AppDbContext db) : base(sessionStore)
        {
            _db = db;
        }

        // Flutter calls this right after login and on FirebaseMessaging.instance.onTokenRefresh.
        // Stores FCM token in Postgres — backend reads from here when sending push notifications.
        [HttpPost("RegisterToken")]
        public async Task<IActionResult> RegisterToken([FromBody] RegisterTokenDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FcmToken))
                return BadRequest(new { error = "FcmToken je obavezan." });

            var email = TryGetEmail();
            if (email is null)
                return Unauthorized("Sesija je istekla ili token nije valjan. Potrebna je ponovna prijava.");

            var cache = await _db.StudentCache.FindAsync(email);
            if (cache == null)
            {
                cache = new StudentCache { Email = email };
                _db.StudentCache.Add(cache);
            }
            cache.FcmToken = dto.FcmToken;
            cache.FcmTokenUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok();
        }
    }

    public record RegisterTokenDto(string FcmToken);
}
