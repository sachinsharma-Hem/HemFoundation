using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DashboardController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
             _env = env;
        }

        [HttpGet("{portal}/Dashboard")]
        public async Task<IActionResult> Index(string portal)
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["PortalName"] = portal;
            string? userId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            // ==========================================
            // 1. SCHOOL PORTAL LOGIC
            // ==========================================
            if (!string.IsNullOrEmpty(portal) && portal.Equals("school", StringComparison.OrdinalIgnoreCase))
            {
                Institute? institute = null;
                if (!string.IsNullOrEmpty(userId))
                {
                    institute = await _context.Institutes.AsNoTracking().FirstOrDefaultAsync(i => i.AspNetUserID == userId && i.IsActive);
                }

                if (institute != null)
                {
                    ViewBag.StudentsCount = await _context.Students.CountAsync(s => s.InstituteID == institute.InstituteID && s.IsActive);
                    ViewBag.TeachersCount = await _context.Teachers.CountAsync(t => t.InstituteID == institute.InstituteID && t.IsActive);
                    ViewBag.VolunteersCount = await _context.Volunteers.CountAsync(v => v.InstituteId == institute.InstituteID && v.IsActive);
                    ViewBag.InstituteName = institute.Name;
                }
            }
            // ==========================================
            // 2. STUDENT PORTAL LOGIC (NAYA ADD KIYA HAI)
            // ==========================================
            else if (!string.IsNullOrEmpty(portal) && portal.Equals("student", StringComparison.OrdinalIgnoreCase))
            {
                var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.AspNetUserID == userId && s.IsActive);

                if (student != null)
                {
                    // Standard ko StandardMaster se map karke ID nikalna
                    var standardMaster = await _context.StandardMasters.FirstOrDefaultAsync(sm => sm.StandardName == student.Standard);
                    int standardId = standardMaster?.Id ?? 0;

                    // Assigned courses count nikalna based on standard
                    int assignedCourses = await _context.StudentCourseMasters.CountAsync(c => c.StandardId == standardId && c.IsActive);

                    ViewBag.StudentName = $"{student.FirstName} {student.LastName}";
                    ViewBag.StandardName = student.Standard;
                    ViewBag.StandardId = standardId;

                    // Dashboard Stats
                    ViewBag.AssignedCourses = assignedCourses;
                    ViewBag.CompletedModules = 0; // Ise baad mein student progress table se link karenge
                    ViewBag.TotalMarks = 0;
                    ViewBag.CertificatesEarned = 0;
                }
                else
                {
                    ViewBag.StandardId = 0;
                    ViewBag.AssignedCourses = 0;
                }
            }

            return View("~/Views/Dashboard/Index.cshtml");
        }
        //[HttpGet("{portal}/Dashboard")]
        //public async Task<IActionResult> Index(string portal)
        //{
        //    var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");

        //    if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
        //    {
        //        return RedirectToAction("Index", "Home");
        //    }

        //    ViewData["PortalName"] = portal;

        //    if (!string.IsNullOrEmpty(portal) &&
        //        portal.Equals("school", StringComparison.OrdinalIgnoreCase))
        //    {
        //        // First priority: Session UserId
        //        string? userId = HttpContext.Session.GetString("UserId");

        //        // Fallback: JWT Claim, agar available ho
        //        if (string.IsNullOrEmpty(userId))
        //        {
        //            userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //        }

        //        Institute? institute = null;

        //        if (!string.IsNullOrEmpty(userId))
        //        {
        //            institute = await _context.Institutes
        //                .AsNoTracking()
        //                .FirstOrDefaultAsync(i =>
        //                    i.AspNetUserID == userId &&
        //                    i.IsActive);
        //        }

        //        if (institute != null)
        //        {
        //            int studentsCount = await _context.Students
        //                .Where(s =>
        //                    s.InstituteID == institute.InstituteID &&
        //                    s.IsActive)
        //                .CountAsync();

        //            // Naya query: Active Teachers ka count nikalne ke liye
        //            int teachersCount = await _context.Teachers
        //                .Where(t =>
        //                    t.InstituteID == institute.InstituteID &&
        //                    t.IsActive)
        //                .CountAsync();


        //            // 4. Volunteers Count (Naya add kiya gaya hai)
        //            int volunteersCount = await _context.Volunteers
        //                .Where(v => v.InstituteId == institute.InstituteID && v.IsActive) // Agar InstituteID nahi hai, toh apni mapping ke hisaab se condition change kar lena
        //                .CountAsync();

        //            ViewBag.Institute = institute;
        //            ViewBag.InstituteName = institute.Name;
        //            ViewBag.IsVerified = institute.IsVerify;

        //            ViewBag.StudentsCount = studentsCount;
        //            ViewBag.TeachersCount = teachersCount; // Hardcoded 0 ki jagah variable pass kar diya
        //            ViewBag.VolunteersCount = volunteersCount;
        //        }
        //        else
        //        {
        //            ViewBag.Institute = null;
        //            ViewBag.InstituteName = "My School Institution";
        //            ViewBag.IsVerified = false;

        //            ViewBag.StudentsCount = 0;
        //            ViewBag.TeachersCount = 0;
        //            ViewBag.VolunteersCount = 0;
        //        }
        //    }

        //    return View("~/Views/Dashboard/Index.cshtml");
        //}



        // --- Dynamic MCQ Capacities Hub ---       
        [HttpGet("Teacher/McqHub")]
        public async Task<IActionResult> McqHub()
        {
            // Database se active categories fetch karke DisplayOrder ke hisaab se sort karenge
            var categories = await _context.WebinarMcqCategories
                .Where(x => x.IsActive && !x.IsDeleted)
                .Include(x => x.OrderNumber)
                .OrderBy(x => x.OrderNumber != null ? x.OrderNumber.DisplayOrder : int.MaxValue)
                .ToListAsync();

            return View("~/Views/Teacher/McqHub.cshtml", categories);
        }

        [HttpGet("Teacher/LoadCapacity/{id}")]
        public async Task<IActionResult> LoadCapacity(int id)
        {
            var category = await _context.WebinarMcqCategories
                .Include(x => x.OrderNumber)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (category == null || category.IsDeleted)
            {
                return BadRequest("Invalid capacity requested.");
            }

            // CourseTopics aur CourseSubTopics ka count nikal rahe hain (Category ID ya Name ke base par)
            // Agar aapke paas CourseRegistrationId ya mapping hai toh us hisaab se filter karein
            int topicsCount = await _context.CourseTopics
                .Where(t => t.TopicName.Contains(category.Name ?? "") && !t.IsDeleted)
                .CountAsync();

            int subTopicsCount = await _context.CourseSubTopics
                .Where(st => !st.IsDeleted)
                .CountAsync();

            ViewBag.TopicsCount = topicsCount > 0 ? topicsCount : 1; // Fallback demo count
            ViewBag.SubTopicsCount = subTopicsCount > 0 ? subTopicsCount : 2; // Fallback demo count

            // Database name ka pehla word nikal kar partial view load karenge
            string firstWord = category.Name?.Split(' ')[0].ToLower() ?? "cognitive";

            string viewName = firstWord switch
            {
                "cognitive" => "~/Views/Teacher/_Cognitive.cshtml",
                "social" => "~/Views/Teacher/_Social.cshtml",
                "ethical" => "~/Views/Teacher/_Ethical.cshtml",
                "emotional" => "~/Views/Teacher/_Emotional.cshtml",
                "life" => "~/Views/Teacher/_LifeSkills.cshtml",
                _ => "~/Views/Teacher/_Cognitive.cshtml"
            };

            return PartialView(viewName, category);
        }

        [HttpGet("Dashboard/DownloadSampleLetter")]
        public IActionResult DownloadSampleLetter()
        {
            var filePath = Path.Combine(_env.WebRootPath, "images", "Partnership_Letter.pdf");

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Sample partnership letter not found on server.");
            }

            var mimeType = "application/pdf";
            return PhysicalFile(filePath, mimeType, "Partnership_Letter.pdf");
        }

        

    }
}