using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Route("api/course-hub")]
    [ApiController]
    public class CourseHubApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CourseHubApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("dropdowns")]
        public async Task<IActionResult> GetDropdowns()
        {
            try
            {
                return Ok(new
                {
                    success = true,
                    years = await _context.CourseYears.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    mainCourses = await _context.MainCourses.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    categories = await _context.CourseCategories.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    subCategories = await _context.CourseSubCategories.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    orderNumbers = await _context.CourseOrderNumbers.Where(x => !x.IsDeleted && x.IsActive).OrderBy(x => x.DisplayOrder).ToListAsync(),
                    standards = await _context.StandardMasters.Where(x => x.IsActive).ToListAsync(),
                    courses = await _context.CourseRegistrations.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    quizzes = await _context.CourseQuizzes.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    authors = await _context.CourseAuthors.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    managers = await _context.CourseManagers.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    coursesFor = await _context.CourseFors.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    customerTypes = await _context.CourseCustomerTypes.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    durationTypes = await _context.CourseDurationTypes.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    allotedTos = await _context.CourseAllotedTos.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    mcqLevels = await _context.CourseMcqLevels.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    levels = await _context.CourseLevels.Where(x => !x.IsDeleted && x.IsActive).ToListAsync()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error loading dropdowns: " + ex.Message });
            }
        }

        // --- 1. SYLLABUS ---
        [HttpGet("syllabus/list")]
        public async Task<IActionResult> GetSyllabusList() => Ok(new { success = true, data = await _context.CourseSyllabuses.ToListAsync() });

        [HttpPost("syllabus/save")]
        public async Task<IActionResult> SaveSyllabus([FromBody] CourseSyllabus model)
        {
            if (model == null) return BadRequest(new { success = false, message = "Invalid payload." });
            try
            {
                if (model.Id == 0) { model.CreatedDate = DateTime.UtcNow; _context.CourseSyllabuses.Add(model); }
                else
                {
                    var item = await _context.CourseSyllabuses.FindAsync(model.Id);
                    if (item == null) return NotFound(new { success = false, message = "Record not found." });
                    item.CourseYearId = model.CourseYearId; item.MainCourseId = model.MainCourseId; item.CourseCategoryId = model.CourseCategoryId; item.CourseSubCategoryId = model.CourseSubCategoryId; item.DisplayOrderId = model.DisplayOrderId; item.CourseId = model.CourseId; item.QuizId = model.QuizId; item.StandardMasterId = model.StandardMasterId; item.IsForTeachers = model.IsForTeachers; item.IsForAll = model.IsForAll; item.Remarks = model.Remarks; item.ModifiedDate = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Syllabus saved successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPatch("syllabus/toggle/{id}")]
        public async Task<IActionResult> ToggleSyllabus(int id)
        {
            var item = await _context.CourseSyllabuses.FindAsync(id);
            if (item != null) { item.IsActive = !item.IsActive; await _context.SaveChangesAsync(); }
            return Ok(new { success = true });
        }

        [HttpDelete("syllabus/delete/{id}")]
        public async Task<IActionResult> DeleteSyllabus(int id)
        {
            var item = await _context.CourseSyllabuses.FindAsync(id);
            if (item != null) { item.IsDeleted = true; await _context.SaveChangesAsync(); }
            return Ok(new { success = true, message = "Deleted successfully!" });
        }

        // --- 2. COURSES ---
        [HttpGet("course/list")]
        public async Task<IActionResult> GetCourseList() => Ok(new { success = true, data = await _context.Courses.Where(x => x.IsActive).ToListAsync() });

        [HttpPost("course/save")]
        public async Task<IActionResult> SaveCourse([FromBody] CourseRegistration model)
        {
            if (model == null) return BadRequest(new { success = false, message = "Invalid payload." });
            try
            {
                if (model.ExpectedDeliveryDate.Kind == DateTimeKind.Unspecified) model.ExpectedDeliveryDate = DateTime.SpecifyKind(model.ExpectedDeliveryDate, DateTimeKind.Utc);
                else model.ExpectedDeliveryDate = model.ExpectedDeliveryDate.ToUniversalTime();

                if (model.Id == 0) { model.CreatedDate = DateTime.UtcNow; _context.CourseRegistrations.Add(model); }
                else
                {
                    var item = await _context.CourseRegistrations.FindAsync(model.Id);
                    if (item == null) return NotFound(new { success = false, message = "Record not found." });
                    item.CourseName = model.CourseName; item.ExpectedDeliveryDate = model.ExpectedDeliveryDate; item.FromAge = model.FromAge; item.ToAge = model.ToAge; item.NumberOfSessions = model.NumberOfSessions; item.CourseAuthorId = model.CourseAuthorId; item.CourseManagerId = model.CourseManagerId; item.MainCourseId = model.MainCourseId; item.CourseForId = model.CourseForId; item.CourseCustomerTypeId = model.CourseCustomerTypeId; item.Description = model.Description; item.ModifiedDate = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Course registered successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPatch("course/toggle/{id}")]
        public async Task<IActionResult> ToggleCourse(int id)
        {
            var item = await _context.CourseRegistrations.FindAsync(id);
            if (item != null) { item.IsActive = !item.IsActive; await _context.SaveChangesAsync(); }
            return Ok(new { success = true });
        }

        [HttpDelete("course/delete/{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var item = await _context.CourseRegistrations.FindAsync(id);
            if (item != null) { item.IsDeleted = true; await _context.SaveChangesAsync(); }
            return Ok(new { success = true, message = "Deleted successfully!" });
        }

        // --- 3. QUIZZES ---
        [HttpGet("quiz/list")]
        public async Task<IActionResult> GetQuizList() => Ok(new { success = true, data = await _context.CourseQuizzes.Where(x => !x.IsDeleted).ToListAsync() });

        [HttpPost("quiz/save")]
        public async Task<IActionResult> SaveQuiz([FromBody] CourseQuiz model)
        {
            if (model == null) return BadRequest(new { success = false, message = "Invalid payload." });
            try
            {
                if (model.StartDate.Kind == DateTimeKind.Unspecified) model.StartDate = DateTime.SpecifyKind(model.StartDate, DateTimeKind.Utc);
                else model.StartDate = model.StartDate.ToUniversalTime();
                if (model.EndDate.Kind == DateTimeKind.Unspecified) model.EndDate = DateTime.SpecifyKind(model.EndDate, DateTimeKind.Utc);
                else model.EndDate = model.EndDate.ToUniversalTime();

                if (model.Id == 0) { model.CreatedDate = DateTime.UtcNow; _context.CourseQuizzes.Add(model); }
                else
                {
                    var item = await _context.CourseQuizzes.FindAsync(model.Id);
                    if (item == null) return NotFound(new { success = false, message = "Record not found." });
                    item.QuizName = model.QuizName; item.CourseId = model.CourseId; item.StartDate = model.StartDate; item.EndDate = model.EndDate; item.StartTime = model.StartTime; item.EndTime = model.EndTime; item.CourseDurationTypeId = model.CourseDurationTypeId; item.PassingMarks = model.PassingMarks; item.TotalMarks = model.TotalMarks; item.NoOfAttemptsAllowed = model.NoOfAttemptsAllowed; item.CourseAllotedToId = model.CourseAllotedToId; item.CourseMcqLevelId = model.CourseMcqLevelId; item.CourseLevelId = model.CourseLevelId; item.ExamActive = model.ExamActive; item.ModifiedDate = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Quiz created successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPatch("quiz/toggle/{id}")]
        public async Task<IActionResult> ToggleQuiz(int id)
        {
            var item = await _context.CourseQuizzes.FindAsync(id);
            if (item != null) { item.IsActive = !item.IsActive; await _context.SaveChangesAsync(); }
            return Ok(new { success = true });
        }

        [HttpDelete("quiz/delete/{id}")]
        public async Task<IActionResult> DeleteQuiz(int id)
        {
            var item = await _context.CourseQuizzes.FindAsync(id);
            if (item != null) { item.IsDeleted = true; await _context.SaveChangesAsync(); }
            return Ok(new { success = true, message = "Deleted successfully!" });
        }
    }
}