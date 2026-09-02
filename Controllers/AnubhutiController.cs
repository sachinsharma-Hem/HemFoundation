using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HemFoundation.Controllers
{
   
    public class AnubhutiController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly ExamBAL _examBAL;

        public AnubhutiController(IConfiguration configuration, ApplicationDbContext context, ExamBAL examBAL)
        {
            _configuration = configuration;
            _context = context;
            _examBAL = examBAL;
        }

        [HttpGet]
        public async Task<IActionResult> ThirdIndex(int courseID, int practID)
        {
            // 1. Resolve UserID safely from claims or session fallback
            int userId = 0;
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdClaim))
            {
                int.TryParse(userIdClaim, out userId);
            }
            if (userId == 0)
            {
                userId = HttpContext.Session.GetInt32("TempStudentID") ?? 1;
            }

            // 2. Language ID resolution from Session
            int languageId = HttpContext.Session.GetInt32("LanguageID") ?? 1;
            ViewBag.LanguageID = languageId;

            // 3. Get Course Name via EF Core
            var courseName = await _context.Courses
                .Where(c => c.CourseID == courseID)
                .Select(c => c.CourseName)
                .FirstOrDefaultAsync();

            ViewBag.CourseName = courseName ?? "Unknown Course";

            // 4. Get Practical Resources Path/Query via EF Core
            string practicalQuery = await _context.WJ_Practicals
                .Where(p => p.PracticalID == practID)
                .Select(p => p.Resources)
                .FirstOrDefaultAsync();

            List<WJ_Practical_ThumbnailView> pracs = new List<WJ_Practical_ThumbnailView>();
            if (!string.IsNullOrEmpty(practicalQuery) && practicalQuery != "NoFile")
            {
                pracs.Add(new WJ_Practical_ThumbnailView { Resources = practicalQuery });
            }
            ViewBag.Pracs = pracs;

            // 5. Fetch Anubhuti Master Data excluding already resolved ones via EF Core LINQ
            var resolvedAnbIds = _context.AnubhutiTransaction
                .Where(t => t.IsResolvedBy == userId)
                .Select(t => t.AnbID);

            var anubhutiList = await _context.AnubhutiMaster
                .Where(a => a.CourseID == courseID && a.AnbType == "Anubhuti2" && !resolvedAnbIds.Contains(a.AnbID))
                .ToListAsync();

            ViewData.Model = anubhutiList.Any() ? anubhutiList : string.Empty;
            ViewBag.CourseID = courseID;
            ViewBag.PracticleID = practID;

            return View();
        }

        [HttpPost]
        
        public async Task<JsonResult> SaveAnubhtiData([FromBody] AnubhutiSubmissionRequest request)
        {
            List<object> list = new List<object>();

            try
            {
                int studentId = 0;
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim)) int.TryParse(userIdClaim, out studentId);
                if (studentId == 0) studentId = HttpContext.Session.GetInt32("TempStudentID") ?? 1;

                // Step 1: Check if submission already exists
                bool alreadySubmitted = await _context.WJ_PracticalSubmission
                    .AnyAsync(s => s.StudentID == studentId && s.PracticalID == request.PracID);

                if (alreadySubmitted)
                {
                    return Json(new { success = false, message = "You have already submitted this practical." });
                }

                // Step 2: Save Anubhuti transaction data items
                if (request.cls_Anubhuti != null)
                {
                    foreach (var item in request.cls_Anubhuti)
                    {
                        var transaction = new AnubhutiTransaction
                        {
                            AnbID = item.AnbID,
                            IsResolvedBy = studentId
                        };

                        if (request.AnuType == "AN1")
                        {
                            transaction.Action1 = item.Action1;
                            transaction.Action2 = item.Action2;
                        }
                        else if (request.AnuType == "AN2")
                        {
                            transaction.Comments1 = item.Comments1;
                        }
                        else if (request.AnuType == "AN3")
                        {
                            transaction.Comments1 = item.Comments1;
                            transaction.Comments2 = item.Comments2;
                            transaction.Comments3 = item.Comments3;
                        }

                        _context.AnubhutiTransaction.Add(transaction);
                        await _context.SaveChangesAsync();
                        list.Add(0);
                    }
                }

                // Step 3 & 4: Add coins and Record Practical Submission
                string coinTag = request.AnuType == "AN1" ? "Task1-(Colour_the_face)" :
                                 request.AnuType == "AN2" ? "Task3-(Sketch_Your_Answers)" :
                                 "Task2-(BE_A_BETTER_FRIEND)";

               

                var submissionEntry = new WJ_PracticalSubmission
                {
                    StudentID = studentId,
                    PracticalID = request.PracID,
                    Status = "Approve",
                    Remarks = "yes",
                    CreatedDate = DateTime.Now
                };

                
                _context.WJ_PracticalSubmission.Add(submissionEntry);
                await _context.SaveChangesAsync();

                return Json(list);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SecondIndex(int courseID, int practID)
        {
            int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 1;
            int languageId = HttpContext.Session.GetInt32("LanguageID") ?? 1;

            try
            {

                ViewBag.LanguageID = languageId;

                var course = await _context.Courses
                    .Where(c => c.CourseID == courseID)
                    .Select(c => c.CourseName)
                    .FirstOrDefaultAsync();

                ViewBag.CourseName = course ?? "Unknown Course";

                string practicalQuery = await _context.WJ_Practicals
                    .Where(p => p.PracticalID == practID)
                    .Select(p => p.Resources)
                    .FirstOrDefaultAsync();

                List<WJ_Practical_ThumbnailView> pracs = new List<WJ_Practical_ThumbnailView>();
                if (!string.IsNullOrEmpty(practicalQuery) && practicalQuery != "NoFile")
                {
                    pracs.Add(new WJ_Practical_ThumbnailView { Resources = practicalQuery });
                }
                ViewBag.Pracs = pracs;

                var resolvedAnbIds = _context.AnubhutiTransaction
                    .Where(t => t.IsResolvedBy == userId)
                    .Select(t => t.AnbID);

                var anubhutiList = await _context.AnubhutiMaster
                    .Where(a => a.CourseID == courseID && a.AnbType == "Anubhuti3" && !resolvedAnbIds.Contains(a.AnbID))
                    .ToListAsync();

                ViewData.Model = anubhutiList;
                ViewBag.CourseID = courseID;
                ViewBag.PracticleID = practID;

            }
            catch(Exception ex)
            { }


            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AnubhutiIndex(int courseID, int practID)
        {
            int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 1;
            int languageId = HttpContext.Session.GetInt32("LanguageID") ?? 1;
            ViewBag.LanguageID = languageId;

            var courseName = await _context.Courses
                .Where(c => c.CourseID == courseID)
                .Select(c => c.CourseName)
                .FirstOrDefaultAsync();

            ViewBag.CourseName = courseName ?? "Unknown Course";

            string practicalQuery = await _context.WJ_Practicals
                .Where(p => p.PracticalID == practID)
                .Select(p => p.Resources)
                .FirstOrDefaultAsync();

            List<WJ_Practical_ThumbnailView> pracs = new List<WJ_Practical_ThumbnailView>();
            if (!string.IsNullOrEmpty(practicalQuery) && practicalQuery != "NoFile")
            {
                pracs.Add(new WJ_Practical_ThumbnailView { Resources = practicalQuery });
            }

            var resolvedAnbIds = _context.AnubhutiTransaction
                .Where(t => t.IsResolvedBy == userId)
                .Select(t => t.AnbID);

            var imageQuestions = await _context.V_HF_Anubhuti_ImageQuestionBind
                .Where(q => q.CourseID == courseID && q.AnbType == "Anubhuti1" && !resolvedAnbIds.Contains(q.AnbID))
                .ToListAsync();

            List<AnubhutiModel> anubhutiList = new List<AnubhutiModel>();

            if (imageQuestions.Any())
            {
                foreach (var item in imageQuestions)
                {
                    string smileImage = item.Smile_Image != null ? $"data:image/jpeg;base64,{Convert.ToBase64String(item.Smile_Image)}" : string.Empty;
                    string sadImage = item.Sad_Image != null ? $"data:image/jpeg;base64,{Convert.ToBase64String(item.Sad_Image)}" : string.Empty;

                    anubhutiList.Add(new AnubhutiModel
                    {
                        AnbID = item.AnbID.ToString(),
                        CourseID = courseID.ToString(),
                        AnbType = item.AnbType,
                        AnbQuestion = item.AnbQuestion,
                        Smile_Image = smileImage,
                        Sad_Image = sadImage
                    });
                }

                ViewBag.Anubhutilist = anubhutiList;
                ViewBag.CourseID = courseID;
                ViewBag.PracticleID = practID;
            }
            else
            {
                ViewBag.CourseID = courseID;
                ViewBag.PracticleID = practID;
                ViewBag.Anubhutilist = null;
                TempData["Error"] = "Upcoming Task..";
            }

            return View(pracs);
        }
    }

    public class AnubhutiSubmissionRequest
    {
        public string AnuType { get; set; }
        public int PracID { get; set; }
        public int CourseID { get; set; }
        public Cls_Anubhuti[] cls_Anubhuti { get; set; }
    }

    public class Cls_Anubhuti
    {
        public int AnbID { get; set; }
        public string Action1 { get; set; }
        public string Action2 { get; set; }
        public string Comments1 { get; set; }
        public string Comments2 { get; set; }
        public string Comments3 { get; set; }
    }
}