using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    public class ProfileController : Controller
    {
        [HttpGet("Profile/Index")]
        public IActionResult Index()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }
            string userRole = HttpContext.Session.GetString("UserRole") ?? "User";
            ViewBag.SelectedLayout = userRole.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                ? "~/Views/Shared/Layouts/_AdminLayout.cshtml"
                : "~/Views/Shared/Layouts/_TeacherLayout.cshtml";

            return View("~/Views/Profile/Index.cshtml");
        }
    }
}

