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
            // categoryId yaha par CourseID ki tarah kaam karega

            // 1. Main Topic nikalna (Jiska ParentTopicID null hai)
            var mainTopic = await _context.Topics
                .FirstOrDefaultAsync(x => x.CourseID == categoryId && x.ParentTopicID == null);

            if (mainTopic == null)
                return NotFound(new { success = false, message = "Topic not found for this course." });

            // 2. Sub-Topics nikalna (Jinka ParentTopicID null NAHI hai)
            var subTopicsData = await _context.Topics
                .Where(x => x.CourseID == categoryId && x.ParentTopicID != null)
                .OrderBy(x => x.Order)
                .ToListAsync();

            int topicsCount = 1; // Main topic ek hi hai
            int subTopicsCount = subTopicsData.Count;

            // 3. Frontend (JS) ko jo format chahiye usme map karna
            var subTopics = subTopicsData.Select(x => new
            {
                id = x.TopicID,
                subTopicName = x.TopicName,
                videoLink = x.TrainerVideo,          // First card me video play karne ke liye
                mcqThumbnail = x.TrainerExercise,    // Banner/Image ke liye
                numberOfQuestions = x.NoOfQuestion ?? 0,
                languageId = x.languageID ?? 1
            }).ToList();

            return Ok(new
            {
                success = true,
                courseRegId = categoryId,
                category = new
                {
                    Name = mainTopic.TopicName,
                    Description = mainTopic.Description,
                    ImageUrl = "/images/default-course.png" // Agar topic me image ka column nahi hai to default bhejenge
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