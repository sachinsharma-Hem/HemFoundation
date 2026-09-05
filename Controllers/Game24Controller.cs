using DocumentFormat.OpenXml.Spreadsheet;
using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HemFoundation.Controllers
{
    public class Game24Controller : Controller
    {
        private readonly ApplicationDbContext _context;

        public Game24Controller(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Game24
        [HttpGet]
        public async Task<IActionResult> Index(int CourseID, int PractID, string Coursename)
        {
            int languageId = HttpContext.Session.GetInt32("LanguageID") ?? 1;
            ViewBag.LanguageID = languageId;
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


            // Fetch solved game IDs using standard DbSet names
            var solvedGameIds = await _context.Game24Solution
                .Where(s => s.IsCreatedBy == userId && s.CourseID==CourseID)
                .Select(s => s.Game24ID)
                .ToListAsync();

            var gameList = await _context.WJ_Game24
                .Where(g => g.CourseID == CourseID && !solvedGameIds.Contains(g.Game24ID))
                .Select(g => new Game24
                {
                    Game24ID = g.Game24ID,
                    GameTopicID = g.GameTopicID,
                    Number1 = g.Number1,
                    Number2 = g.Number2,
                    Number3 = g.Number3,
                    Number4 = g.Number4
                })
                .ToListAsync();

            ViewData.Model = gameList;

            var courseName = await _context.Courses
                .Where(c => c.CourseID == CourseID)
                .Select(c => c.CourseName)
                .FirstOrDefaultAsync();

            ViewBag.CourseName = string.IsNullOrEmpty(Coursename) ? (courseName ?? "Unknown Course") : Coursename;
            ViewBag.CourseName1 = ViewBag.CourseName;
            ViewBag.CourseID = CourseID;
            ViewBag.PracticleID = PractID;

            return View();
        }

        [HttpPost]
        public async Task<JsonResult> Save24(string Game24ID, string GameTopicID, string CID, string SolutionAnswers, string PracID)
        {
            List<object> list = new List<object>();
            try
            {
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
                    
                    list.Add("0");
                    list.Add("Session expired. Please login again.");
                    return Json(list);
                }

                int pracIdInt = Convert.ToInt32(PracID);
                int game24IdInt = Convert.ToInt32(Game24ID);
                int gameTopicIdInt = Convert.ToInt32(GameTopicID);
                int courseIdInt = Convert.ToInt32(CID);

                int isAlreadySubmitted = await _context.WJ_PracticalSubmission
                    .CountAsync(p => p.StudentID == userId && p.PracticalID == pracIdInt);

                if (isAlreadySubmitted > 0)
                {
                    list.Add("0");
                    list.Add("This practical has already been submitted.");
                    return Json(list);
                }

                // Update game status
                var gameItem = await _context.WJ_Game24
                    .FirstOrDefaultAsync(g => g.Game24ID == game24IdInt && g.GameTopicID == gameTopicIdInt);
                if (gameItem != null)
                {
                    gameItem.GameIsActive = true;
                }

                // Insert Solution
                var solution = new Game24Solution
                {
                    Game24ID = game24IdInt,
                    EntryDate = DateTime.Now,
                    SolutionAnswers = SolutionAnswers,
                    IsCreatedBy = userId,
                    CourseID = courseIdInt
                };
                _context.Game24Solution.Add(solution);

                // Insert Coins (Mapping to your exact property setup)
                //var coinRecord = new WJ_Coins
                //{
                //    StudentID = userId,
                //    CoinFor = "Game24",
                //    ReferenceID = courseIdInt, // assign int, not string
                //    Coins = 1,
                //    CreatedOn = DateTime.Now,
                //    // If your WJ_Coins model uses TopicID instead of ExamTopicID, update this line:
                //    // TopicID = gameTopicIdInt 
                //};
                //_context.WJ_Coins.Add(coinRecord);

                await _context.SaveChangesAsync();

                int maxGameId = await _context.WJ_Game24
                    .Where(g => g.CourseID == courseIdInt)
                    .MaxAsync(g => (int?)g.Game24ID) ?? 0;

                if (game24IdInt == maxGameId)
                {
                    var practicalSubmission = new WJ_PracticalSubmission
                    {
                        StudentID = userId,
                        PracticalID = pracIdInt,
                        Status = "Approve",
                        Remarks = "yes",
                        CreatedDate = DateTime.Now
                    };
                    _context.WJ_PracticalSubmission.Add(practicalSubmission);
                    await _context.SaveChangesAsync();

                    list.Add("1");
                    list.Add("Game has been completed Successfully....");
                }
                else
                {
                    list.Add("2");
                    list.Add("Record save Successfully....");
                }
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMsg += " " + ex.InnerException.Message;
                }
                list.Add("Something went wrong...");
                list.Add(errorMsg);
               
            }
            return Json(list);
        }

        [HttpPost]
        public async Task<JsonResult> Save24ForSkip(string Game24ID, string GameTopicID, string CID, string SolutionAnswers, string PracID)
        {
            List<object> list = new List<object>();
            try
            {
                int userId = 1;
                int.TryParse(HttpContext.Session.GetString("UserId"), out userId);
                int pracIdInt = Convert.ToInt32(PracID);
                int game24IdInt = Convert.ToInt32(Game24ID);
                int gameTopicIdInt = Convert.ToInt32(GameTopicID);
                int courseIdInt = Convert.ToInt32(CID);

                int isAlreadySubmitted = await _context.WJ_PracticalSubmission
                    .CountAsync(p => p.StudentID == userId && p.PracticalID == pracIdInt);

                if (isAlreadySubmitted > 0)
                {
                    list.Add("0");
                    list.Add("This practical has already been submitted.");
                    return Json(list);
                }

                var gameItem = await _context.WJ_Game24
                    .FirstOrDefaultAsync(g => g.Game24ID == game24IdInt && g.GameTopicID == gameTopicIdInt);
                if (gameItem != null)
                {
                    gameItem.GameIsActive = true;
                }

                var solution = new Game24Solution
                {
                    Game24ID = game24IdInt,
                    EntryDate = DateTime.Now,
                    SolutionAnswers = SolutionAnswers,
                    IsCreatedBy = userId,
                    CourseID = courseIdInt
                };
                _context.Game24Solution.Add(solution);

                var coinRecord = new WJ_Coins
                {
                    StudentID = userId,
                    CoinFor = "Game24",
                    ReferenceID = courseIdInt, // assign int, not string
                    Coins = 0,
                    CreatedOn = DateTime.Now
                };
                _context.WJ_Coins.Add(coinRecord);

                await _context.SaveChangesAsync();

                int maxGameId = await _context.WJ_Game24
                    .Where(g => g.CourseID == courseIdInt)
                    .MaxAsync(g => (int?)g.Game24ID) ?? 0;

                if (game24IdInt == maxGameId)
                {
                    var practicalSubmission = new WJ_PracticalSubmission
                    {
                        StudentID = userId,
                        PracticalID = pracIdInt,
                        Status = "Approve",
                        Remarks = "yes",
                        CreatedDate = DateTime.Now
                    };
                    _context.WJ_PracticalSubmission.Add(practicalSubmission);
                    await _context.SaveChangesAsync();

                    list.Add("1");
                    list.Add("Game has been completed Successfully....");
                }
                else
                {
                    list.Add("2");
                    list.Add("Record save Successfully....");
                }
            }
            catch (Exception ex)
            {
                list.Add(ex.Message);
                list.Add("Something went wrong...");
            }
            return Json(list);
        }
    }
}