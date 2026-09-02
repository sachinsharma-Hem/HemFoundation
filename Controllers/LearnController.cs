using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql; // Changed from Microsoft.Data.SqlClient to Npgsql for PostgreSQL
using System.Collections.Generic;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using HemFoundation.Models;
using Dapper;

namespace HemFoundation.Controllers
{
    public class LearnController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public LearnController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        }

        public async Task<IActionResult> Index(int? id)
        {
            int? studentsID = id;

            // 1. Get the current logged-in user's actual database UserID from our session or claims
            int loggedInUserID = HttpContext.Session.GetInt32("UserID") ?? 0;

            // Fallback if session userID hasn't been cached yet
            if (loggedInUserID == 0)
            {
                var userIdClaim = User.FindFirstValue("UserID");
                if (int.TryParse(userIdClaim, out int parsedId))
                {
                    loggedInUserID = parsedId;
                    HttpContext.Session.SetInt32("UserID", loggedInUserID);
                }
            }

            // 2. Replicate your exact legacy mentor-switching logic
            if (id == null || id == 0)
            {
                // If no ID is passed in the query string, use the logged-in user's own ID
                HttpContext.Session.SetInt32("TempStudentID", loggedInUserID);
                studentsID = loggedInUserID;
                ViewBag.IsStudent = true;
            }
            else
            {
                // If a student ID is passed via query string (e.g., mentor viewing a student), store it
                HttpContext.Session.SetInt32("TempStudentID", id.Value);
                studentsID = id.Value;
                ViewBag.IsStudent = false;
            }

            List<MainCourseVM> mainCourses = new List<MainCourseVM>();

            try
            {
                // 3. Use NpgsqlConnection and NpgsqlCommand for PostgreSQL database queries
                using (NpgsqlConnection conn = new NpgsqlConnection(_connectionString))
                {
                    // Call the function as a table-valued query
                    using (NpgsqlCommand cmd = new NpgsqlCommand("SELECT * FROM public.hf_sp_getstudentmaincourses(@UserID)", conn))
                    {
                        // Change CommandType to Text (default) since we are querying a function
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.AddWithValue("@UserID", studentsID ?? (object)DBNull.Value);

                        await conn.OpenAsync();

                        using (NpgsqlDataReader rdr = await cmd.ExecuteReaderAsync())
                        {
                            while (await rdr.ReadAsync())
                            {
                                mainCourses.Add(new MainCourseVM
                                {
                                    MainCourseID = rdr.GetInt32(rdr.GetOrdinal("maincourseid")),
                                    MainCourseName = rdr.GetString(rdr.GetOrdinal("maincoursename")),
                                    OrderNo = rdr.GetInt32(rdr.GetOrdinal("orderno")),
                                    Status = rdr.GetString(rdr.GetOrdinal("status")),
                                    FeaturedImage = rdr.GetString(rdr.GetOrdinal("featuredimage")),
                                    MainCourseMarks = rdr.GetInt32(rdr.GetOrdinal("maincoursemarks")),
                                    GlobalAssigned = rdr.GetInt32(rdr.GetOrdinal("globalassigned")),
                                    GlobalCompleted = rdr.GetInt32(rdr.GetOrdinal("globalcompleted")),
                                    GlobalTotalMarks = rdr.GetInt32(rdr.GetOrdinal("globaltotalmarks")),
                                    CertifiedCount = rdr.GetInt32(rdr.GetOrdinal("certifiedcount"))
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception if necessary
            }

            return View(mainCourses);
        }

        [HttpGet]
        public JsonResult GetCourses(int mainCourseId)
        {
            // Safely retrieve user ID using ASP.NET Core ISession extension methods
            int currentUserId = 1;

            int? tempStudentId = HttpContext?.Session?.GetInt32("TempStudentID");
            int? userId = HttpContext?.Session?.GetInt32("UserID");

            if (tempStudentId.HasValue)
            {
                currentUserId = tempStudentId.Value;
            }
            else if (userId.HasValue)
            {
                currentUserId = userId.Value;
            }

            try
            {
                // Connect via Npgsql to PostgreSQL
                using (IDbConnection db = new NpgsqlConnection(_connectionString))
                {
                    var courseDictionary = new Dictionary<int, CourseDetailVM>();

                    db.Query<CourseDetailVM, SqlRawRowVM, CourseDetailVM>(
                        "SELECT * FROM public.hf_sp_getdashboarddata(@MainCourseId, @UserId)",
                        (course, rawRow) =>
                        {
                            CourseDetailVM currentCourse;

                            if (!courseDictionary.TryGetValue(course.CourseID, out currentCourse))
                            {
                                currentCourse = course;
                                currentCourse.SqlRawRows = new List<SqlRawRowVM>();
                                courseDictionary.Add(currentCourse.CourseID, currentCourse);
                            }

                            if (rawRow != null && (rawRow.TopicID > 0 || !string.IsNullOrEmpty(rawRow.PracticalCode)))
                            {
                                currentCourse.SqlRawRows.Add(rawRow);
                            }

                            return currentCourse;
                        },
                        param: new { MainCourseId = mainCourseId, UserId = currentUserId },
                        splitOn: "QuizID",
                        commandType: CommandType.Text
                    );

                    return Json(courseDictionary.Values.ToList());
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}