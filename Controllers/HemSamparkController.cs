using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    public class HemSamparkController : Controller
    {
        [HttpGet("HemSampark/Admin")]
        public IActionResult Admin()
        {
            return View(); 
        }

        [HttpGet("hem-sampark/dashboard")]
        public IActionResult SamparkDashboard()
        {
            if (HttpContext.Session.GetString("IsAdminLoggedIn") != "true" && string.IsNullOrEmpty(User.Identity?.Name))
            {
                return RedirectToAction("Index", "Home");
            }
            return View("~/Views/HemSampark/Dashboard.cshtml");
        }

    }
}