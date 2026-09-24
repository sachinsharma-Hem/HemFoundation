using HemFoundation.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers
{
    public class AffiliateController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AffiliateController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet("Affiliate/Index")]
        public IActionResult Index()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Affiliate/Index.cshtml");
        }
        [HttpGet("Affiliate/Hub")]
        public IActionResult Hub()
        {
            return View("~/Views/Affiliate/Hub.cshtml");
        }

        [HttpGet("Affiliate/LoadModule/{moduleName}")]
        public IActionResult LoadModule(string moduleName)
        {
            string viewName = moduleName.ToLower() switch
            {
                "planvisit" => "~/Views/Affiliate/Partials/_PlanVisit.cshtml",
                "network" => "~/Views/Affiliate/Partials/_Network.cshtml",
                "rewards" => "~/Views/Affiliate/Partials/_Rewards.cshtml",
                "reports" => "~/Views/Affiliate/Partials/_Reports.cshtml",  
                "performancedashboard" => "~/Views/Affiliate/Partials/_PerformanceDashboard.cshtml",
                "focusedperformance" => "~/Views/Affiliate/Partials/_FocusedPerformance.cshtml",
                "attendancesummary" => "~/Views/Affiliate/Partials/_AttendanceSummary.cshtml",
                "activitydashboard" => "~/Views/Affiliate/Partials/_ActivityDashboard.cshtml",
                _ => "~/Views/Affiliate/Partials/_PlanVisit.cshtml" // Fallback default
            };

            return PartialView(viewName);
        }

        [HttpGet("Affiliate/SchoolMapping")]
        public IActionResult SchoolMapping()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Affiliate/SchoolMapping.cshtml");
        }
        [HttpGet("Affiliate/RequestMaterial")]
        public IActionResult RequestMaterial()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Affiliate/RequestMaterial.cshtml");
        }

        [HttpGet("Affiliate/Financials")]
        public IActionResult Financials()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true") return RedirectToAction("Index", "Home");

            return View("~/Views/Affiliate/Financials.cshtml");
        }

        [HttpGet("Affiliate/Volunteers")]
        public IActionResult Volunteers()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true") return RedirectToAction("Index", "Home");

            return View("~/Views/Affiliate/Volunteers.cshtml");
        }
        [HttpGet("Affiliate/OtherActivity")]
        public IActionResult OtherActivity()
        {
            // Login/Session check as per your requirement
            var affiliateId = HttpContext.Session.GetString("AffiliateUserId") ?? HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(affiliateId))
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Affiliate/OtherActivity.cshtml");
        }
        [HttpGet("Affiliate/Guides")]
        public IActionResult Guides()
        {
            // Yahan Affiliate ka session check lagayein (Agar zaroorat ho)
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn"); // Replace with Affiliate login check
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Affiliate/Guides.cshtml");
        }

        public IActionResult AffiliateIncentive()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Affiliate/AffiliateIncentive.cshtml");
        }
    }
}