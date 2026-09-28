using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    [Route("admin")]
    public class AdminController : Controller
    {
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
          
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpGet("AffiliateVisitTracker")] 
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
        [HttpGet("Leave")]
        public IActionResult Leave()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }
            return View("~/Views/Admin/Leave.cshtml");
        }

        [HttpGet("ManageBulkReport")]
        public IActionResult ManageBulkReport()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Admin/ManageBulkReport.cshtml");
        }
        [HttpGet("StudentDemo")]
        public IActionResult StudentDemo()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Admin/StudentDemo.cshtml");
        }
        [HttpGet("StudentCourseSetup")]
        public IActionResult StudentCourseSetup()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Admin/StudentCourseSetup.cshtml");
        }

        [HttpGet("AffiliateReport")]
        public IActionResult AffiliateReport()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Admin/AffiliateReport.cshtml");
        }


        [HttpGet("UserDetails")]
        public IActionResult UserDetails()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }
            return View("~/Views/Admin/UserDetails.cshtml");
        }
    }
}