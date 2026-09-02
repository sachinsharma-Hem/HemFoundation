using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    [Route("Value")]
    public class ValueController : Controller
    {
        // GET: /Value/Index
        [HttpGet("Index")]
        public IActionResult Index()
        {
            // Admin Authentication Check (Taaki koi bina login ke is page ko na khol sake)
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            // Page Render Karna
            return View("~/Views/Value/Index.cshtml");
        }
    }
}