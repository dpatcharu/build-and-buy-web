using BuildAndBuy.Web.Models;
using BuildAndBuy.Web.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace BuildAndBuy.Web.Controllers
{
    public class AiController : Controller
    {
        private readonly IAiService _aiService;

        public AiController(IAiService aiService)
        {
            _aiService = aiService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(AiRequestDto model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var response = await _aiService.GetAiResponseAsync(model);
            ViewBag.AiResult = response.Result;
            return View();
        }

        [HttpGet]
        public IActionResult Plan()
        {
            return View();
        }
    }
}
