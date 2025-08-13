using BuildAndBuy.Web.Models;

namespace BuildAndBuy.Web.Services.Abstractions
{
    public interface IAiService
    {
        Task<AiPlanDto> GeneratePlanAsync(AiPlanRequestDto request);
    }
}
