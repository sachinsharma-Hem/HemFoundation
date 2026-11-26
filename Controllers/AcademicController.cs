using HemFoundation.Services.Academic;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    /// <summary>Who is using the academic pages, and for which school.</summary>
    public class AcademicAccess
    {
        public bool LoggedIn { get; init; }
        public string Role { get; init; } = "";
        public int UserId { get; init; }
        public bool IsSchool { get; init; }
        public bool IsStudent { get; init; }
        public bool IsAdmin { get; init; }
        /// <summary>The school whose data is shown: the school itself, or the one an admin picked.</summary>
        public int InstituteId { get; init; }

        public static AcademicAccess From(HttpContext http)
        {
            var s = http.Session;
            if (s.GetString("IsAdminLoggedIn") != "true") return new AcademicAccess();
            string role = s.GetString("UserRole") ?? "";
            int userId = s.GetInt32("UserID") ?? 0;
            bool isAdmin = !string.IsNullOrEmpty(s.GetString("AdminId"))
                           || role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || role.Equals("WJ_Admin", StringComparison.OrdinalIgnoreCase);
            bool isSchool = role.Equals("WJ_School", StringComparison.OrdinalIgnoreCase);
            bool isStudent = role.Equals("WJ_Students", StringComparison.OrdinalIgnoreCase);

            int inst = 0;
            if (isSchool) inst = userId;
            else if (isAdmin && int.TryParse(http.Request.Query["instituteId"], out var picked) && picked > 0) inst = picked;

            return new AcademicAccess { LoggedIn = true, Role = role, UserId = userId, IsSchool = isSchool, IsStudent = isStudent, IsAdmin = isAdmin, InstituteId = inst };
        }
    }

    [Route("Academic")]
    public class AcademicController : Controller
    {
        private readonly AcademicService _academic;
        private readonly HemFoundation.Services.Parent.ParentService _parents;

        public AcademicController(AcademicService academic, HemFoundation.Services.Parent.ParentService parents)
        {
            _academic = academic;
            _parents = parents;
        }

        /// <summary>Subjects, terms, exam setup, marks and report cards of a school.</summary>
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var a = AcademicAccess.From(HttpContext);
            if (!a.LoggedIn) return RedirectToAction("Index", "Home");
            if (a.IsStudent) return RedirectToAction(nameof(Scorecard));
            if (a.InstituteId == 0)
                return View("NoSchool", a.IsAdmin
                    ? "Open this page with ?instituteId=<school id> to see a school’s academics."
                    : "Academics is available to school accounts.");

            ViewBag.TablesReady = await _academic.TablesExistAsync();
            ViewBag.InstituteId = a.InstituteId;
            ViewBag.IsAdminView = !a.IsSchool;
            ViewBag.AcademicYear = AcademicService.CurrentAcademicYear(DateTime.Today);
            return View();
        }

        /// <summary>Printable report card. Students see their own; schools see their students'; admins see any.</summary>
        [HttpGet("Scorecard/{id:int?}")]
        public async Task<IActionResult> Scorecard(int? id)
        {
            var a = AcademicAccess.From(HttpContext);
            if (!a.LoggedIn) return RedirectToAction("Index", "Home");

            int studentId = a.IsStudent ? a.UserId : id ?? 0;
            if (studentId <= 0) return View("NoSchool", "Choose a student from Academics → Report cards.");

            // A parent sees the report cards of the children they have added.
            var parent = ParentAccess.From(HttpContext);
            bool parentOk = parent.IsParent && await _parents.TableExistsAsync() && await _parents.IsMappedAsync(parent.ParentId, studentId);

            if (!a.IsStudent && !a.IsAdmin && !parentOk)
            {
                if (parent.IsParent) return View("NoSchool", "This child is not linked to your account.");
                if (!a.IsSchool || !await _academic.StudentBelongsAsync(a.InstituteId, studentId))
                    return View("NoSchool", "This student is not in your school.");
            }

            if (!await _academic.TablesExistAsync()) return View("NoSchool", "The academic tables are not set up yet. Run Data/Scripts/RunOnce_Update_2026_11_27.sql.");

            var card = await _academic.ReportCardAsync(studentId);
            if (card == null) return View("NoSchool", "Student not found.");
            ViewBag.BackUrl = parentOk ? "/Parent/Index" : a.IsStudent ? "/Learn/Index" : "/Academic/Index" + (a.IsSchool ? "" : "?instituteId=" + card.InstituteId) + "#report-cards";
            return View(card);
        }
    }
}
