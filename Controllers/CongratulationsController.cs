using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    /// <summary>Birthday and anniversary cards (/Congratulations/Index).</summary>
    public class CongratulationsController : Controller
    {
        [HttpGet("Congratulations/Index")]
        [HttpGet("Congratulations")]
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("IsAdminLoggedIn") != "true")
                return RedirectToAction("Index", "Home");

            return View("~/Views/Congratulations/Index.cshtml");
        }
    }
}
