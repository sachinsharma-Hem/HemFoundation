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
    public class PuzzleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;

        public PuzzleController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: Puzzle
        [HttpGet]
        public async Task<IActionResult> PuzzleIndex(int CourseID, int PractID)
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

            if (userId == 0)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.LanguageID = HttpContext.Session.GetInt32("LanguageID") ?? 1;

            List<string> answers = new List<string>();

            using (var con = new NpgsqlConnection(_connectionString))
            {
                await con.OpenAsync();
                string courseQuery = @"
                    SELECT answer 
                    FROM puzzlemaster 
                    WHERE courseid = @CourseID 
                    AND courseid NOT IN (
                        SELECT courseid FROM puzzletransaction WHERE isresolvedby = @UserId
                    )";

                using (var cmd = new NpgsqlCommand(courseQuery, con))
                {
                    cmd.Parameters.AddWithValue("@CourseID", CourseID);
                    cmd.Parameters.AddWithValue("@UserId", userId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                answers.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }

            string histLine = string.Join(",", answers);

            ViewBag.CourseID = CourseID;
            ViewBag.HistWord = histLine;
            ViewBag.PracticleID = PractID;

            return View();
        }

        [HttpPost]
        public async Task<JsonResult> InserPuzzleData(int CourseID, string fname, int PractID)
        {
            List<object> listobj = new List<object>();

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
                listobj.Add("session_expired");
                listobj.Add("Your session has expired. Please log in again.");
                string expiredJson = Newtonsoft.Json.JsonConvert.SerializeObject(listobj);
                return Json(expiredJson);
            }

            try
            {
                string primaryRole = HttpContext.Session.GetString("UserRole") ?? "";

                if (primaryRole == "WJ_Students")
                {
                    using (var con = new NpgsqlConnection(_connectionString))
                    {
                        await con.OpenAsync();

                        string checkResultQuery = "SELECT count(1) FROM puzzletransaction WHERE courseid = @CourseID AND isresolvedby = @UserId";
                        int res = 0;
                        using (var cmd = new NpgsqlCommand(checkResultQuery, con))
                        {
                            cmd.Parameters.AddWithValue("@CourseID", CourseID);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            res = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        string insertTransQuery = "INSERT INTO puzzletransaction (courseid, isresolved, isresolvedby, isresolveddate) VALUES (@CourseID, 'Y', @UserId, NOW())";
                        using (var cmd = new NpgsqlCommand(insertTransQuery, con))
                        {
                            cmd.Parameters.AddWithValue("@CourseID", CourseID);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                       

                        string insertSubQuery = "INSERT INTO wj_practicalsubmission (studentid, practicalid, status, remarks, creativity, createddate) VALUES (@UserId, @PractID, 'Approve', 'yes', 1, NOW())";
                        using (var cmd = new NpgsqlCommand(insertSubQuery, con))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.Parameters.AddWithValue("@PractID", PractID);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        listobj.Add("1");
                        listobj.Add("Record Save Successfull");
                    }
                }
                else
                {
                    listobj.Add("2");
                    listobj.Add("Only Students can submit the puzzle!!");
                }
            }
            catch (Exception ex)
            {
                listobj.Add(ex.Message);
                listobj.Add("Something went wrong...");
            }

            // Serialize the list into a string so the legacy JS JSON.parse() handles it correctly
            string jsonResultString = Newtonsoft.Json.JsonConvert.SerializeObject(listobj);
            return Json(jsonResultString);
        }
    }
}