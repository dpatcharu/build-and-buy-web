using BuildAndBuy.Web.Models;
using BuildAndBuy.Web.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace BuildAndBuy.Web.Controllers
{
    public class AiController : Controller
    {
        private readonly IAiService _ai;
        public AiController(IAiService ai) { _ai = ai; }

        [HttpGet]
        public IActionResult Index() => View(new AiPlanRequestDto());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(AiPlanRequestDto model)
        {
            var plan = await _ai.GeneratePlanAsync(model);
            plan.OriginalPrompt = model.Prompt;   // keep for Regenerate
            return View("Plan", plan);
        }


        // Optional direct route to Plan view
        [HttpGet]
        public IActionResult Plan() => View(new AiPlanDto { Title = "Your Plan will appear here." });
    }
}
