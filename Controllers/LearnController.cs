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

        [HttpGet]
        public IActionResult DownloadCertificate(int CourseID, int Quizid)
        {
            ViewBag.CourseID = CourseID;
            ViewBag.Quizid = Quizid;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetCertificateStatus(int courseId, int Quizid)
        {
            int studentId = HttpContext.Session.GetInt32("UserID") ?? 1;

            StudentCertificateModel model = new StudentCertificateModel();
            try
            {
                string base64ImageRepresentation = null;
                bool hasRow = false;

                // 1. Check if certificate record exists for the student
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new NpgsqlCommand("SELECT certificationno, firstname, lastname, standard, institutename, coursename, courseid, instituteid FROM wj_value_certification WHERE userid = @UserID AND courseid = @CourseID LIMIT 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserID", studentId);
                        cmd.Parameters.AddWithValue("@CourseID", courseId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                hasRow = true;
                                model.CertificationNo = reader["certificationno"]?.ToString();
                                model.FirstName = reader["firstname"]?.ToString();
                                model.LastName = reader["lastname"]?.ToString();
                                model.Standard = reader["standard"]?.ToString();
                                model.InstituteName = reader["institutename"]?.ToString();
                                model.CourseName = reader["coursename"]?.ToString();
                            }
                        }
                    }
                }

                if (hasRow)
                {
                    int instituteID = 0;
                    string mainCourseName = "";

                    using (var conn = new NpgsqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();

                        // Get Main Course Name & ID details
                        using (var cmd = new NpgsqlCommand("SELECT maincoursename FROM view_wj_syllabus WHERE courseid = @CourseID LIMIT 1", conn))
                        {
                            cmd.Parameters.AddWithValue("@CourseID", courseId);
                            var res = await cmd.ExecuteScalarAsync();
                            mainCourseName = res?.ToString() ?? "";
                        }

                        using (var cmd = new NpgsqlCommand("SELECT instituteid FROM wj_value_certification WHERE userid = @UserID AND courseid = @CourseID LIMIT 1", conn))
                        {
                            cmd.Parameters.AddWithValue("@UserID", studentId);
                            cmd.Parameters.AddWithValue("@CourseID", courseId);
                            var res = await cmd.ExecuteScalarAsync();
                            if (res != null && int.TryParse(res.ToString(), out int instId))
                            {
                                instituteID = instId;
                            }
                        }
                    }

                    model.MainCourseName = mainCourseName;

                    // Fetch image/certificate bytes via helper
                    DataTable certificateData = await GetInstituteCertificateAsync(studentId, courseId, instituteID);
                    if (certificateData.Rows.Count > 0 && certificateData.Rows[0]["certificate"] != DBNull.Value)
                    {
                        byte[] data = (byte[])certificateData.Rows[0]["certificate"];
                        base64ImageRepresentation = "data:image/png;base64," + Convert.ToBase64String(data);
                    }
                    else
                    {
                        TempData["Error"] = "No certificate found...";
                    }

                    using (var conn = new NpgsqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new NpgsqlCommand("SELECT featuredimage FROM courses WHERE courseid = @CourseID LIMIT 1", conn))
                        {
                            cmd.Parameters.AddWithValue("@CourseID", courseId);
                            model.CourseImage = (await cmd.ExecuteScalarAsync())?.ToString();
                        }

                        using (var cmd = new NpgsqlCommand("SELECT photourl FROM users WHERE userid = @InstituteID LIMIT 1", conn))
                        {
                            cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                            model.InstituteUrl = (await cmd.ExecuteScalarAsync())?.ToString();
                        }
                    }

                    model.CertificationTemplateUrl = base64ImageRepresentation;
                    return PartialView("~/Views/Learn/Shared/_GetStudentCertificateViewNew.cshtml", model);
                }
                else
                {
                    string studentStandard = "";
                    int status = 0;
                    bool isEligible = true;

                    using (var conn = new NpgsqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new NpgsqlCommand("SELECT standard FROM wj_students WHERE studentid = @StudentID", conn))
                        {
                            cmd.Parameters.AddWithValue("@StudentID", studentId);
                            studentStandard = (await cmd.ExecuteScalarAsync())?.ToString() ?? "";
                        }
                    }

                    if (studentStandard.Contains("13") || studentStandard.Contains("14") || studentStandard.Contains("15"))
                    {
                        using (var conn = new NpgsqlConnection(_connectionString))
                        {
                            await conn.OpenAsync();
                            using (var cmd = new NpgsqlCommand("CALL public.hf_sp_gethigherstandardcompletion(@p_students_id, @p_status)", conn))
                            {
                                cmd.CommandType = CommandType.Text;
                                cmd.Parameters.AddWithValue("@p_students_id", studentId);

                                var statusParam = new NpgsqlParameter("@p_status", NpgsqlTypes.NpgsqlDbType.Integer)
                                {
                                    Direction = ParameterDirection.Output
                                };
                                cmd.Parameters.Add(statusParam);

                                await cmd.ExecuteNonQueryAsync();
                                status = statusParam.Value != DBNull.Value ? Convert.ToInt32(statusParam.Value) : 0;
                            }
                        }

                        if (status != 1)
                        {
                            isEligible = false;
                        }
                    }

                    if (!isEligible) return PartialView("_NotEligibleMessage");
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            ViewBag.Courseid = courseId;
            ViewBag.StudentsID = studentId;
            ViewBag.Quizid = Quizid;
            ViewBag.isStudentCertification = true;
            return PartialView("_AffirmationView");
        }

        private async Task<DataTable> GetInstituteCertificateAsync(int studentID, int courseID, int instituteID)
        {
            DataTable dt = new DataTable();
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string functionCall = "";

                if (instituteID == 0)
                {
                    functionCall = "SELECT * FROM public.getcertificatedetails1(@p_studentID, @p_courseID, NULL)";
                }
                else
                {
                    bool hasCert = false;
                    using (var checkCmd = new NpgsqlCommand("SELECT 1 FROM wj_main_certificates WHERE instituteid = @InstituteID LIMIT 1", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@InstituteID", instituteID);
                        hasCert = (await checkCmd.ExecuteScalarAsync() != null);
                    }

                    if (hasCert)
                    {
                        functionCall = "SELECT * FROM public.getcertificatedetails(@p_studentID, @p_courseID, @p_instituteID)";
                    }
                    else
                    {
                        functionCall = "SELECT * FROM public.getcertificatedetails1(@p_studentID, @p_courseID, NULL)";
                    }
                }

                using (var cmd = new NpgsqlCommand(functionCall, conn))
                {
                    cmd.Parameters.AddWithValue("@p_studentID", studentID);
                    cmd.Parameters.AddWithValue("@p_courseID", courseID);
                    if (instituteID != 0 && functionCall.Contains("@p_instituteID"))
                    {
                        cmd.Parameters.AddWithValue("@p_instituteID", instituteID);
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        dt.Load(reader);
                    }
                }
            }
            return dt;
        }

    }
}