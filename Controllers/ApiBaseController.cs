using E_Dnevnik_API.ScrapingServices;
using Microsoft.AspNetCore.Mvc;

namespace E_Dnevnik_API.Controllers
{
    [ApiController]
    public abstract class ApiBaseController : ControllerBase
    {
        protected readonly SessionStore SessionStore;

        protected ApiBaseController(SessionStore sessionStore)
        {
            SessionStore = sessionStore;
        }

        // Extracts Bearer token from Authorization header, null if missing/malformed
        protected string? GetBearerToken()
        {
            var authHeader = Request.Headers.Authorization.ToString();
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return null;
            return authHeader["Bearer ".Length..].Trim();
        }

        // Returns email for authenticated request, null if token missing or session expired
        protected string? TryGetEmail()
        {
            var token = GetBearerToken();
            return token is null ? null : SessionStore.GetEmailByToken(token);
        }
    }
}
