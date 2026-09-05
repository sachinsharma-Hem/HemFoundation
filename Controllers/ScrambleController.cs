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

            try
            {
                // Language ID resolution from Session
                int languageId = HttpContext.Session.GetInt32("LanguageID") ?? 1;
                ViewBag.LanguageID = languageId;

                // Resolve UserID safely from claims or session fallback
               
                int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 0;

                if (userId == 0)
                {
                    var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!string.IsNullOrEmpty(userIdClaim))
                    {
                        int.TryParse(userIdClaim, out userId);
                    }
                }
                if (userId == null || userId == 0)
                {
                    return RedirectToAction("Login", "Account"); // Adjust controller/action names to match your login route
                }

                // 1. Resolve answered question IDs safely by forcing integer evaluation first
                var answeredQuestionIds = _context.WJ_ScrambleAnswer
                    .Where(a => a.UserId == userId)
                    .Select(a => a.QuestionId)
                    .ToList(); // Materialize list into memory to prevent expression tree parameter failures

                // 2. Query using the local list array
                var scrambleQuestion = await _context.WJ_ScrambleQuestion
                    .Where(q => q.CourseID == courseID && !answeredQuestionIds.Contains(q.Id))
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
            catch (Exception ex)
            {
                // Safe handling avoiding null reference on InnerException
                string errorMsg = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMsg += " " + ex.InnerException.Message;
                }
                TempData["Message"] = errorMsg;
                TempData["MessageType"] = "error";
            }

            return View(model);
        }

        [HttpGet]
        
        public async Task<JsonResult> AddAnswer(int questionID, int scrambleTopicId, string answer, int practID)
        {
            var courseIdVal = TempData["CourseID"];
            int courseId = courseIdVal != null ? Convert.ToInt32(courseIdVal) : 0;

            int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 0;

            if (userId == 0)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    int.TryParse(userIdClaim, out userId);
                }
            }
            if (userId == null || userId == 0)
            {
                TempData["Message"] = "Session expired. Please login again.";
                TempData["MessageType"] = "info";
                return Json(new { Success = false, Message = "Session expired. Please login again." }); // Adjust controller/action names to match your login route
            }


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
              
                _context.WJ_PracticalSubmission.Add(practicalSubmission);

                await _context.SaveChangesAsync();

                TempData["Message"] = "Answer saved successfully!!";
                TempData["MessageType"] = "success";
                return Json(new { Success = true, Message = "Answer saved successfully", Data = (object)null });
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMsg += " " + ex.InnerException.Message;
                }
                TempData["Message"] = errorMsg;
                TempData["MessageType"] = "error";
                return Json(new { Success = false, Message = errorMsg, Data = (object)null });
            }
        }
    }
}