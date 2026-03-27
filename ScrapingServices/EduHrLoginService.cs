using System.Net;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;

namespace E_Dnevnik_API.ScrapingServices
{
    public record LoginResult(HttpClient? Client, int StatusCode, string Error);

    public static class EduHrLoginService
    {
        private const string LoginUrl = "https://ocjene.skole.hr/login";

        /// <summary>
        /// Authenticates with e-Dnevnik and returns an HttpClient with a valid session.
        /// The caller is responsible for disposing the client.
        /// Returns a null Client on failure, with StatusCode and Error set.
        /// </summary>
        public static async Task<LoginResult> LoginAsync(string email, string password)
        {
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
            };

            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Clear();

            var loginPageResponse = await client.GetAsync(LoginUrl);
            var loginPageContent = await loginPageResponse.Content.ReadAsStringAsync();

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(loginPageContent);
            var csrfToken = htmlDoc.DocumentNode
                .SelectSingleNode("//input[@name='csrf_token']")
                ?.Attributes["value"]?.Value;

            if (string.IsNullOrEmpty(csrfToken))
            {
                client.Dispose();
                return new LoginResult(null, StatusCodes.Status500InternalServerError, "CSRF token not found.");
            }

            var formData = new Dictionary<string, string>
            {
                ["username"] = email,
                ["password"] = password,
                ["csrf_token"] = csrfToken,
            };

            var loginContent = new FormUrlEncodedContent(formData);
            client.DefaultRequestHeaders.Referrer = new Uri(LoginUrl);
            var loginResponse = await client.PostAsync(LoginUrl, loginContent);

            if (!loginResponse.IsSuccessStatusCode)
            {
                client.Dispose();
                return new LoginResult(null, (int)loginResponse.StatusCode, "Failed to log in.");
            }

            return new LoginResult(client, StatusCodes.Status200OK, string.Empty);
        }
    }
}
