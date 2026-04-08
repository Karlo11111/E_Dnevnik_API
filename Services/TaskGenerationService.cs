using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E_Dnevnik_API.Services
{
    public class TaskGenerationService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly ILogger<TaskGenerationService> _logger;

        public TaskGenerationService(HttpClient http, IConfiguration config, ILogger<TaskGenerationService> logger)
        {
            _http = http;
            _apiKey = config["OpenAI:ApiKey"] ?? "";
            _logger = logger;
        }

        public async Task<List<string>> GenerateTasks(
            string subjectName,
            decimal newAverage,
            decimal previousAverage,
            int count = 7)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("[TaskGen] OpenAI:ApiKey not configured, returning empty task list.");
                return new List<string>();
            }

            var prompt = $"""
                Ti si pomoćnik za učenje za hrvatske učenike srednje škole.
                Učeniku je iz predmeta "{subjectName}" pao prosjek s {previousAverage:F1} na {newAverage:F1}.
                Generiraj {count} konkretnih zadataka za vježbu koji će mu pomoći da bolje razumije gradivo i popravi ocjenu.
                Zadaci trebaju biti na hrvatskom jeziku, prilagođeni razini srednje škole.
                Formatiraj kao numeriranu listu. Samo zadaci, bez rješenja.
                """;

            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = "Ti si iskusni nastavnik koji pomaže učenicima srednje škole." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 1000,
                temperature = 0.7
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                var content = json["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";

                return content
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.Trim().Length > 0)
                    .Select(line => line.Trim())
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TaskGen] OpenAI request failed for subject {Subject}", subjectName);
                return new List<string>();
            }
        }
    }
}
