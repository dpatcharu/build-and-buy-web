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
        public IActionResult Index() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(AiRequestDto model)
        {
            var response = await _ai.GetAiResponseAsync(model);
            ViewBag.AiResult = response.Result;
            return View();
        }
    }
}
