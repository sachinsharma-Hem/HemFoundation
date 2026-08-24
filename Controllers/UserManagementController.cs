using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    [Route("usermanagement")]
    public class UserManagementController : Controller
    {
        [HttpGet("index")]
        [HttpGet("")]
        public IActionResult Index()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }
    }
}