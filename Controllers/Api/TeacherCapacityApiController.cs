using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Route("api/teacher-capacity")]
    [ApiController]
    public class TeacherCapacityApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TeacherCapacityApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("details/{categoryId}")]
        public async Task<IActionResult> GetCapacityDetails(int categoryId)
        {
            var category = await _context.WebinarMcqCategories
                .FirstOrDefaultAsync(x => x.Id == categoryId && !x.IsDeleted);

            if (category == null) return NotFound(new { success = false, message = "Category not found" });

            var matchedCourse = await _context.CourseRegistrations
                .FirstOrDefaultAsync(c => c.CourseName.ToLower().Contains(category.Name.ToLower()) && !c.IsDeleted);

            int courseRegId = matchedCourse != null ? matchedCourse.Id : category.Id;

            int topicsCount = await _context.CourseTopics.CountAsync(x => x.CourseRegistrationId == courseRegId && !x.IsDeleted);
            int subTopicsCount = await _context.CourseSubTopics.CountAsync(x => x.CourseRegistrationId == courseRegId && !x.IsDeleted);

            var subTopics = await _context.CourseSubTopics
                .Where(x => x.CourseRegistrationId == courseRegId && !x.IsDeleted)
                .OrderBy(x => x.DisplayOrderId)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                courseRegId = courseRegId,
                category = new
                {
                    category.Name,
                    category.Description,
                    ImageUrl = category.ImageUrl ?? "/images/default-course.png"
                },
                counts = new { topicsCount, subTopicsCount },
                subTopics
            });
        }

        [HttpGet("subtopic-questions/{subTopicId}")]
        public async Task<IActionResult> GetSubTopicQuestions(int subTopicId)
        {
            int teacherId = 0;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.AspNetUserID == userId);
                if (teacher != null) teacherId = teacher.TeacherID;
            }

            if (teacherId == 0)
            {
                var sessionUserJson = HttpContext.Session.GetString("user_session");
                if (!string.IsNullOrEmpty(sessionUserJson))
                {
                    try
                    {
                        dynamic userObj = Newtonsoft.Json.JsonConvert.DeserializeObject(sessionUserJson);
                        string email = userObj?.email;
                        if (!string.IsNullOrEmpty(email))
                        {
                            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.EmailID == email || t.AspNetUserID == email);
                            if (teacher != null) teacherId = teacher.TeacherID;
                        }
                    }
                    catch { }
                }
            }

            var subTopic = await _context.CourseSubTopics.FindAsync(subTopicId);
            if (subTopic == null) return NotFound(new { success = false, message = "Subtopic not found" });

            var attemptedQuestionIds = await _context.TeacherQuizAttempts
                .Where(x => x.TeacherId == teacherId && x.CourseSubTopicId == subTopicId && !x.IsDeleted)
                .Select(x => x.CourseMcqQuestionId)
                .ToListAsync();

            var questions = await _context.CourseMcqQuestions
                .Where(x => x.CourseSubTopicId == subTopicId && !x.IsDeleted && !attemptedQuestionIds.Contains(x.Id))
                .Take(subTopic.NumberOfQuestions > 0 ? subTopic.NumberOfQuestions : 10)
                .Select(q => new {
                    q.Id,
                    q.Question,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD
                })
                .ToListAsync();

            bool allCompleted = questions.Count == 0;

            return Ok(new { success = true, allCompleted, subTopicTitle = subTopic.SubTopicName, questions });
        }

        // Summary Result Endpoint
        [HttpGet("quiz-summary/{subTopicId}")]
        public async Task<IActionResult> GetQuizSummary(int subTopicId)
        {
            int teacherId = 0;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.AspNetUserID == userId);
                if (teacher != null) teacherId = teacher.TeacherID;
            }

            var attempts = await _context.TeacherQuizAttempts
                .Where(x => x.TeacherId == teacherId && x.CourseSubTopicId == subTopicId && !x.IsDeleted)
                .ToListAsync();

            int totalAttempted = attempts.Count;
            int correctCount = attempts.Count(x => x.IsCorrect);
            int wrongCount = totalAttempted - correctCount;
            int totalTimeSpent = attempts.Sum(x => x.TimeSpentSeconds);

            return Ok(new
            {
                success = true,
                totalAttempted,
                correctCount,
                wrongCount,
                totalTimeSpent
            });
        }

        [HttpPost("submit-answer")]
        public async Task<IActionResult> SubmitAnswer([FromBody] TeacherQuizAttempt model)
        {
            int teacherId = 0;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.AspNetUserID == userId);
                if (teacher != null) teacherId = teacher.TeacherID;
            }

            if (teacherId == 0)
            {
                var sessionUserJson = HttpContext.Session.GetString("user_session");
                if (!string.IsNullOrEmpty(sessionUserJson))
                {
                    try
                    {
                        dynamic userObj = Newtonsoft.Json.JsonConvert.DeserializeObject(sessionUserJson);
                        string email = userObj?.email;
                        if (!string.IsNullOrEmpty(email))
                        {
                            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.EmailID == email || t.AspNetUserID == email);
                            if (teacher != null) teacherId = teacher.TeacherID;
                        }
                    }
                    catch { }
                }
            }

            if (teacherId == 0) return Unauthorized(new { success = false, message = "Authentication failed." });

            model.TeacherId = teacherId;

            var question = await _context.CourseMcqQuestions.FindAsync(model.CourseMcqQuestionId);
            if (question != null && question.RightAnswer != null)
            {
                model.IsCorrect = question.RightAnswer.Equals(model.SelectedOption, StringComparison.OrdinalIgnoreCase);
            }

            model.CreatedDate = DateTime.UtcNow;
            _context.TeacherQuizAttempts.Add(model);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, isCorrect = model.IsCorrect });
        }
    }
}