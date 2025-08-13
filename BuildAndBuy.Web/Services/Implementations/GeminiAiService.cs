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

        // very basic guardrail list for MVP
        private static readonly string[] BlockedTerms =
            { "weapon","bomb","explosive","lockpick","drug synthesis","bypass","break in" };

        public GeminiAiService(IHttpClientFactory httpFactory, IConfiguration cfg, IMemoryCache cache)
        {
            _httpFactory = httpFactory;
            _cfg = cfg;
            _cache = cache;
        }

        public async Task<AiResponseDto> GetAiResponseAsync(AiRequestDto request)
        {
            var prompt = request.Prompt?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(prompt))
                return new AiResponseDto { Result = "Please describe what you want to build or buy." };

            if (BlockedTerms.Any(t => prompt.ToLowerInvariant().Contains(t)))
                return new AiResponseDto { Result = "That topic isn’t allowed. Try a different DIY idea." };

            // cache to protect free quota
            var key = "gemini:" + Sha1(prompt);
            if (_cache.TryGetValue(key, out string? cached) && !string.IsNullOrEmpty(cached))
                return new AiResponseDto { Result = cached };

            var apiKey = _cfg["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return new AiResponseDto { Result = "Gemini API key not configured." };

            var model = _cfg["Ai:Model"] ?? "gemini-1.5-flash";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var systemInstruction =
                "You are a helpful DIY assistant. Return a concise plan: a title, 4-7 numbered steps, " +
                "a short materials list, and 1-2 safety tips. Keep it beginner-friendly.";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = $"{systemInstruction}\n\nUser request: {prompt}" } }
                    }
                }
            };

            var client = _httpFactory.CreateClient();
            using var res = await client.PostAsJsonAsync(url, payload);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                return new AiResponseDto { Result = $"AI error: {res.StatusCode}. {err}" };
            }

            var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
            // candidates[0].content.parts[0].text
            string result =
                doc.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0 &&
                cands[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textEl)
                    ? textEl.GetString() ?? "No content returned."
                    : "No content returned.";

            _cache.Set(key, result, TimeSpan.FromHours(2));
            return new AiResponseDto { Result = result };
        }

        private static string Sha1(string input)
        {
            using var sha = SHA1.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(input)))
                   .Replace("-", "").ToLowerInvariant();
        }
    }
}
