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
        { _httpFactory = httpFactory; _cfg = cfg; _cache = cache; }

        public async Task<AiPlanDto> GeneratePlanAsync(AiPlanRequestDto request)
        {
            var prompt = request.Prompt?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(prompt))
                return new AiPlanDto { Title = "Describe what you want to build or buy." };

            if (BlockedTerms.Any(t => prompt.ToLowerInvariant().Contains(t)))
                return new AiPlanDto { Title = "That topic isn’t allowed. Try a different DIY idea." };

            var cacheKey = "gemini-plan:" + Sha1(prompt);
            if (_cache.TryGetValue(cacheKey, out AiPlanDto? cached) && cached is not null)
                return cached;

            var apiKey = _cfg["Gemini:ApiKey"];
            var model  = _cfg["Ai:Model"] ?? "gemini-1.5-flash";
            if (string.IsNullOrWhiteSpace(apiKey))
                return new AiPlanDto { Title = "Gemini API key not configured." };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            // Ask Gemini to return STRICT JSON so we can format nicely.
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
                        parts = new[] {
                            new { text = $"You are a DIY assistant.\n{jsonSchema}\nUser request: {prompt}" }
                        }
                    }
                },
                // Tell Gemini to output JSON
                generationConfig = new {
                    responseMimeType = "application/json"
                }
            };

            var client = _httpFactory.CreateClient();
            using var res = await client.PostAsJsonAsync(url, payload);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                return new AiPlanDto { Title = $"AI error: {res.StatusCode}", Steps = new(){ err } };
            }

            var root = await res.Content.ReadFromJsonAsync<JsonElement>();
            // candidates[0].content.parts[0].text contains the JSON string
            var planJson =
                root.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0 &&
                cands[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textEl)
                    ? textEl.GetString()
                    : null;

            AiPlanDto plan;
            try
            {
                plan = planJson is null
                    ? new AiPlanDto { Title = "No content returned." }
                    : JsonSerializer.Deserialize<AiPlanDto>(planJson!, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new AiPlanDto { Title = "Could not parse plan." };
            }
            catch
            {
                plan = new AiPlanDto { Title = "Plan parsing error.", Steps = new(){ planJson ?? "" } };
            }

            // Cache for 2 hours
            _cache.Set(cacheKey, plan, TimeSpan.FromHours(2));
            return plan;
        }

        private static string Sha1(string input)
        {
            using var sha = SHA1.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(input)))
                .Replace("-", "").ToLowerInvariant();
        }
    }
}
