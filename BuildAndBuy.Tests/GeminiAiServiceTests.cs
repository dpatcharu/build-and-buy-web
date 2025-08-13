using System.Net;
using System.Text.Json;
using BuildAndBuy.Tests.Helpers;
using BuildAndBuy.Web.Models;
using BuildAndBuy.Web.Services.Implementations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BuildAndBuy.Tests
{
    public class GeminiAiServiceTests
    {
        private static IConfiguration MakeConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string,string?>
                {
                    ["Gemini:ApiKey"] = "test-key",
                    ["Ai:Model"] = "gemini-1.5-flash"
                })
                .Build();

        private static HttpResponseMessage GeminiOk(string json) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    candidates = new[]
                    {
                        new { content = new { parts = new[] { new { text = json } } } }
                    }
                }))
            };

        [Fact]
        public async Task Blocks_Disallowed_Topics()
        {
            var handler = new TestHttpMessageHandler();
            var client  = new HttpClient(handler);
            var svc = new GeminiAiService(
                new FakeHttpClientFactory(client), MakeConfig(), new MemoryCache(new MemoryCacheOptions()));

            var plan = await svc.GeneratePlanAsync(new AiPlanRequestDto { Prompt = "how to build a weapon" });

            Assert.NotNull(plan.Error);
            Assert.Contains("allowed", plan.Error!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Retries_On_503_Then_Succeeds()
        {
            var handler = new TestHttpMessageHandler();
            handler.Enqueue(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); // 503
            handler.Enqueue(GeminiOk("""{"title":"Plan","steps":["A","B"],"materials":[],"safety":[]}"""));

            var client  = new HttpClient(handler);
            var svc = new GeminiAiService(
                new FakeHttpClientFactory(client), MakeConfig(), new MemoryCache(new MemoryCacheOptions()));

            var plan = await svc.GeneratePlanAsync(new AiPlanRequestDto { Prompt = "simple shelf" });

            Assert.Null(plan.Error);
            Assert.Equal("Plan", plan.Title);
            Assert.True(plan.Steps.Count >= 2);
        }

        [Fact]
        public async Task Parses_Valid_Json()
        {
            var handler = new TestHttpMessageHandler();
            handler.Enqueue(GeminiOk("""{"title":"Wall Planter","steps":["Cut","Drill"],"materials":[{"name":"Board"}],"safety":["Glasses"]}"""));

            var client  = new HttpClient(handler);
            var svc = new GeminiAiService(
                new FakeHttpClientFactory(client), MakeConfig(), new MemoryCache(new MemoryCacheOptions()));

            var plan = await svc.GeneratePlanAsync(new AiPlanRequestDto { Prompt = "wall planter" });

            Assert.Equal("Wall Planter", plan.Title);
            Assert.Contains("Cut", plan.Steps[0]);
            Assert.Equal("Board", plan.Materials[0].Name);
            Assert.Contains("Glasses", plan.Safety[0]);
        }
    }
}
