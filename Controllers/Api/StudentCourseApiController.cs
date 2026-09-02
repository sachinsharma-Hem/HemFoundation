using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Route("api/studentcourse")]
    [ApiController]
    public class StudentCourseApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public StudentCourseApiController(ApplicationDbContext context) { _context = context; }

        [HttpGet("standards")]
        public async Task<IActionResult> GetStandards()
        {
            var standards = await _context.StandardMasters.Where(s => s.IsActive).OrderBy(s => s.DisplayOrder).ToListAsync();
            return Ok(new { success = true, data = standards });
        }

        [HttpPost("add-course")]
        public async Task<IActionResult> AddCourse([FromForm] StudentCourseMaster model)
        {
            model.CreatedDate = DateTime.UtcNow;
            _context.StudentCourseMasters.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Course created successfully." });
        }

        // --- YEH METHOD MISSING THA ---
        [HttpPost("update-course")]
        public async Task<IActionResult> UpdateCourse([FromForm] StudentCourseMaster model)
        {
            var existing = await _context.StudentCourseMasters.FindAsync(model.Id);
            if (existing == null) return NotFound(new { success = false, message = "Course not found." });

            existing.CourseName = model.CourseName;
            existing.TotalPoints = model.TotalPoints; // Points fix
            existing.DisplayOrder = model.DisplayOrder;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Course updated successfully." });
        }

        [HttpPost("add-module")]
        public async Task<IActionResult> AddModule([FromForm] StudentCourseModule model)
        {
            _context.StudentCourseModules.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Module added successfully." });
        }

        [HttpPost("update-module")]
        public async Task<IActionResult> UpdateModule([FromForm] StudentCourseModule model)
        {
            var existing = await _context.StudentCourseModules.FindAsync(model.Id);
            if (existing == null) return NotFound(new { success = false, message = "Module not found." });

            existing.ModuleName = model.ModuleName;
            existing.UnlockDate = model.UnlockDate;
            existing.Points = model.Points;
            existing.DisplayOrder = model.DisplayOrder;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Module updated successfully." });
        }

        // --- YEH DELETE METHODS ADD KIYE GAYE HAIN ---
        [HttpDelete("delete-course/{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _context.StudentCourseMasters.FindAsync(id);
            if (course != null)
            {
                var modules = _context.StudentCourseModules.Where(m => m.CourseId == id);
                _context.StudentCourseModules.RemoveRange(modules);
                _context.StudentCourseMasters.Remove(course);
                await _context.SaveChangesAsync();
            }
            return Ok(new { success = true, message = "Course deleted successfully." });
        }

        [HttpDelete("delete-module/{id}")]
        public async Task<IActionResult> DeleteModule(int id)
        {
            var module = await _context.StudentCourseModules.FindAsync(id);
            if (module != null)
            {
                _context.StudentCourseModules.Remove(module);
                await _context.SaveChangesAsync();
            }
            return Ok(new { success = true, message = "Week deleted successfully." });
        }

        [HttpGet("course-list/{standardId}")]
        public async Task<IActionResult> GetCoursesWithModules(int standardId)
        {
            var courses = await _context.StudentCourseMasters
                .Where(c => c.StandardId == standardId && c.IsActive)
                .OrderBy(c => c.DisplayOrder).ToListAsync();

            var modules = await _context.StudentCourseModules
                .Where(m => courses.Select(c => c.Id).Contains(m.CourseId) && m.IsActive)
                .OrderBy(m => m.DisplayOrder).ToListAsync();

            var result = courses.Select(c => new
            {
                c.Id,
                c.CourseName,
                c.ThemeColor,
                c.TotalPoints,
                Modules = modules.Where(m => m.CourseId == c.Id).ToList()
            });

            return Ok(new { success = true, data = result });
        }

        // Dropdown ke liye Courses fetch karna (By Standard)
        [HttpGet("get-courses/{standardId}")]
        public async Task<IActionResult> GetCoursesOnly(int standardId)
        {
            var courses = await _context.StudentCourseMasters.Where(c => c.StandardId == standardId && c.IsActive).ToListAsync();
            return Ok(new { success = true, data = courses });
        }

        // Dropdown ke liye Modules/Weeks fetch karna (By Course)
        [HttpGet("get-modules/{courseId}")]
        public async Task<IActionResult> GetModulesOnly(int courseId)
        {
            var modules = await _context.StudentCourseModules.Where(m => m.CourseId == courseId && m.IsActive).ToListAsync();
            return Ok(new { success = true, data = modules });
        }

        // Admin: Add Module Content (With File Uploads)
        [HttpPost("add-content")]
        public async Task<IActionResult> AddModuleContent([FromForm] StudentModuleContent model, IFormFile? ThumbnailFile, IFormFile? ResourceFile)
        {
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "training");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // Save Thumbnail Image
            if (ThumbnailFile != null && ThumbnailFile.Length > 0)
            {
                string ext = Path.GetExtension(ThumbnailFile.FileName);
                string fileName = $"thumb_{Guid.NewGuid()}{ext}";
                using (var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create))
                {
                    await ThumbnailFile.CopyToAsync(stream);
                }
                model.ThumbnailUrl = $"/images/training/{fileName}";
            }

            // Save PDF/Resource File
            if (ResourceFile != null && ResourceFile.Length > 0)
            {
                string ext = Path.GetExtension(ResourceFile.FileName);
                string fileName = $"res_{Guid.NewGuid()}{ext}";
                using (var stream = new FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create))
                {
                    await ResourceFile.CopyToAsync(stream);
                }
                model.ResourceFileUrl = $"/images/training/{fileName}";
            }

            model.CreatedDate = DateTime.UtcNow;
            _context.StudentModuleContents.Add(model);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Content added successfully!" });
        }

        // Student: Get Content for specific Module
        [HttpGet("module-content/{moduleId}")]
        public async Task<IActionResult> GetModuleContent(int moduleId)
        {
            var content = await _context.StudentModuleContents
                .Where(c => c.ModuleId == moduleId && c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            return Ok(new { success = true, data = content });
        }

        // Admin: Load Content list by Module ID
        [HttpGet("admin-content-list/{moduleId}")]
        public async Task<IActionResult> GetAdminContentList(int moduleId)
        {
            var content = await _context.StudentModuleContents
                .Where(c => c.ModuleId == moduleId)
                .OrderBy(c => c.TabCategory)
                .ThenBy(c => c.DisplayOrder)
                .ToListAsync();
            return Ok(new { success = true, data = content });
        }

        // Admin: Delete Content
        [HttpDelete("delete-content/{id}")]
        public async Task<IActionResult> DeleteContent(int id)
        {
            var content = await _context.StudentModuleContents.FindAsync(id);
            if (content != null)
            {
                _context.StudentModuleContents.Remove(content);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Content deleted successfully." });
            }
            return NotFound(new { success = false, message = "Content not found." });
        }
    }
}