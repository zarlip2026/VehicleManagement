using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;

namespace VehicleManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger) => _logger = logger;

        [IgnoreAntiforgeryToken]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var failure = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (failure == null) return NotFound();
            _logger.LogError(failure.Error, "Request failed. Reference {RequestId}", HttpContext.TraceIdentifier);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return View("Error", HttpContext.TraceIdentifier);
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
