using HemFoundation.Services.Parent;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    /// <summary>Who is using the parent pages, and whose children are shown.</summary>
    public class ParentAccess
    {
        public bool LoggedIn { get; init; }
        public bool IsParent { get; init; }
        public bool IsAdmin { get; init; }
        /// <summary>The parent (users.userid): the logged-in parent, or the one an admin opened with ?parentId=.</summary>
        public int ParentId { get; init; }

        public static ParentAccess From(HttpContext http)
        {
            var s = http.Session;
            if (s.GetString("IsAdminLoggedIn") != "true") return new ParentAccess();
            string role = s.GetString("UserRole") ?? "";
            int userId = s.GetInt32("UserID") ?? 0;
            bool isParent = role.Equals("HF_PARENT", StringComparison.OrdinalIgnoreCase) || role.Equals("Parent", StringComparison.OrdinalIgnoreCase);
            bool isAdmin = !string.IsNullOrEmpty(s.GetString("AdminId"))
                           || role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || role.Equals("WJ_Admin", StringComparison.OrdinalIgnoreCase);

            int parentId = 0;
            if (isParent) parentId = userId;
            else if (isAdmin && int.TryParse(http.Request.Query["parentId"], out var picked) && picked > 0) parentId = picked;
            return new ParentAccess { LoggedIn = true, IsParent = isParent, IsAdmin = isAdmin, ParentId = parentId };
        }
    }

    [Route("Parent")]
    public class ParentController : Controller
    {
        private readonly ParentService _parent;
        public ParentController(ParentService parent) => _parent = parent;

        /// <summary>My children + each child's progress dashboard (one page).</summary>
        [HttpGet("")]
        [HttpGet("Index")]
        [HttpGet("Dashboard")]
        [HttpGet("SearchChildren")]
        public async Task<IActionResult> Index()
        {
            var a = ParentAccess.From(HttpContext);
            if (!a.LoggedIn) return RedirectToAction("Index", "Home");
            if (a.ParentId == 0)
                return View("NoAccess", a.IsAdmin
                    ? "Open this page with ?parentId=<parent user id> to see what a parent sees."
                    : "This page is for parent accounts.");

            ViewBag.TableReady = await _parent.TableExistsAsync();
            ViewBag.ParentId = a.ParentId;
            ViewBag.IsAdminView = !a.IsParent;
            return View();
        }
    }
}
