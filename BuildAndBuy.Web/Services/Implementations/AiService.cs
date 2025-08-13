using BuildAndBuy.Web.Models;
using BuildAndBuy.Web.Services.Abstractions;

namespace BuildAndBuy.Web.Services.Implementations
{
    public class AiService : IAiService
    {
        public async Task<AiResponseDto> GetAiResponseAsync(AiRequestDto request)
        {
            // TODO: Replace with actual AI API call
            await Task.Delay(500); // Simulate API latency
            return new AiResponseDto
            {
                Result = $"AI Suggestion based on: {request.Prompt}"
            };
        }
    }
}
