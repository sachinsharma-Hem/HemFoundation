using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    [Route("admin")]
    public class DbmanagerController : Controller
    {
        [HttpGet("superadmindata")]
        public IActionResult Index()
        {
            var isLoggedIn =
                HttpContext.Session.GetString("IsAdminLoggedIn");

            if (string.IsNullOrEmpty(isLoggedIn) ||
                isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Dbmanager/Index.cshtml");
        }
    }
}