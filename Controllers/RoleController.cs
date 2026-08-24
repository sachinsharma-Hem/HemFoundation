using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    [Route("role")]
    public class RoleController : Controller
    {
        [HttpGet("index")]
        [HttpGet("")]
        public IActionResult Index()
        {
            // Security Guard: Check C# Session
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }
    }
}