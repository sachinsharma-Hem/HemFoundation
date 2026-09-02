using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    [Route("Registration")]
    public class RegistrationController : Controller
    {
        // GET: /Registration/Index
        [HttpGet("Index")]
        public IActionResult Index()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Registration/Index.cshtml");
        }
    }
}