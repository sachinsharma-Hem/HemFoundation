using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    [Route("admin")]
    public class AdminController : Controller
    {
        // GET: /admin/dashboard
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            // =========================================================================
            // INSTANT SERVER-SIDE BLOCK: Prevents 1-Second Page Flash (FOUC)
            // =========================================================================
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                // Agar session nahi hai ya expire ho gaya hai, toh turant Home page par bhej dein
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // GET: /admin/AffiliateVisitTracker
        [HttpGet("AffiliateVisitTracker")] // "Admin/" yahan se hata diya hai
        public IActionResult AffiliateVisitTracker()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }
            return View("~/Views/Admin/AffiliateVisitTracker.cshtml");
        }

        [HttpGet("ResourceRequest")]
        public IActionResult ResourceRequest()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true") return RedirectToAction("Index", "Home");

            return View("~/Views/Admin/ResourceRequest.cshtml");
        }

        [HttpGet("ManageBillings")]
        public IActionResult ManageBillings()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }
            return View("~/Views/Admin/ManageBillings.cshtml");
        }

        [HttpGet("ManageVolunteers")]
        public IActionResult ManageVolunteers()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true") return RedirectToAction("Index", "Home");

            return View("~/Views/Admin/ManageVolunteers.cshtml");
        }

        [HttpGet("ManageLeaves")]
        // GET: /admin/Leave (Ya /admin/leaves)
        [HttpGet("Leave")]
        public IActionResult Leave()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            // Aapki file ka naam Leave.cshtml hai, toh wahi path dein:
            return View("~/Views/Admin/Leave.cshtml");
        }
    }
}