using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HemFoundation.Models;

namespace HemFoundation.Controllers
{
    
    public class ScrambleController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ScrambleController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int courseID, int practID)
        {
            TempData["CourseID"] = courseID;
            ScrambleModel model = new ScrambleModel();

            try { 

            // Language ID resolution from Session
            int languageId = HttpContext.Session.GetInt32("LanguageID") ?? 1;
            ViewBag.LanguageID = languageId;

            // Resolve UserID safely from claims or session fallback
            int userId = 0;
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdClaim)) int.TryParse(userIdClaim, out userId);
            if (userId == 0) userId = HttpContext.Session.GetInt32("TempStudentID") ?? 1;

           

            // 1. Replicating legacy query: select TopicId FROM WJ_ScrambleQuestion where Id not in (select QuestionId from WJ_ScrambleAnswer where UserId = @UserID) and CourseID = @CourseID
            var answeredQuestionIds = _context.WJ_ScrambleAnswer
                .Where(a => a.UserId == userId)
                .Select(a => a.QuestionId);

            var scrambleQuestion = await _context.WJ_ScrambleQuestion
                .Where(q => q.Id == courseID && !answeredQuestionIds.Contains(q.Id))
                .FirstOrDefaultAsync();

            if (scrambleQuestion != null)
            {
                int topicId = scrambleQuestion.TopicId;

                // 2. Fetch Stored Procedure results via EF Core (PostgreSQL compatible mapping)
                model.scrambleWord_Results = await _context.SP_ScrambleWord_Results
                    .FromSqlRaw("SELECT * FROM SP_ScrambleWord({0})", topicId)
                    .ToListAsync();

                if (model.scrambleWord_Results.Any())
                {
                    var firstResult = model.scrambleWord_Results.First();
                    model.ScrambleTopic = firstResult.ScrambleTopic;
                    model.TopicId = firstResult.TopicId;
                }

                // Populate ViewBag fields corresponding to the exact un-answered question found
                ViewBag.QuestionID = scrambleQuestion.Id;
                ViewBag.TopicId = scrambleQuestion.TopicId;
                ViewBag.Question = scrambleQuestion.Question;
                ViewBag.Answer = scrambleQuestion.Answer;
            }
            else
            {
                TempData["Message"] = "All scramble questions are already answered or not available.";
                TempData["MessageType"] = "info";
            }

            // 3. Fetch Course Name via EF Core
            var courseName = await _context.Courses
                .Where(c => c.CourseID == courseID)
                .Select(c => c.CourseName)
                .FirstOrDefaultAsync();

            ViewBag.CourseName = courseName ?? "Unknown Course";
            ViewBag.CourseID = courseID;
            ViewBag.PracticleID = practID;
            }
            catch (Exception ex) {
                TempData["Message"] = ex.Message.ToString()+ex.InnerException.Message.ToString();
                TempData["MessageType"] = "error";
            }
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public async Task<JsonResult> AddAnswer(int questionID, int scrambleTopicId, string answer, int practID)
        {
            var courseIdVal = TempData["CourseID"];
            int courseId = courseIdVal != null ? Convert.ToInt32(courseIdVal) : 0;

            int userId = 0;
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdClaim)) int.TryParse(userIdClaim, out userId);
            if (userId == 0) userId = HttpContext.Session.GetInt32("TempStudentID") ?? 1;

            try
            {
                // Check if already submitted for the same PracticalID and StudentID
                bool alreadySubmitted = await _context.WJ_PracticalSubmission
                    .AnyAsync(p => p.PracticalID == practID && p.StudentID == userId);

                if (alreadySubmitted)
                {
                    TempData["Message"] = "All scramble questions are already answered!!";
                    TempData["MessageType"] = "info";
                    return Json(new { Success = false, Message = "Answer already submitted." });
                }

                // Add Scramble Answer Record
                var scrambleAnswer = new WJ_ScrambleAnswer
                {
                    Answer = answer,
                    CreatedDate = DateTime.Now,
                    QuestionId = questionID,
                    ScrambleTopicId = scrambleTopicId,
                    UserId = userId
                };

                // Add Coin Reward
                var coins = new WJ_Coins
                {
                    StudentID = userId,
                    CoinFor = "Scramble",
                    Coins = 2,
                    ReferenceID = courseId,
                    CreatedOn = DateTime.Now
                };

                // Record Practical Submission
                var practicalSubmission = new WJ_PracticalSubmission
                {
                    PracticalID = practID,
                    StudentID = userId,
                    Status = "Approve",
                    Remarks = "yes",
                    CreatedDate = DateTime.Now
                };

                _context.WJ_ScrambleAnswer.Add(scrambleAnswer);
                _context.WJ_Coins.Add(coins);
                _context.WJ_PracticalSubmission.Add(practicalSubmission);

                await _context.SaveChangesAsync();

                TempData["Message"] = "Answer saved successfully!!";
                TempData["MessageType"] = "success";
                return Json(new { Success = true, Message = "Answer saved successfully", Data = (object)null });
            }
            catch (Exception ex)
            {
                return Json(new { Success = false, Message = ex.Message, Data = (object)null });
            }
        }
    }
}