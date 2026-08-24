using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Route("api/course-authoring")]
    [ApiController]
    public class CourseAuthoringApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CourseAuthoringApiController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("search-courses")]
        public async Task<IActionResult> SearchCourses([FromQuery] string query)
        {
            var q = query?.ToLower() ?? "";
            var courses = await _context.CourseRegistrations
                .Where(x => !x.IsDeleted && x.IsActive && x.CourseName.ToLower().Contains(q))
                .Take(10)
                .Select(x => new { x.Id, x.CourseName, x.CreatedDate, x.ExpectedDeliveryDate, x.Description, x.FromAge, x.ToAge, x.NumberOfSessions })
                .ToListAsync();
            return Ok(new { success = true, data = courses });
        }

        [HttpGet("dropdowns/{courseId}")]
        public async Task<IActionResult> GetCourseDropdowns(int courseId)
        {
            return Ok(new
            {
                success = true,
                orderNumbers = await _context.CourseOrderNumbers.Where(x => !x.IsDeleted && x.IsActive).OrderBy(x => x.DisplayOrder).ToListAsync(),
                topics = await _context.CourseTopics.Where(x => !x.IsDeleted && x.CourseRegistrationId == courseId).ToListAsync(),
                subTopics = await _context.CourseSubTopics.Where(x => !x.IsDeleted && x.CourseRegistrationId == courseId).ToListAsync(),
                mcqLevels = await _context.CourseMcqLevels.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                standards = await _context.StandardMasters.Where(x => x.IsActive).ToListAsync()
            });
        }

        // ================= 1. TOPIC ENDPOINTS =================
        [HttpGet("topic/list/{courseId}")]
        public async Task<IActionResult> GetTopics(int courseId) => Ok(new { success = true, data = await _context.CourseTopics.Where(x => !x.IsDeleted && x.CourseRegistrationId == courseId).ToListAsync() });

        [HttpPost("topic/save")]
        public async Task<IActionResult> SaveTopic([FromBody] CourseTopic model)
        {
            if (model.Id == 0) { model.CreatedDate = DateTime.UtcNow; _context.CourseTopics.Add(model); }
            else { var item = await _context.CourseTopics.FindAsync(model.Id); if (item == null) return NotFound(); item.TopicName = model.TopicName; item.Description = model.Description; item.DisplayOrderId = model.DisplayOrderId; item.Language = model.Language; item.ModifiedDate = DateTime.UtcNow; }
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Topic saved successfully!" });
        }

        [HttpDelete("topic/delete/{id}")]
        public async Task<IActionResult> DeleteTopic(int id)
        {
            var item = await _context.CourseTopics.FindAsync(id);
            if (item != null) { item.IsDeleted = true; await _context.SaveChangesAsync(); }
            return Ok(new { success = true, message = "Deleted successfully!" });
        }

        // ================= 2. SUB-TOPIC ENDPOINTS =================
        [HttpGet("subtopic/list/{courseId}")]
        public async Task<IActionResult> GetSubTopics(int courseId) => Ok(new { success = true, data = await _context.CourseSubTopics.Where(x => !x.IsDeleted && x.CourseRegistrationId == courseId).ToListAsync() });

        [HttpPost("subtopic/save")]
        public async Task<IActionResult> SaveSubTopic([FromForm] int id, [FromForm] int courseRegistrationId, [FromForm] int courseTopicId, [FromForm] string subTopicName, [FromForm] int displayOrderId, [FromForm] string? videoLink, [FromForm] int numberOfQuestions, [FromForm] IFormFile? mcqThumbFile, [FromForm] IFormFile? videoThumbFile)
        {
            string mcqThumbUrl = null, videoThumbUrl = null;
            string uploadDir = Path.Combine(_env.WebRootPath, "uploads", "subtopics");
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

            if (mcqThumbFile != null)
            {
                string name = Guid.NewGuid() + "_" + mcqThumbFile.FileName;
                using (var s = new FileStream(Path.Combine(uploadDir, name), FileMode.Create)) await mcqThumbFile.CopyToAsync(s);
                mcqThumbUrl = $"/uploads/subtopics/{name}";
            }
            if (videoThumbFile != null)
            {
                string name = Guid.NewGuid() + "_" + videoThumbFile.FileName;
                using (var s = new FileStream(Path.Combine(uploadDir, name), FileMode.Create)) await videoThumbFile.CopyToAsync(s);
                videoThumbUrl = $"/uploads/subtopics/{name}";
            }

            if (id == 0)
            {
                var st = new CourseSubTopic { CourseRegistrationId = courseRegistrationId, CourseTopicId = courseTopicId, SubTopicName = subTopicName, DisplayOrderId = displayOrderId, VideoLink = videoLink, NumberOfQuestions = numberOfQuestions, McqThumbnail = mcqThumbUrl, VideoThumbnail = videoThumbUrl, CreatedDate = DateTime.UtcNow };
                _context.CourseSubTopics.Add(st);
            }
            else
            {
                var st = await _context.CourseSubTopics.FindAsync(id);
                if (st == null) return NotFound();
                st.CourseTopicId = courseTopicId; st.SubTopicName = subTopicName; st.DisplayOrderId = displayOrderId; st.VideoLink = videoLink; st.NumberOfQuestions = numberOfQuestions;
                if (mcqThumbUrl != null) st.McqThumbnail = mcqThumbUrl;
                if (videoThumbUrl != null) st.VideoThumbnail = videoThumbUrl;
                st.ModifiedDate = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Sub-Topic saved successfully!" });
        }

        [HttpDelete("subtopic/delete/{id}")]
        public async Task<IActionResult> DeleteSubTopic(int id)
        {
            var item = await _context.CourseSubTopics.FindAsync(id);
            if (item != null) { item.IsDeleted = true; await _context.SaveChangesAsync(); }
            return Ok(new { success = true, message = "Deleted successfully!" });
        }

        // ================= 3. MCQ ENDPOINTS =================
        [HttpGet("mcq/list/{courseId}")]
        public async Task<IActionResult> GetMcqs(int courseId) => Ok(new { success = true, data = await _context.CourseMcqQuestions.Where(x => !x.IsDeleted && x.CourseRegistrationId == courseId).ToListAsync() });

        [HttpPost("mcq/save")]
        public async Task<IActionResult> SaveMcq([FromBody] CourseMcqQuestion model)
        {
            if (model.Id == 0) { model.CreatedDate = DateTime.UtcNow; _context.CourseMcqQuestions.Add(model); }
            else { var item = await _context.CourseMcqQuestions.FindAsync(model.Id); if (item == null) return NotFound(); item.CourseTopicId = model.CourseTopicId; item.CourseSubTopicId = model.CourseSubTopicId; item.CourseMcqLevelId = model.CourseMcqLevelId; item.Question = model.Question; item.OptionA = model.OptionA; item.OptionB = model.OptionB; item.OptionC = model.OptionC; item.OptionD = model.OptionD; item.RightAnswer = model.RightAnswer; item.ModifiedDate = DateTime.UtcNow; }
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "MCQ saved successfully!" });
        }

        [HttpDelete("mcq/delete/{id}")]
        public async Task<IActionResult> DeleteMcq(int id)
        {
            var item = await _context.CourseMcqQuestions.FindAsync(id);
            if (item != null) { item.IsDeleted = true; await _context.SaveChangesAsync(); }
            return Ok(new { success = true, message = "Deleted successfully!" });
        }

        // ================= 4. PRACTICAL LAB ENDPOINTS =================
        [HttpGet("lab/list/{courseId}")]
        public async Task<IActionResult> GetLabs(int courseId) => Ok(new { success = true, data = await _context.CoursePracticalLabs.Where(x => !x.IsDeleted && x.CourseRegistrationId == courseId).ToListAsync() });

        [HttpPost("lab/save")]
        public async Task<IActionResult> SaveLab([FromForm] int id, [FromForm] int courseRegistrationId, [FromForm] string title, [FromForm] string? description, [FromForm] int coins, [FromForm] int gems, [FromForm] string practicalTopic, [FromForm] int standardMasterId, [FromForm] int displayOrderId, [FromForm] string? videoLink, [FromForm] IFormFile? thumbnailFile, [FromForm] IFormFile? resourceFile)
        {
            string thumbUrl = null, resUrl = null;
            string uploadDir = Path.Combine(_env.WebRootPath, "uploads", "labs");
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

            if (thumbnailFile != null)
            {
                string tName = Guid.NewGuid() + "_" + thumbnailFile.FileName;
                using (var s = new FileStream(Path.Combine(uploadDir, tName), FileMode.Create)) await thumbnailFile.CopyToAsync(s);
                thumbUrl = $"/uploads/labs/{tName}";
            }
            if (resourceFile != null)
            {
                string rName = Guid.NewGuid() + "_" + resourceFile.FileName;
                using (var s = new FileStream(Path.Combine(uploadDir, rName), FileMode.Create)) await resourceFile.CopyToAsync(s);
                resUrl = $"/uploads/labs/{rName}";
            }

            if (id == 0)
            {
                var lab = new CoursePracticalLab { CourseRegistrationId = courseRegistrationId, Title = title, Description = description, Coins = coins, Gems = gems, PracticalTopic = practicalTopic, StandardMasterId = standardMasterId, DisplayOrderId = displayOrderId, VideoLink = videoLink, ThumbnailImageUrl = thumbUrl, ResourceFileUrl = resUrl, CreatedDate = DateTime.UtcNow };
                _context.CoursePracticalLabs.Add(lab);
            }
            else
            {
                var lab = await _context.CoursePracticalLabs.FindAsync(id);
                if (lab == null) return NotFound();
                lab.Title = title; lab.Description = description; lab.Coins = coins; lab.Gems = gems; lab.PracticalTopic = practicalTopic; lab.StandardMasterId = standardMasterId; lab.DisplayOrderId = displayOrderId; lab.VideoLink = videoLink;
                if (thumbUrl != null) lab.ThumbnailImageUrl = thumbUrl;
                if (resUrl != null) lab.ResourceFileUrl = resUrl;
                lab.ModifiedDate = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Practical Lab saved successfully!" });
        }

        [HttpDelete("lab/delete/{id}")]
        public async Task<IActionResult> DeleteLab(int id)
        {
            var item = await _context.CoursePracticalLabs.FindAsync(id);
            if (item != null) { item.IsDeleted = true; await _context.SaveChangesAsync(); }
            return Ok(new { success = true, message = "Deleted successfully!" });
        }
    }
}