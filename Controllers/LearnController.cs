using Dapper;
using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.InkML;
using HemFoundation.Data;
using HemFoundation.DTOs;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using Npgsql; // Changed from Microsoft.Data.SqlClient to Npgsql for PostgreSQL
using System.Collections.Generic;
using System.Data;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
namespace HemFoundation.Controllers
{
    public class LearnController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        private readonly ApplicationDbContext _context;
        public LearnController(IConfiguration configuration, ApplicationDbContext context)
        {
            _context = context;
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
            ViewBag.StudentID = HttpContext.Session.GetInt32("UserID") ?? 1;
            ViewBag.Quizid = Quizid;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetCertificateStatus(int courseId, int Quizid)
        {
            int studentId = HttpContext.Session.GetInt32("UserID") ?? 1;
            ViewBag.StudentID = HttpContext.Session.GetInt32("UserID") ?? 1;
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
        [HttpPost]
        public async Task<IActionResult> UpdateStudentCertification(bool isStudentCertification, int studentId, int courseId, int quizId)
        {
            if (isStudentCertification)
            {
                bool result = await InsertStudentCertificateAsync(isStudentCertification, studentId, courseId, quizId);
                if (result)
                {
                    return Json(true);
                }
            }
            return Json(false);
        }

        public async Task<bool> InsertStudentCertificateAsync(bool isStudentCertification, int studentId, int courseId, int quizId)
        {
            try
            {
                string firstName = "";
                string lastName = "";
                string standard = "";
                string division = "";
                string emailId = "";
                string levelPath = "";
                int points = 0;
                long rank = 0;
                int? instituteId = null;
                string instituteName = "";
                string courseName = "";

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Use a transaction to ensure atomicity for reads/write
                    using (var transaction = await conn.BeginTransactionAsync())
                    {
                        try
                        {
                            // 1. Get Student Details from View
                            using (var cmd = new NpgsqlCommand("SELECT firstname, lastname, standard, division, emailid, levelpath, coins, rank FROM view_wj_students_details_info WHERE studentid = @StudentID LIMIT 1", conn, transaction))
                            {
                                cmd.CommandTimeout = 30; // Prevent hanging queries
                                cmd.Parameters.AddWithValue("@StudentID", studentId);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        firstName = reader["firstname"]?.ToString() ?? "";
                                        lastName = reader["lastname"]?.ToString() ?? "";
                                        standard = reader["standard"]?.ToString() ?? "";
                                        division = reader["division"]?.ToString() ?? "";
                                        emailId = reader["emailid"]?.ToString() ?? "";
                                        levelPath = reader["levelpath"]?.ToString() ?? "";
                                        points = reader["coins"] != DBNull.Value ? Convert.ToInt32(reader["coins"]) : 0;
                                        rank = reader["rank"] != DBNull.Value ? Convert.ToInt64(reader["rank"]) : 0;
                                    }
                                }
                            }

                            // 2. Get School Institute Association
                            using (var cmd = new NpgsqlCommand(@"
                        SELECT i.instituteid, i.""Name"" 
                        FROM wj_studentassociates a 
                        JOIN wj_institutes i ON a.instituteid = i.instituteid 
                        WHERE a.studentid = @StudentID AND i.institutetype = 'School' 
                        ORDER BY a.studentid DESC LIMIT 1", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@StudentID", studentId);
                                using (var reader = await cmd.ExecuteReaderAsync())
                                {
                                    if (await reader.ReadAsync())
                                    {
                                        instituteId = reader["instituteid"] != DBNull.Value ? Convert.ToInt32(reader["instituteid"]) : (int?)null;
                                        instituteName = reader["name"]?.ToString() ?? "";
                                    }
                                }
                            }

                            // 3. Get Course Details
                            using (var cmd = new NpgsqlCommand("SELECT coursename FROM \"View_WJ_Courses\" WHERE courseid = @CourseID LIMIT 1", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@CourseID", courseId);
                                var res = await cmd.ExecuteScalarAsync();
                                courseName = res?.ToString() ?? "";
                            }

                            // 4. Check if certification already exists
                            bool isExists = false;
                            using (var cmd = new NpgsqlCommand("SELECT 1 FROM wj_value_certification WHERE userid = @UserID AND isadminstatus::integer = 0 AND courseid = @CourseID LIMIT 1", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UserID", studentId);
                                cmd.Parameters.AddWithValue("@CourseID", courseId);
                                var res = await cmd.ExecuteScalarAsync();
                                isExists = (res != null);
                            }

                            // 5. Insert if it doesn't already exist
                            // 5. Insert if it doesn't already exist (removed 'rank' if it's missing from the table schema)
                            // 5. Insert if it doesn't already exist
                            if (!isExists)
                            {
                                using (var cmd = new NpgsqlCommand(@"
        INSERT INTO wj_value_certification (
            userid, firstname, lastname, standard, division, emailid, 
            instituteid, institutename, lavel, points, quizid, courseid, 
            coursename, status, iscompleted, isactive, isadminstatus, createdon, createdby
        ) VALUES (
            @UserID, @FirstName, @LastName, @Standard, @Division, @EmailId, 
            @InstituteId, @InstituteName, @Lavel, @Points, @QuizId, @CourseId, 
            @CourseName, '0'::bit, '0'::bit, @IsActive::integer::bit, '1'::bit, CURRENT_TIMESTAMP, @CreatedBy
        )", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@UserID", studentId);
                                    cmd.Parameters.AddWithValue("@FirstName", firstName);
                                    cmd.Parameters.AddWithValue("@LastName", lastName);
                                    cmd.Parameters.AddWithValue("@Standard", standard);
                                    cmd.Parameters.AddWithValue("@Division", division);
                                    cmd.Parameters.AddWithValue("@EmailId", emailId);
                                    cmd.Parameters.AddWithValue("@InstituteId", (object)instituteId ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("@InstituteName", instituteName);
                                    cmd.Parameters.AddWithValue("@Lavel", levelPath);
                                    cmd.Parameters.AddWithValue("@Points", points);
                                    cmd.Parameters.AddWithValue("@QuizId", quizId);
                                    cmd.Parameters.AddWithValue("@CourseId", courseId);
                                    cmd.Parameters.AddWithValue("@CourseName", courseName);
                                    cmd.Parameters.AddWithValue("@IsActive", isStudentCertification);
                                    cmd.Parameters.AddWithValue("@CreatedBy", studentId);

                                    await cmd.ExecuteNonQueryAsync();
                                }
                            }

                            await transaction.CommitAsync();
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                // Helpful debugging hook: check console or log ex.Message if a timeout occurs
                Console.WriteLine($"Error inserting student certificate: {ex.Message}");
                return false;
            }
        }
        [HttpGet]
        public IActionResult DisplayScoreCard(int? id)
        {
            StudentProfileModel obj = new StudentProfileModel();
            try
            {
                // Retrieve UserID from session (ASP.NET Core Session syntax)
                int userID = HttpContext.Session.GetInt32("UserID") ?? 0;

                // Fallback or handle session structure based on your architecture
                int studentID = userID;

               

               // int coinCount = mng.GetStudentCoinCount(studentUSID);
                ViewBag.CoinCount = 0;
                

                if (id.HasValue && id.Value >= 1)
                {
                    studentID = id.Value;
                }

                if (userID == 0)
                {
                    return RedirectToAction("Index", "Learn", new { ID = id });
                }
                obj.StudentID = studentID;
                if (studentID >= 1)
                {
                    obj = GetStudentDetailsByID(studentID);

                    if (obj != null)
                    {
                        if (obj.StudentID >= 1)
                        {
                            obj.StudentID = obj.StudentID;
                        }
                        else
                        {
                            return RedirectToAction("Index", "Home");
                        }
                    }
                    else
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }

                
            }
            catch (Exception ex)
            {
                // ASP.NET Core Exception Logging equivalent
               // _logger.LogError(ex, "Error occurred in DisplayScoreCard for ID: {Id}", id);
                TempData["ErrorMessage"] = "Error 404. Page Not Found.";
                return RedirectToAction("Index", "Home");
            }

            return View(obj);
        }
        public StudentProfileModel GetStudentDetailsByID(int studentID)
        {
            StudentProfileModel obj = new StudentProfileModel();
            int userID = HttpContext.Session.GetInt32("UserID") ?? 0;

            try
            {
                obj.IsMobileVerified = false;
                obj.IsEmailVerified = false;
                obj.IsProfileUpdated = false;
                int profileupdatecount = 0;

                // 1. Fetch student base details directly from your Users/Students table using EF Core
                var student = _context.ViewWjStudentsDetailsInfo.Where(u => u.StudentId == studentID).FirstOrDefault();
                // Note: Change `_context.Users` to whatever your actual table/DbSet is named (e.g., _context.Students)

                if (student != null)
                {
                    obj.StudentID = student.StudentId;
                    obj.FirstName = student.FirstName;
                    obj.LastName = student.LastName;
                    obj.EmailID = student.EmailId;
                    obj.MobileNo = student.MobileNo;
                    //obj.DOB = student.DOB;
                    obj.AddressLine1 = student.AddressLine1;
                    obj.CityName = student.CityName;
                    obj.Gender = student.Gender;
                    obj.Pincode = student.Pincode;
                    //obj.GardianName = student.GuardianName; // Check property spelling (Gardian vs Guardian)
                    obj.InstituteID = student.InstituteID ?? 0;
                    obj.InstituteName = student.InstituteName;
                    obj.IsMobileVerified = student.IsMobileVerified;
                    obj.IsEmailVerified = student.IsEmailVerified;
                }

               
                obj.Coins = 0;

                if (obj.InstituteID != null && obj.InstituteID > 0)
                {
                    // Already set above from student entity
                }
                else
                {
                    profileupdatecount++;
                    obj.IsProfileUpdated = true;
                }

                if (string.IsNullOrEmpty(obj.GardianName))
                {
                    obj.IsProfileUpdated = true;
                    profileupdatecount++;
                }

                if (string.IsNullOrEmpty(obj.FirstName) || string.IsNullOrEmpty(obj.LastName) ||
                    string.IsNullOrEmpty(obj.EmailID) || string.IsNullOrEmpty(obj.MobileNo) ||
                    !obj.DOB.HasValue || string.IsNullOrEmpty(obj.AddressLine1) ||
                    string.IsNullOrEmpty(obj.CityName) || string.IsNullOrEmpty(obj.Gender) ||
                    string.IsNullOrEmpty(obj.Pincode))
                {
                    obj.IsProfileUpdated = true;
                }

                if (!obj.IsMobileVerified.GetValueOrDefault(false))
                {
                    profileupdatecount++;
                }
                else if (!obj.IsEmailVerified.GetValueOrDefault(false))
                {
                    profileupdatecount++;
                }

                // 3. Get Referral Code
                var referralcode = _context.WJ_ReferralCodes
                                           .Where(x => x.UserID == studentID)
                                           .Select(x => x.ReferralCode)
                                           .FirstOrDefault();
                obj.ReferralCode = referralcode;
                obj.IsPaid = true;

                if (userID == studentID)
                {
                    obj.IsMyPage = true;
                }

                obj.IsDailySpin = false;
            }
            catch (Exception ex)
            {
                string msg = "Error 404. Page not Found.";
                throw new Exception(msg, ex);
            }
            return obj;
        }
        [HttpGet]        
        public IActionResult GetStudent_SummaryReportPartial1(int studentID, int instituteID)
        {
            TempData["ID"] = studentID;
            TempData["StudentID"] = studentID;
            ViewBag.StudentID = studentID;
            TempData["InsID"] = instituteID.ToString();
            ViewBag.InstituteID = instituteID;

            StundetSummaryReportModel stundetSummaryReportModel = new StundetSummaryReportModel
            {
                StudentID = studentID,
                InstituteID = instituteID,
                StudentDetailList = new List<StudentDetailSummary>()
            };

            try
            {
                string connectionString = _connectionString;
                using (var connection = new Npgsql.NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Transactions are mandatory when fetching refcursors in PostgreSQL
                    using (var transaction = connection.BeginTransaction())
                    {
                        using (var command = new Npgsql.NpgsqlCommand("SELECT * FROM \"Sp_Studnet_ReportSummary\"(@p1, @p2, @p3, @p4, @p5)", connection))
                        {
                            command.Parameters.AddWithValue("p1", studentID);
                            command.Parameters.AddWithValue("p2", instituteID);
                            command.Parameters.AddWithValue("p3", DBNull.Value); // MainCourseID
                            command.Parameters.AddWithValue("p4", "Display");     // Action
                            command.Parameters.AddWithValue("p5", DBNull.Value); // CourseID

                            // Declare output refcursors for PostgreSQL function
                            command.Parameters.Add(new Npgsql.NpgsqlParameter("ref1", NpgsqlTypes.NpgsqlDbType.Refcursor) { Direction = ParameterDirection.Output });
                            command.Parameters.Add(new Npgsql.NpgsqlParameter("ref2", NpgsqlTypes.NpgsqlDbType.Refcursor) { Direction = ParameterDirection.Output });
                            command.Parameters.Add(new Npgsql.NpgsqlParameter("ref3", NpgsqlTypes.NpgsqlDbType.Refcursor) { Direction = ParameterDirection.Output });

                            using (var reader = command.ExecuteReader())
                            {
                                // Because refcursors return cursor names as strings first, we fetch from those cursors 
                                // Or if mapped cleanly via Dapper, you can fetch multi-query datasets easily.
                            }
                        }
                        transaction.Commit();
                    }
                }

                HttpContext.Session.SetInt32("StudentID", studentID);
                HttpContext.Session.SetInt32("InstituteID", instituteID);

                if (User.IsInRole("WJ_Students"))
                {
                    return PartialView("~/Views/Learn/Shared/StudentSummary_Report.cshtml", stundetSummaryReportModel);
                }
                else
                {
                    return PartialView("~/Views/Learn/Shared/Teacher_Student_Summary_Report.cshtml", stundetSummaryReportModel);
                }
            }
            catch (Exception ex)
            {
               // _logger.LogError(ex, "Error in GetStudent_SummaryReportPartial for StudentID: {StudentId}", studentID);

                if (User.IsInRole("WJ_Students"))
                {
                    return PartialView("~/Views/Learn/Shared/StudentSummary_Report.cshtml", null);
                }
                else
                {
                    return PartialView("~/Views/Learn/Shared/Teacher_Student_Summary_Report.cshtml", null);
                }
            }
        }
        public IActionResult GetStudent_SummaryReportPartial(int studentID, int instituteID)
        {
            TempData["ID"] = studentID;
            TempData["StudentID"] = studentID;
            ViewBag.StudentID = studentID;
            TempData["InsID"] = instituteID.ToString();
            ViewBag.InstituteID = instituteID;
            String roleNAME = HttpContext.Session.GetString("UserRole") ?? "NA";
            StundetSummaryReportModel stundetSummaryReportModel = new StundetSummaryReportModel
            {
                StudentID = studentID,
                InstituteID = instituteID,
                StudentDetailList = new List<StudentDetailSummary>()
            };

            try
            {
                string connectionString = _connectionString;
                using (var connection = new Npgsql.NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Transactions are mandatory when fetching refcursors in PostgreSQL
                    using (var transaction = connection.BeginTransaction())
                    {
                        // 1. First, call the function which returns the cursor names
                        string ref1Name = string.Empty;
                        string ref2Name = string.Empty;
                        string ref3Name = string.Empty;

                        using (var command = new Npgsql.NpgsqlCommand("SELECT * FROM \"Sp_Studnet_ReportSummary\"(@p1, @p2, @p3, @p4, @p5)", connection))
                        {
                            command.Transaction = transaction;
                            command.Parameters.AddWithValue("p1", studentID);
                            command.Parameters.AddWithValue("p2", instituteID);
                            command.Parameters.AddWithValue("p3", DBNull.Value); // MainCourseID
                            command.Parameters.AddWithValue("p4", "Display");     // Action
                            command.Parameters.AddWithValue("p5", DBNull.Value); // CourseID

                            // Declare output refcursors for PostgreSQL function
                            command.Parameters.Add(new Npgsql.NpgsqlParameter("ref1", NpgsqlTypes.NpgsqlDbType.Refcursor) { Direction = ParameterDirection.Output });
                            command.Parameters.Add(new Npgsql.NpgsqlParameter("ref2", NpgsqlTypes.NpgsqlDbType.Refcursor) { Direction = ParameterDirection.Output });
                            command.Parameters.Add(new Npgsql.NpgsqlParameter("ref3", NpgsqlTypes.NpgsqlDbType.Refcursor) { Direction = ParameterDirection.Output });

                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    ref1Name = reader.GetString(0);
                                    ref2Name = reader.GetString(1);
                                    ref3Name = reader.GetString(2);
                                }
                            }
                        }

                        // Helper local function to fetch data from individual cursors using independent commands within the same transaction
                        DataTable FetchCursorData(string cursorName)
                        {
                            var dt = new DataTable();
                            using (var fetchCmd = new Npgsql.NpgsqlCommand($"FETCH ALL FROM \"{cursorName}\"", connection))
                            {
                                fetchCmd.Transaction = transaction;
                                using (var fetchReader = fetchCmd.ExecuteReader())
                                {
                                    dt.Load(fetchReader);
                                }
                            }
                            return dt;
                        }

                        // 2. Fetch the 3 corresponding tables matching the legacy DataSets
                        DataTable dtStudentInfo = FetchCursorData(ref1Name);
                        DataTable dtMcqPracticalCount = FetchCursorData(ref2Name);
                        DataTable dtDropdownCourses = FetchCursorData(ref3Name);

                        // Populate Dropdown for Views
                        var courseList = dtDropdownCourses.AsEnumerable().Select(row => new {
                            MainCourseID = row["MainCourseID"],
                            MainCourseName = row["MainCourseName"]
                        }).ToList();

                        ViewBag.listCourse = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                            courseList,
                            "MainCourseID",
                            "MainCourseName"
                        );
                        // 3. Map Student Information (Table 0 / ref1)
                        if (dtStudentInfo.Rows.Count > 0)
                        {
                            DataRow row = dtStudentInfo.Rows[0];
                            stundetSummaryReportModel.StudentName = Convert.ToString(row["StudentName"]);

                            string div = Convert.ToString(row["Division"]);
                            stundetSummaryReportModel.Division = string.IsNullOrEmpty(div) ? "--" : div;

                            stundetSummaryReportModel.Stanadard = Convert.ToString(row["Standard"]);

                            string schName = Convert.ToString(row["SchoolName"]);
                            stundetSummaryReportModel.SchoolName = string.IsNullOrEmpty(schName) ? "--" : schName;
                        }

                        // 4. Map Counts (Table 1 / ref2) - Optional handling matching legacy structure if needed
                        if (dtMcqPracticalCount.Rows.Count > 0)
                        {
                            // Loop or assign depending on your model properties for MCQ / Practical counts
                            foreach (DataRow r in dtMcqPracticalCount.Rows)
                            {
                                string courseName = Convert.ToString(r["CourseName"]);
                                int fearlessCount = Convert.ToInt32(r["Fearless"]);
                                if (courseName == "MCQ")
                                {
                                    stundetSummaryReportModel.ToTalMCQ = fearlessCount < 0 ? 0 : fearlessCount;
                                }
                                else if (courseName == "Practical")
                                {
                                    stundetSummaryReportModel.TotalPractical = fearlessCount < 0 ? 0 : fearlessCount;
                                }
                            }
                            stundetSummaryReportModel.GrandTotalMCQPractical = stundetSummaryReportModel.ToTalMCQ + stundetSummaryReportModel.TotalPractical;
                        }

                        transaction.Commit();
                    }
                }

                HttpContext.Session.SetInt32("StudentID", studentID);
                HttpContext.Session.SetInt32("InstituteID", instituteID);

                if (roleNAME=="WJ_Students")
                {
                    return PartialView("~/Views/Learn/Shared/StudentSummary_Report.cshtml", stundetSummaryReportModel);
                }
                else
                {
                    return PartialView("~/Views/Learn/Shared/Teacher_Student_Summary_Report.cshtml", stundetSummaryReportModel);
                }
            }
            catch (Exception ex)
            {
                // _logger.LogError(ex, "Error in GetStudent_SummaryReportPartial for StudentID: {StudentId}", studentID);

                if (roleNAME == "WJ_Students")
                {
                    return PartialView("~/Views/Learn/Shared/StudentSummary_Report.cshtml", null);
                }
                else
                {
                    return PartialView("~/Views/Learn/Shared/Teacher_Student_Summary_Report.cshtml", null);
                }
            }
        }

        public JsonResult GetCoursesList(string CourseID, string StudentID, int InstituteID)
        {
            int studentID = Convert.ToInt32(StudentID);
            if (TempData != null)
            {
                if (TempData["ID"] != null)
                {
                    StudentID = TempData["ID"].ToString();
                    studentID = Convert.ToInt32(StudentID);
                }
                if (TempData["InsID"] != null)
                {
                    InstituteID = Convert.ToInt32(TempData["InsID"]);
                }
            }
            int courseId = Convert.ToInt32(CourseID);

            DataTable dtDetails = new DataTable();
            string cursorName = string.Empty;

            try
            {
                string connectionString = _connectionString;
                using (var connection = new Npgsql.NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // 1. Call function and retrieve the returned cursor name using ExecuteScalar
                        using (var command = new Npgsql.NpgsqlCommand("SELECT \"SP_Studnet_ReportSummarydemo_updated2026\"(@p1, @p2, @p3, @p4, @p5)", connection))
                        {
                            command.Transaction = transaction;
                            command.Parameters.AddWithValue("p1", studentID);
                            command.Parameters.AddWithValue("p2", InstituteID);
                            command.Parameters.AddWithValue("p3", courseId);
                            command.Parameters.AddWithValue("p4", "DisplayDetials");
                            command.Parameters.AddWithValue("p5", DBNull.Value); // CourseID

                            cursorName = command.ExecuteScalar()?.ToString();
                        }

                        // 2. Fetch the data out of the returned cursor within the same transaction
                        if (!string.IsNullOrEmpty(cursorName))
                        {
                            using (var fetchCmd = new Npgsql.NpgsqlCommand($"FETCH ALL FROM \"{cursorName}\"", connection))
                            {
                                fetchCmd.Transaction = transaction;
                                using (var fetchReader = fetchCmd.ExecuteReader())
                                {
                                    dtDetails.Load(fetchReader);
                                }
                            }
                        }

                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception if needed
            }

            string pblStatusElement = "";
            if (dtDetails.Rows.Count > 3)
            {
                if (dtDetails.Rows[3]["pblStatusElement"] != DBNull.Value)
                {
                    pblStatusElement = dtDetails.Rows[3]["pblStatusElement"].ToString();
                }
            }

            var listValues = DataTableToJSONWithStringBuilder(dtDetails);

            return Json(new { listValues = listValues, pblStatus = pblStatusElement });
        }
        public static string DataTableToJSONWithStringBuilder(DataTable table)
        {
            var JSONString = new StringBuilder();
            if (table.Rows.Count > 0)
            {
                JSONString.Append("[");
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    JSONString.Append("{");
                    for (int j = 0; j < table.Columns.Count; j++)
                    {
                        if (j < table.Columns.Count - 1)
                        {
                            JSONString.Append("\"" + table.Columns[j].ColumnName.ToString() + "\":" + "\"" + table.Rows[i][j].ToString() + "\",");
                        }
                        else if (j == table.Columns.Count - 1)
                        {
                            JSONString.Append("\"" + table.Columns[j].ColumnName.ToString() + "\":" + "\"" + table.Rows[i][j].ToString() + "\"");
                        }
                    }
                    if (i == table.Rows.Count - 1)
                    {
                        JSONString.Append("}");
                    }
                    else
                    {
                        JSONString.Append("},");
                    }
                }
                JSONString.Append("]");
            }
            return JSONString.ToString();
        }
        [HttpGet]
        public IActionResult PrintStudentScorecard(int? id)
        {
            int targetStudentId = id ?? HttpContext.Session.GetInt32("UserID") ?? 0;
           // int instituteId = HttpContext.Session.GetInt32("InstituteID") ?? 1;

            if (targetStudentId == 0) return NotFound("The system cannot identify this student.");

            var model = new PrintableScorecardViewModel
            {
                AcademicYear = "2026-2027",
                AcademicScores = new List<AcademicScoreLine>(),
                ValueScores = new List<ValueCourseScoreLine>()
            };

            

            using (var conn = new Npgsql.NpgsqlConnection(_connectionString))
            {
                conn.Open();

                // 1. Fetch Core Profile Data Details (Fixed verbatim string quotes and multi-line syntax)
                string studentSql = @"
            SELECT s.studentid, 
                   COALESCE(s.firstname, '') || ' ' || COALESCE(s.lastname, '') AS ""FullName"", 
                   s.institutename AS ""InstituteName"", 
                   standard 
            FROM view_wj_students_details_info s 
            WHERE s.studentID = @StudentID";
                using (var cmd = new Npgsql.NpgsqlCommand(studentSql, conn))
                {
                    cmd.Parameters.AddWithValue("StudentID", targetStudentId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            model.StudentName = rdr["FullName"].ToString();
                            model.InstituteName = rdr["InstituteName"].ToString();
                            model.Standard = rdr["Standard"].ToString();
                        }
                    }
                }

                // 2. Fetch Academic Records Matrix via PostgreSQL Function/Procedure
                using (var cmd = new Npgsql.NpgsqlCommand("SELECT * FROM \"HF_SP_GetStudentHolisticAcademicReport\"(@StudentID)", conn))
                {
                    cmd.Parameters.AddWithValue("StudentID", targetStudentId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            model.AcademicScores.Add(new AcademicScoreLine
                            {
                                SubjectName = rdr["SubjectName"].ToString(),
                                TermName = rdr["TermName"].ToString(),
                                MaxMarks = Convert.ToDecimal(rdr["MaxMarks"]),
                                MarksObtained = Convert.ToBoolean(rdr["IsAbsent"]) ? 0 : Convert.ToDecimal(rdr["MarksObtained"]),
                                Status = rdr["Status"].ToString()
                            });
                        }
                    }
                }

                // 3. Fetch Value-Based Score Matrix Rows
                string valueSql = "SELECT * FROM \"SP_GetStudentActivityLogMarks\"(@StudentID, @Standard)";

                using (var cmd = new Npgsql.NpgsqlCommand(valueSql, conn))
                {
                    cmd.Parameters.AddWithValue("StudentID", targetStudentId);
                    cmd.Parameters.AddWithValue("Standard", model.Standard ?? (object)DBNull.Value);

                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            int mcq = rdr["MCQ_MarksObtain"] != DBNull.Value ? Convert.ToInt32(rdr["MCQ_MarksObtain"]) : 0;
                            int practical = rdr["Practical_MarksObtain"] != DBNull.Value ? Convert.ToInt32(rdr["Practical_MarksObtain"]) : 0;
                            int pbl = rdr["PBL_MarksObtain"] != DBNull.Value ? Convert.ToInt32(rdr["PBL_MarksObtain"]) : 0;

                            model.ValueScores.Add(new ValueCourseScoreLine
                            {
                                CourseName = rdr["MainCourseName"]?.ToString() ?? "Unnamed Course",
                                McqMarks = mcq,
                                PracticalMarks = practical,
                                PblMarks = pbl,
                                GrandTotal = (mcq + practical + pbl)
                            });
                        }
                    }
                }
            }

            // Provide DefaultURL from session to the view (fallback to Dashboard index)
            var sessionUrl = HttpContext.Session.GetString("DefaultURL");
            ViewBag.DefaultURL = string.IsNullOrEmpty(sessionUrl) ? Url.Action("Index", "Home") : sessionUrl;

            return View(model);
        }
    }
}