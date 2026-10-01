using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    [Route("admin")]
    public class DbmanagerController : Controller
    {
        [HttpGet("{path}")]
        public IActionResult Index(string path)
        {
            var isLoggedIn =
                HttpContext.Session.GetString("IsAdminLoggedIn");

            var savedPath =
                HttpContext.Session.GetString("AdminDynamicPath");

            if (isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrEmpty(savedPath))
            {
                return RedirectToAction("Index", "Home");
            }

            if (!string.Equals(path, savedPath, StringComparison.Ordinal))
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Dbmanager/Index.cshtml");
        }
    }
}