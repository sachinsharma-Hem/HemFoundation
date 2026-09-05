using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using HemFoundation.Data;

namespace HemFoundation.Controllers
{
    public class WordGameController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;

        public WordGameController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: WordGame
        [HttpGet]
        public async Task<IActionResult> Index(int CourseID, int PractID)
        {
            // Session check for TempStudentID with claim fallback
            int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 0;

            if (userId == 0)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    int.TryParse(userIdClaim, out userId);
                }
            }

            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.LanguageID = HttpContext.Session.GetInt32("LanguageID") ?? 1;

            // Fetch unresolved word game questions using Npgsql for PostgreSQL
            DataTable dataTable = new DataTable();
            using (var con = new NpgsqlConnection(_connectionString))
            {
                await con.OpenAsync();
                string query = @"
                    SELECT wgid, wgtopicid, courseid, wgt.wg_topicname AS wg_topicname, wg_question AS wg_question, wg_answer AS wg_answer 
                    FROM wj_wordgamequestions AS wgq 
                    INNER JOIN wj_wordgametopic AS wgt ON wgt.wg_topicid = wgq.wgtopicid 
                    WHERE wgq.wgid NOT IN (
                        SELECT wgid FROM wordgamesolution WHERE iscreatedby = @UserId
                    ) AND courseid = @CourseID";

                using (var cmd = new NpgsqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@CourseID", CourseID);

                    using (var adapter = new NpgsqlDataAdapter(cmd))
                    {
                        adapter.Fill(dataTable);
                    }
                }
            }

            if (dataTable.Rows.Count > 0)
                ViewData.Model = dataTable.AsEnumerable();
            else
                ViewData.Model = "";

            // Fetch course name via EF Core
            var courseName = await _context.Courses
                .Where(x => x.CourseID == CourseID)
                .Select(x => x.CourseName)
                .FirstOrDefaultAsync();

            ViewBag.CourseName = courseName ?? "Unknown Course";
            ViewBag.CourseID = CourseID;
            ViewBag.PracticleID = PractID;

            return View();
        }

        [HttpPost]
        public async Task<JsonResult> WordGameSave(string Game24ID, string GameTopicID, string CID, string SolutionAnswers, int PractID)
        {
            List<object> list = new List<object>();

            int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 0;

            if (userId == 0)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    int.TryParse(userIdClaim, out userId);
                }
            }

            if (userId == 0)
            {
                list.Add("session_expired");
                list.Add("Your session has expired. Please log in again.");
                return Json(list);
            }

            try
            {
                int gameIdInt = Convert.ToInt32(Game24ID);
                int topicIdInt = Convert.ToInt32(GameTopicID);
                int courseIdInt = Convert.ToInt32(CID);

                // Check submission status via EF Core
                int isAlreadySubmitted = await _context.WJ_PracticalSubmission
                    .CountAsync(p => p.StudentID == userId && p.PracticalID == PractID);

                if (isAlreadySubmitted > 0)
                {
                    list.Add("0");
                    list.Add("This practical has already been submitted.");
                    return Json(list);
                }

                using (var con = new NpgsqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    // Insert solution using correct table name 'wordgamesolution'
                    string insertSol = "INSERT INTO wordgamesolution(wgid, entrydate, solutionanswers, iscreatedby, courseid) VALUES (@WGID, NOW(), @SolutionAnswers, @UserID, @CourseID)";
                    using (var cmd = new NpgsqlCommand(insertSol, con))
                    {
                        cmd.Parameters.AddWithValue("@WGID", gameIdInt);
                        cmd.Parameters.AddWithValue("@SolutionAnswers", SolutionAnswers);
                        cmd.Parameters.AddWithValue("@UserID", userId);
                        cmd.Parameters.AddWithValue("@CourseID", courseIdInt);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Check remaining questions
                    string countQuery = "SELECT COUNT(1) FROM wj_wordgamequestions WHERE wgid NOT IN (SELECT wgid FROM wordgamesolution WHERE iscreatedby = @UserID) AND courseid = @CourseID";
                    int checkValues = 0;
                    using (var cmd = new NpgsqlCommand(countQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@UserID", userId);
                        cmd.Parameters.AddWithValue("@CourseID", courseIdInt);
                        checkValues = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    if (checkValues != 0)
                    {
                        list.Add("2");
                        list.Add("Record save Successfully....");
                    }
                    else
                    {
                        
                        
                        // Insert Practical Submission
                        string insertSub = "INSERT INTO wj_practicalsubmission(studentid, practicalid, status, remarks, createddate) VALUES (@UserID, @PractID, 'Approve', 'yes', NOW())";
                        using (var cmd = new NpgsqlCommand(insertSub, con))
                        {
                            cmd.Parameters.AddWithValue("@UserID", userId);
                            cmd.Parameters.AddWithValue("@PractID", PractID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        list.Add("1");
                        list.Add("Game has been completed Successfully....");

                    }
                }
            }
            catch (Exception ex)
            {
                list.Add("Something went wrong...");
                list.Add(ex.Message);
                
            }
            return Json(list);
        }
    }
}