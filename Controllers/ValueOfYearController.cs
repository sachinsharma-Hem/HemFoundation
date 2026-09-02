using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    [Route("ValueOfYear")]
    public class ValueOfYearController : Controller
    {
        [HttpGet("Index")]
        public IActionResult Index()
        {
            // Security Check: Ensure user is logged in
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/ValueOfYear/Index.cshtml");
        }
        [HttpGet("ActivitySubmission/{valueItemId}")]
        public IActionResult ActivitySubmission(int valueItemId)
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.ValueItemId = valueItemId;
            return View("~/Views/ValueOfYear/ActivitySubmission.cshtml");
        }
    }
}