using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("{portal}/Dashboard")]
        public IActionResult Index(string portal)
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["PortalName"] = portal;
            return View("~/Views/Dashboard/Index.cshtml");
        }

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
       
    }
}