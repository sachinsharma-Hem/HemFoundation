using HemFoundation.Data;
using HemFoundation.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>Roles that see the full admin dashboard (as before), besides super admins.</summary>
        private static readonly string[] FullDashboardRoles = { "Admin", "WJ_Admin" };

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
          
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            string role = HttpContext.Session.GetString("UserRole") ?? "";
            bool superAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";

            // Admin, WJ_Admin, super admins and Supervisors: the dashboard as it is today.
            if (superAdmin
                || FullDashboardRoles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase))
                || string.Equals(role, "Supervisor", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(role))
            {
                return View();
            }

            // Any other role: a dashboard built from the menus given to that role (Role rights page).
            var menus = await RoleDashboard.MenusForRoleAsync(_context, role);
            var model = new RoleDashboard.ViewModel
            {
                Role = role,
                Name = HttpContext.Session.GetString("UserFullName") ?? "",
                Menus = menus,
                Widgets = RoleDashboard.WidgetsFor(menus)
            };
            return View("RoleDashboard", model);
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
        [HttpGet("passwordmismatch")]
        public IActionResult PasswordMismatch()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Admin/PasswordMismatch.cshtml");
        }

    }
}