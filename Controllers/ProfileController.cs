using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    public class ProfileController : Controller
    {
        [HttpGet("Profile/Index")]
        public IActionResult Index()
        {
            // 1. Session check karein ki user logged-in hai ya nahi
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            // 2. Session se user ka role nikal lein jo login ke time save kiya tha
            string userRole = HttpContext.Session.GetString("UserRole") ?? "User";

            // 3. Role ke hisaab se layout set karein
            // Agar role "Admin" hai toh _AdminLayout, warna _TeacherLayout
            ViewBag.SelectedLayout = userRole.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                ? "~/Views/Shared/Layouts/_AdminLayout.cshtml"
                : "~/Views/Shared/Layouts/_TeacherLayout.cshtml";

            return View("~/Views/Profile/Index.cshtml");
        }
    }
}

//using Microsoft.AspNetCore.Mvc;

//namespace HemFoundation.Controllers
//{
//    public class ProfileController : Controller
//    {
//        [HttpGet("Profile/Index")]
//        public IActionResult Index()
//        {
//            return View("~/Views/Profile/Index.cshtml");
//        }
//    }
//}