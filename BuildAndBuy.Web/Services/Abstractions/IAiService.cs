using BuildAndBuy.Web.Models;

namespace BuildAndBuy.Web.Services.Abstractions
{
    public interface IAiService
    {
        Task<AiResponseDto> GetAiResponseAsync(AiRequestDto request);
    }
}
