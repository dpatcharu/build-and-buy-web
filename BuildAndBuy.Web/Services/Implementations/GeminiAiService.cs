using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildAndBuy.Web.Models;
using BuildAndBuy.Web.Services.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace BuildAndBuy.Web.Services.Implementations
{
    public class GeminiAiService : IAiService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _cfg;
        private readonly IMemoryCache _cache;

        private static readonly string[] BlockedTerms =
            { "weapon","bomb","explosive","lockpick","drug synthesis","bypass","break in" };

        public GeminiAiService(IHttpClientFactory httpFactory, IConfiguration cfg, IMemoryCache cache)
        {
            _httpFactory = httpFactory;
            _cfg = cfg;
            _cache = cache;
        }

        public async Task<AiPlanDto> GeneratePlanAsync(AiPlanRequestDto request)
        {
            var prompt = request.Prompt?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(prompt))
                return new AiPlanDto { Title = "Describe what you want to build or buy.", Error = "Please enter a short description." };

            if (BlockedTerms.Any(t => prompt.ToLowerInvariant().Contains(t)))
                return new AiPlanDto { Title = "Blocked topic", Error = "That topic isn’t allowed. Try a different DIY idea." };

            var cacheKey = "gemini-plan:" + Sha1(prompt);
            if (_cache.TryGetValue(cacheKey, out AiPlanDto? cached) && cached is not null && string.IsNullOrEmpty(cached.Error))
                return cached;

            var apiKey = _cfg["Gemini:ApiKey"];
            var model  = _cfg["Ai:Model"] ?? "gemini-1.5-flash";
            if (string.IsNullOrWhiteSpace(apiKey))
                return new AiPlanDto { Title = "Missing API key", Error = "Gemini API key is not configured on the server." };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var jsonSchema = """
            Return ONLY JSON (no markdown) with this shape:
            {
              "title": "string",
              "difficulty": 1,
              "timeMinutes": 60,
              "budgetNote": "Under $25",
              "steps": ["...","..."],
              "materials": [{"name":"..","specs":"..","link":""}],
              "safety": ["..",".."]
            }
            """;

            var payload = new
            {
                contents = new[]
                {
                    new {
                        role = "user",
                        parts = new[] { new { text = $"You are a DIY assistant.\n{jsonSchema}\nUser request: {prompt}" } }
                    }
                },
                generationConfig = new { responseMimeType = "application/json" }
            };

            try
            {
                var planJson = await PostWithRetryAsync(url, payload);
                if (planJson is null)
                    return new AiPlanDto
                    {
                        Title = "AI is busy",
                        Error = "Our AI is temporarily at capacity. Please try again in a moment."
                    };

                AiPlanDto plan;
                try
                {
                    plan = JsonSerializer.Deserialize<AiPlanDto>(planJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new AiPlanDto { Title = "Could not parse plan.", Error = "The AI response could not be parsed." };
                }
                catch
                {
                    plan = new AiPlanDto { Title = "Plan parsing error.", Error = "The AI response format was unexpected." };
                }

                _cache.Set(cacheKey, plan, TimeSpan.FromHours(2));
                return plan;
            }
            catch (Exception)
            {
                return new AiPlanDto
                {
                    Title = "AI is unavailable",
                    Error = "We couldn’t reach the AI service. Please try again shortly."
                };
            }
        }

        /// <summary>
        /// Posts with retry/backoff; returns the JSON text from Gemini’s parts[0].text or null on final failure.
        /// </summary>
        private async Task<string?> PostWithRetryAsync(string url, object payload)
        {
            var client = _httpFactory.CreateClient();

            int maxAttempts = 4;             // total tries
            int baseDelayMs = 800;           // initial backoff
            var rand = new Random();

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var res = await client.PostAsJsonAsync(url, payload);

                if (res.IsSuccessStatusCode)
                {
                    var root = await res.Content.ReadFromJsonAsync<JsonElement>();
                    var text =
                        root.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0 &&
                        cands[0].TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0 &&
                        parts[0].TryGetProperty("text", out var textEl)
                            ? textEl.GetString()
                            : null;
                    return text;
                }

                // Retry on transient status codes
                if (IsTransient(res.StatusCode) && attempt < maxAttempts)
                {
                    // small jitter to avoid thundering herd
                    int delay = baseDelayMs * (int)Math.Pow(2, attempt - 1) + rand.Next(0, 250);
                    await Task.Delay(delay);
                    continue;
                }

                // If not transient (or out of attempts), surface a friendly failure
                if (attempt >= maxAttempts)
                    return null;
            }

            return null;
        }

        private static bool IsTransient(HttpStatusCode code) =>
            code == HttpStatusCode.TooManyRequests || // 429
            code == HttpStatusCode.InternalServerError ||     // 500
            code == HttpStatusCode.BadGateway ||              // 502
            code == HttpStatusCode.ServiceUnavailable ||      // 503
            code == HttpStatusCode.GatewayTimeout;            // 504

        private static string Sha1(string input)
        {
            using var sha = SHA1.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(input)))
                .Replace("-", "").ToLowerInvariant();
        }
    }
}
