using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Route("api/value")]
    [ApiController]
    public class ValueApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ValueApiController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ================== GRADE MASTER ==================

        [HttpGet("standards")]
        public async Task<IActionResult> GetStandards()
        {
            var standards = await _context.StandardMasters.Where(s => s.IsActive && s.Id >= 1 && s.Id <= 10).OrderBy(s => s.DisplayOrder).ToListAsync();
            return Ok(new { success = true, data = standards });
        }

        [HttpPost("add-grade")]
        public async Task<IActionResult> AddGrade([FromForm] ValueGradeMaster model)
        {
            model.CreatedDate = DateTime.UtcNow;
            _context.ValueGradeMasters.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Grade Master added successfully." });
        }

        [HttpGet("grades-list")]
        public async Task<IActionResult> GetGrades()
        {
            var grades = await _context.ValueGradeMasters.Where(g => g.IsActive).OrderBy(g => g.StandardId).ToListAsync();
            return Ok(new { success = true, data = grades });
        }

        // ================== VALUE ITEM MASTER ==================

        [HttpPost("add-value")]
        public async Task<IActionResult> AddValue([FromForm] int GradeId, [FromForm] string CapacityName, [FromForm] string ValueTitleEng, [FromForm] string ValueTitleHin, IFormFile ValueImage, IFormFile ActivityPdf)
        {
            var item = new ValueItemMaster
            {
                GradeId = GradeId,
                CapacityName = CapacityName,
                ValueTitleEng = ValueTitleEng,
                ValueTitleHin = ValueTitleHin,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };

            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "annual_calendar");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            if (ValueImage != null)
            {
                string imgName = $"{Guid.NewGuid()}_{ValueImage.FileName}";
                using (var fs = new FileStream(Path.Combine(uploadsFolder, imgName), FileMode.Create)) { await ValueImage.CopyToAsync(fs); }
                item.ValueImageUrl = $"/uploads/annual_calendar/{imgName}";
            }

            if (ActivityPdf != null)
            {
                string pdfName = $"{Guid.NewGuid()}_{ActivityPdf.FileName}";
                using (var fs = new FileStream(Path.Combine(uploadsFolder, pdfName), FileMode.Create)) { await ActivityPdf.CopyToAsync(fs); }
                item.ActivityPdfUrl = $"/uploads/annual_calendar/{pdfName}";
            }

            _context.ValueItemMasters.Add(item);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Calendar Value Card added successfully." });
        }

        [HttpGet("values-list")]
        public async Task<IActionResult> GetValues()
        {
            var values = await (from v in _context.ValueItemMasters
                                join g in _context.ValueGradeMasters on v.GradeId equals g.Id
                                orderby g.StandardId, v.CapacityName
                                select new
                                {
                                    v.Id,
                                    g.GradeName,
                                    g.GroupName,
                                    v.CapacityName,
                                    v.ValueTitleEng,
                                    v.ValueTitleHin,
                                    v.ValueImageUrl,
                                    v.ActivityPdfUrl
                                }).ToListAsync();
            return Ok(new { success = true, data = values });
        }

        // ================== SCHOOL CALENDAR API ==================

        [HttpGet("school-calendar")]
        public async Task<IActionResult> GetSchoolCalendar()
        {
            string? currentUserId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized(new { success = false, message = "Not logged in" });

            var institute = await _context.Institutes.FirstOrDefaultAsync(i => i.AspNetUserID == currentUserId);
            if (institute == null) return BadRequest(new { success = false, message = "School profile not found" });

            var schoolStandardNames = await _context.Students
                .Where(s => s.InstituteID == institute.InstituteID && s.IsActive && s.Standard != null)
                .Select(s => s.Standard).Distinct().ToListAsync();

            var standardIds = await _context.StandardMasters
                .Where(s => schoolStandardNames.Contains(s.StandardName))
                .Select(s => s.Id).ToListAsync();

            var grades = await _context.ValueGradeMasters
                .Where(g => g.IsActive && standardIds.Contains(g.StandardId))
                .OrderBy(g => g.StandardId).ToListAsync();

            var gradeIds = grades.Select(g => g.Id).ToList();

            var values = await _context.ValueItemMasters
                .Where(v => v.IsActive && gradeIds.Contains(v.GradeId)).ToListAsync();

            // NAYA: Check submissions to mark "Completed"
            var submittedActivityIds = await _context.ActivitySubmissions
                .Where(s => s.InstituteId == institute.InstituteID)
                .Select(s => s.ValueItemId)
                .Distinct()
                .ToListAsync();

            var result = grades.Select(g => new
            {
                g.Id,
                g.GradeName,
                g.GroupName,
                g.ButtonName,
                Capacities = values.Where(v => v.GradeId == g.Id).Select(v => new
                {
                    v.Id, // NAYA: URL ke liye ID zaroori hai
                    v.CapacityName,
                    v.ValueTitleEng,
                    v.ValueTitleHin,
                    v.ValueImageUrl,
                    v.ActivityPdfUrl,
                    IsCompleted = submittedActivityIds.Contains(v.Id) // NAYA: Status Check
                }).ToList()
            });

            return Ok(new { success = true, data = result });
        }

        // ================== ACTIVITY SUBMISSION APIS ==================

        [HttpGet("activity-page-data/{valueItemId}")]
        public async Task<IActionResult> GetActivityPageData(int valueItemId)
        {
            string? currentUserId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var institute = await _context.Institutes.FirstOrDefaultAsync(i => i.AspNetUserID == currentUserId);
            if (institute == null) return BadRequest(new { success = false, message = "School not found." });

            var valueItem = await _context.ValueItemMasters.FirstOrDefaultAsync(v => v.Id == valueItemId);
            if (valueItem == null) return BadRequest(new { success = false, message = "Activity not found." });

            var grade = await _context.ValueGradeMasters.FirstOrDefaultAsync(g => g.Id == valueItem.GradeId);
            var standard = await _context.StandardMasters.FirstOrDefaultAsync(s => s.Id == grade.StandardId);

            // 1. Us standard ke saare students
            var students = await _context.Students
    .Where(s => s.InstituteID == institute.InstituteID && s.Standard == standard.StandardName && s.IsActive)
    .Select(s => new { Id = s.StudentID, Name = s.FirstName + " " + s.LastName }) // <--- ISE UPDATE KIYA
    .ToListAsync();

            // 2. Existing Groups aur unke students
            var groups = await _context.ActivityGroups
                .Where(g => g.InstituteId == institute.InstituteID && g.ValueItemId == valueItemId && g.IsActive)
                .ToListAsync();

            var groupStudents = await _context.ActivityGroupStudents
                .Where(gs => groups.Select(g => g.Id).Contains(gs.GroupId))
                .ToListAsync();

            // 3. Existing Submissions
            var submissions = await _context.ActivitySubmissions
                .Where(s => s.InstituteId == institute.InstituteID && s.ValueItemId == valueItemId)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                activity = new { valueItem.CapacityName, valueItem.ValueTitleEng, grade.GradeName },
                students,
                groups = groups.Select(g => new {
                    g.Id,
                    g.GroupName,
                    StudentIds = groupStudents.Where(gs => gs.GroupId == g.Id).Select(gs => gs.StudentId).ToList()
                }),
                submissions
            });
        }

        public class SaveGroupDto { public string GroupName { get; set; } public List<int> StudentIds { get; set; } }

        // ========================================================
        // EXISTING SAVE GROUPS METHOD KO ISSE REPLACE KAREIN
        // ========================================================
        [HttpPost("save-groups/{valueItemId}")]
        public async Task<IActionResult> SaveGroups(int valueItemId, [FromBody] List<SaveGroupDto> requestGroups)
        {
            if (requestGroups == null || !requestGroups.Any())
                return BadRequest(new { success = false, message = "No data received." });

            string? currentUserId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var institute = await _context.Institutes.FirstOrDefaultAsync(i => i.AspNetUserID == currentUserId);

            if (institute == null)
                return BadRequest(new { success = false, message = "Institute not found." });

            // Puraani mappings delete karein
            var oldGroups = await _context.ActivityGroups.Where(g => g.InstituteId == institute.InstituteID && g.ValueItemId == valueItemId).ToListAsync();
            var oldMappings = await _context.ActivityGroupStudents.Where(gs => oldGroups.Select(g => g.Id).Contains(gs.GroupId)).ToListAsync();

            _context.ActivityGroupStudents.RemoveRange(oldMappings);
            _context.ActivityGroups.RemoveRange(oldGroups);
            await _context.SaveChangesAsync();

            // Nayi mappings add karein
            foreach (var rg in requestGroups)
            {
                // Agar group name empty hai toh default naam de dein
                string gName = string.IsNullOrWhiteSpace(rg.GroupName) ? "Group" : rg.GroupName;

                var newGroup = new ActivityGroup
                {
                    InstituteId = institute.InstituteID,
                    ValueItemId = valueItemId,
                    GroupName = gName
                };

                _context.ActivityGroups.Add(newGroup);
                await _context.SaveChangesAsync(); // Id generate karne ke liye

                // Null check lagaya hai taaki array empty hone par crash na ho
                if (rg.StudentIds != null && rg.StudentIds.Any())
                {
                    foreach (var sid in rg.StudentIds)
                    {
                        _context.ActivityGroupStudents.Add(new ActivityGroupStudent { GroupId = newGroup.Id, StudentId = sid });
                    }
                }
            }
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Teams & Students updated successfully!" });
        }

        [HttpPost("submit-activity")]
        public async Task<IActionResult> SubmitActivity([FromForm] int ValueItemId, [FromForm] string Comment, IFormFile? ImageFile, IFormFile? VideoFile)
        {
            string? currentUserId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var institute = await _context.Institutes.FirstOrDefaultAsync(i => i.AspNetUserID == currentUserId);

            if (institute == null) return BadRequest(new { success = false, message = "Institute not found." });

            // Check if submission already exists for this Capacity (ValueItemId)
            bool exists = await _context.ActivitySubmissions.AnyAsync(s => s.InstituteId == institute.InstituteID && s.ValueItemId == ValueItemId);
            if (exists) return BadRequest(new { success = false, message = "Activity already submitted for this capacity." });

            // GroupId ko by default 0 rakha gaya hai kyunki ye overall capacity submission hai
            var submission = new ActivitySubmission
            {
                InstituteId = institute.InstituteID,
                ValueItemId = ValueItemId,
                GroupId = 0,
                Comment = Comment
            };

            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "submissions");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            if (ImageFile != null)
            {
                string imgName = $"{Guid.NewGuid()}_{ImageFile.FileName}";
                using (var fs = new FileStream(Path.Combine(uploadsFolder, imgName), FileMode.Create)) { await ImageFile.CopyToAsync(fs); }
                submission.ImageUrl = $"/uploads/submissions/{imgName}";
            }
            if (VideoFile != null)
            {
                string vidName = $"{Guid.NewGuid()}_{VideoFile.FileName}";
                using (var fs = new FileStream(Path.Combine(uploadsFolder, vidName), FileMode.Create)) { await VideoFile.CopyToAsync(fs); }
                submission.VideoUrl = $"/uploads/submissions/{vidName}";
            }

            _context.ActivitySubmissions.Add(submission);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Capacity activity submitted successfully!" });
        }
    }

    // Controller class ke bahar define karein taaki 400 Bad Request na aaye
    public class SaveGroupDto
    {
        public string? GroupName { get; set; }
        public List<int>? StudentIds { get; set; }
    }
}