using HemFoundation.Data;
using HemFoundation.Models.DTOs;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;
using HemFoundation.DTOs;
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
            if (!string.IsNullOrEmpty(portal) &&
     portal.Equals("school", StringComparison.OrdinalIgnoreCase))
            {
                SchoolProfileDetailsDto? institute = null;

                if (!string.IsNullOrEmpty(userId))
                {
                    // =====================================================
                    // 1. Current logged-in user find karo
                    // =====================================================
                    var currentUser = await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u =>
                            u.AspNetUserID == userId);

                    // =====================================================
                    // 2. Users.UserID = InstituteID ke according school find
                    // =====================================================
                    if (currentUser != null)
                    {
                        institute = await _context.SchoolProfileDetails
                            .AsNoTracking()
                            .FirstOrDefaultAsync(i =>
                                i.instituteid == currentUser.UserID);
                    }
                }
              
                // =====================================================
                // 3. School mil gayi to dashboard counts
                // =====================================================
                if (institute != null)
                {
                    var instituteId = institute.instituteid;
                    var knowledgePartner = await _context.KnowledgePartnersList
         .FirstOrDefaultAsync(k => k.InstituteID == instituteId && k.FileUrl != null);

                    bool alreadyUploaded = knowledgePartner != null;

                    ViewBag.StudentsCount =  institute.associatedstudents;
                    ViewBag.ActivetudentCount = institute.activestudents;
                    ViewBag.TeachersCount = institute.totalteachers;
                    ViewBag.ActiveTeacherCount = institute.activeteachers;
                    ViewBag.InstituteName = institute.institute_name;
                    ViewBag.hasUploadedLetter = alreadyUploaded;
                    ViewBag.PartnershipLetterPath = knowledgePartner?.FileUrl;
                }
                else
                {
                    ViewBag.StudentsCount = 0;
                    ViewBag.TeachersCount = 0;
                    ViewBag.VolunteersCount = 0;
                    ViewBag.InstituteName = "";
                    ViewBag.hasUploadedLetter = false;
                    ViewBag.PartnershipLetterPath = null;
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
        [HttpGet("Teacher/McqHub")]
        public async Task<IActionResult> McqHub()
        {
            int? teacherId = HttpContext.Session.GetInt32("UserID");

            // FromSqlRaw ki jagah yahan Database.SqlQuery use kiya gaya hai
            var quizStatusList = await _context.Database
                .SqlQuery<TeacherQuizHubDto>($"SELECT * FROM hf_sp_get_teacher_quiz_completion_status({teacherId})")
                .ToListAsync();

            return View("~/Views/Teacher/McqHub.cshtml", quizStatusList);
        }



        [HttpGet("Teacher/LoadCapacity/{id}")]
        public async Task<IActionResult> LoadCapacity(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest("Invalid CourseID.");
                }

                var mainTopic = await _context.Topics
                    .AsNoTracking()
                    .Where(t => t.CourseID == id && t.ParentTopicID == null)
                    .OrderBy(t => t.Order)
                    .FirstOrDefaultAsync();

                if (mainTopic == null)
                {
                    return BadRequest($"No main topic found for CourseID {id}.");
                }

                int topicsCount = await _context.Topics
                    .AsNoTracking()
                    .Where(t => t.CourseID == id && t.ParentTopicID == null)
                    .CountAsync();

                // Sub-topics count jinka parent mainTopic ki ID ho
                int subTopicsCount = await _context.Topics
                    .AsNoTracking()
                    .Where(t => t.CourseID == id && t.ParentTopicID == mainTopic.TopicID)
                    .CountAsync();

                ViewBag.TopicsCount = topicsCount > 0 ? topicsCount : 1;
                ViewBag.SubTopicsCount = subTopicsCount > 0 ? subTopicsCount : 2;

                var viewModel = new CapacityViewModel
                {
                    Id = mainTopic.CourseID,
                    Name = mainTopic.TopicName ?? "Cognitive Capacity",
                    Description = mainTopic.Description ?? "Manage evaluations and interactive modules for this capacity.",
                    ImageUrl = !string.IsNullOrWhiteSpace(mainTopic.TrainerResource) ? mainTopic.TrainerResource : "/images/default-course.png"
                };

                string firstWord = viewModel.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? "cognitive";

                string viewName = firstWord switch
                {
                    "cognitive" => "~/Views/Teacher/_Cognitive.cshtml",
                    "social" => "~/Views/Teacher/_Social.cshtml",
                    "ethical" => "~/Views/Teacher/_Ethical.cshtml",
                    "emotional" => "~/Views/Teacher/_Emotional.cshtml",
                    "life" => "~/Views/Teacher/_LifeSkills.cshtml",
                    _ => "~/Views/Teacher/_Cognitive.cshtml"
                };

                return PartialView(viewName, viewModel);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpGet("api/teacher-capacity/details/{courseId}")]
        public async Task<IActionResult> GetCapacityDetails(int courseId)
        {
            try
            {
                var mainTopic = await _context.Topics
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.CourseID == courseId && t.ParentTopicID == null);

                if (mainTopic == null)
                    return NotFound(new { success = false, message = "Topic not found" });

                // Sub-topics nikal rahe hain jinka ParentTopicID mainTopic ke TopicID se match kare
                var subTopicsList = await _context.Topics
                    .AsNoTracking()
                    .Where(t => t.CourseID == courseId && t.ParentTopicID == mainTopic.TopicID)
                    .ToListAsync();

                var subTopics = subTopicsList.Select(st => new {
                    id = st.TopicID,
                    subTopicName = st.TopicName ?? "Untitled Module",
                    videoLink = st.TrainerVideo ?? st.StudentVideo ?? "",
                    numberOfQuestions = st.NoOfQuestion ?? 0,
                    mcqThumbnail = !string.IsNullOrEmpty(st.TrainerResource) ? st.TrainerResource : (mainTopic.TrainerResource ?? "/images/default-course.png")
                }).ToList();

                int topicsCount = await _context.Topics.CountAsync(t => t.CourseID == courseId && t.ParentTopicID == null);
                int subTopicsCount = subTopics.Count;

                return Ok(new
                {
                    success = true,
                    courseRegId = courseId,
                    category = new { imageUrl = mainTopic.TrainerResource ?? "/images/default-course.png" },
                    counts = new { topicsCount, subTopicsCount },
                    subTopics = subTopics
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
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