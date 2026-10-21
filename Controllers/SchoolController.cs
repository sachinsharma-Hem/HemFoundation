using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing;
using HemFoundation.Common; // Required for DBManager
using HemFoundation.Data;
using HemFoundation.DTOs;
using HemFoundation.Models; 
using HemFoundation.Models.Entities; // Required for View_WJ_Teachercertificate (adjust namespace if needed)
using HemFoundation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration; // Required for IConfiguration
using Npgsql;
using Rotativa.AspNetCore;
using System.Collections.Generic;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HemFoundation.Controllers
{
    public class SchoolController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly ExceptionLoggerService _exceptionLogger;

        // Constructor injection for configuration
        public SchoolController(IConfiguration configuration, ApplicationDbContext context, ExceptionLoggerService exceptionLogger)
        {
            _configuration = configuration;
            _context = context;
            _exceptionLogger = exceptionLogger;
        }

        public IActionResult Index()
        {
            return View();
        }

        #region Certified Student Count 
        public ActionResult studentcertificate()
        {
            return View();
        }

        /// <summary>
        /// [HttpGet("certified-students-list")]
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("School/studentnewCertificate")] // <-- Add explicit route
        public async Task<IActionResult> studentnewCertificate()
        {
            // Retrieve InstituteID from session or claims safely
            int instituteID = 0;
            if (HttpContext.Session.GetInt32("UserID") is int sessionId && sessionId > 0)
            {
                instituteID = sessionId;
            }
            else if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedClaim))
            {
                instituteID = parsedClaim;
            }

            if (instituteID == 0)
            {
                return Unauthorized(new { success = false, message = "User session not found or user not logged in." });
            }

            List<View_WJ_Teachercertificate> model = new List<View_WJ_Teachercertificate>();

            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection");

                using (var conn = new Npgsql.NpgsqlConnection(connString))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT DISTINCT 
                            WVC.Id, 
                            VWSD.firstname || ' ' || VWSD.lastname AS ""StudentName"", 
                            VWSD.Standard, 
                            VWSD.InstituteName, 
                            VWSD.EmailID, 
                            VWSD.MobileNo, 
                            C.CourseID, 
                            C.CourseName 
                        FROM view_wj_students_details_info AS VWSD 
                        INNER JOIN WJ_Students AS WJS ON VWSD.StudentID = WJS.StudentID 
                        INNER JOIN Users AS U ON U.UserID = WJS.StudentID 
                        INNER JOIN WJ_StudentAssociates AS SA ON SA.StudentID = U.UserID 
                        LEFT JOIN WJ_Institutes AS WJI ON WJI.InstituteID = SA.InstituteID 
                        INNER JOIN WJ_Value_Certification AS WVC ON WVC.UserID = WJS.StudentID 
                        INNER JOIN Courses AS C ON C.CourseName = WVC.CourseName 
                        WHERE WJI.InstituteID = @InstituteID;
                    ";

                    using (var cmd = new Npgsql.NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("InstituteID", instituteID);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            DataTable dt = new DataTable();
                            dt.Load(reader);

                            // Convert DataTable to List using DBManager helper
                            model = DBManager.ConvertToList<View_WJ_Teachercertificate>(dt);
                        }
                    }
                }

                // Return JSON result directly using ASP.NET Core built-in serializer
                return Json(model);
            }
            catch (Exception ex)
            {
                // Ensure AddExceptionLogger is accessible via its namespace or replace with standard logging
                // AddExceptionLogger.Exhandlexception(ex);
                return BadRequest(new { success = false, message = ex.Message });
            }
        }


        [HttpGet]    
        public async Task<IActionResult> GetStudentCertificate(int eventID)
        {
            StudentCertificateModel model = new StudentCertificateModel();

            try
            {
                string base64ImageRepresentation = null;
                int courseId = 0;
                int instituteId = 0;
                int userId = 0;

                using (var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    // 1. Fetch record from wj_value_certification using eventID (Id)
                    string certQuery = @"
                SELECT id, certificationno, firstname, lastname, standard, institutename, 
                       coursename, courseid, instituteid, userid 
                FROM wj_value_certification 
                WHERE id = @EventID 
                LIMIT 1;";

                    using (var cmd = new NpgsqlCommand(certQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@EventID", eventID);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                courseId = reader["courseid"] != DBNull.Value ? Convert.ToInt32(reader["courseid"]) : 0;
                                instituteId = reader["instituteid"] != DBNull.Value ? Convert.ToInt32(reader["instituteid"]) : 0;
                                userId = reader["userid"] != DBNull.Value ? Convert.ToInt32(reader["userid"]) : 0;

                                model.CertificationNo = reader["certificationno"]?.ToString();
                                model.FirstName = reader["firstname"]?.ToString();
                                model.LastName = reader["lastname"]?.ToString();
                                model.Standard = reader["standard"]?.ToString();
                                model.InstituteName = reader["institutename"]?.ToString();
                                model.CourseName = reader["coursename"]?.ToString();
                            }
                        }
                    }

                    if (courseId > 0)
                    {
                        // 2. Fetch Main Course Name from view_wj_syllabus
                        string syllabusQuery = "SELECT maincoursename FROM view_wj_syllabus WHERE courseid = @CourseID LIMIT 1;";
                        using (var cmd = new NpgsqlCommand(syllabusQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@CourseID", courseId);
                            var res = await cmd.ExecuteScalarAsync();
                            model.MainCourseName = res?.ToString() ?? "";
                        }

                        // 3. Fetch Certificate Image/Bytes using your helper method
                        DataTable certificateData = await GetInstituteCertificateAsync(userId, courseId, instituteId);
                        if (certificateData.Rows.Count > 0 && certificateData.Rows[0]["certificate"] != DBNull.Value)
                        {
                            byte[] data = (byte[])certificateData.Rows[0]["certificate"];
                            base64ImageRepresentation = "data:image/png;base64," + Convert.ToBase64String(data);
                        }
                        else
                        {
                            TempData["Error"] = "No certificate found...";
                        }

                        // 4. Fetch Course Featured Image
                        string courseImgQuery = "SELECT featuredimage FROM courses WHERE courseid = @CourseID LIMIT 1;";
                        using (var cmd = new NpgsqlCommand(courseImgQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@CourseID", courseId);
                            model.CourseImage = (await cmd.ExecuteScalarAsync())?.ToString();
                        }

                        // 5. Fetch Institute/User Photo Url
                        string userPhotoQuery = "SELECT photourl FROM users WHERE userid = @InstituteID LIMIT 1;";
                        using (var cmd = new NpgsqlCommand(userPhotoQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@InstituteID", instituteId);
                            model.InstituteUrl = (await cmd.ExecuteScalarAsync())?.ToString();
                        }

                        model.CertificationTemplateUrl = base64ImageRepresentation;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

            return PartialView("~/Views/Learn/Shared/_GetStudentCertificateViewNew.cshtml", model);
        }

        private async Task<DataTable> GetInstituteCertificateAsync(int studentID, int courseID, int instituteID)
        {
            DataTable dt = new DataTable();
            using (var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection")))
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
        #endregion


        [HttpGet]
        public async Task<IActionResult> SubmissionStatus()
        {
            int instituteID = 0;
            if (HttpContext.Session.GetInt32("UserID") is int sessionId && sessionId > 0)
            {
                instituteID = sessionId;
            }
            else if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedClaim))
            {
                instituteID = parsedClaim;
            }

            string firstName = "";
            string lastName = "";
            string cityName = "";
            string instituteName = "";
            string pinCode = "";
            string coordinatorName = "";

            string connString = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();
                string query = @"
            SELECT instituteid, institute_name AS institutename, firstname, lastname, cityname, pincode 
            FROM public.vw_school_profile_details 
            WHERE instituteid = @InstituteID 
            LIMIT 1;
        ";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            instituteID = reader["instituteid"] != DBNull.Value ? Convert.ToInt32(reader["instituteid"]) : instituteID;
                            instituteName = reader["institutename"]?.ToString() ?? "";
                            firstName = reader["firstname"]?.ToString() ?? "";
                            lastName = reader["lastname"]?.ToString() ?? "";
                            cityName = reader["cityname"]?.ToString() ?? "";
                            pinCode = reader["pincode"]?.ToString() ?? "";
                            coordinatorName = $"{firstName} {lastName}".Trim();
                        }
                    }
                }
            }

            ViewBag.InstituteID = instituteID;
            ViewBag.InstituteName = instituteName;
            ViewBag.FirstName = firstName;
            ViewBag.LastName = lastName;
            ViewBag.CityName = cityName;
            ViewBag.CoordinatorName = coordinatorName;
            ViewBag.PinCode = pinCode;

            return View();
        }

        [HttpPost]
        public async Task<JsonResult> GetMainCourses(string Standard)
        {
            List<MainCourse> mainCourses = new List<MainCourse>();
            string connString = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();
                string query = @"
            SELECT DISTINCT m.maincourseid, m.maincoursename 
            FROM maincourses AS m 
            INNER JOIN view_wj_syllabus AS s ON s.maincourseid = m.maincourseid 
            WHERE @Standard = s.standard_;
        ";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Standard", Standard ?? "");
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            mainCourses.Add(new MainCourse
                            {
                                Id = reader["maincourseid"] != DBNull.Value ? Convert.ToInt32(reader["maincourseid"]) : 0,
                                Name = reader["maincoursename"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            return Json(mainCourses);
        }

        [HttpGet]
        public async Task<JsonResult> GetStatus(int course)
        {
            int instituteID = 0;
            if (HttpContext.Session.GetInt32("UserID") is int sessionId && sessionId > 0)
            {
                instituteID = sessionId;
            }
            else if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedClaim))
            {
                instituteID = parsedClaim;
            }

            string groupSelectionStatus = "Pending";
            string imageUploadStatus = "Pending";
            string videoUploadStatus = "Pending";
            string projectexplanationstatus = "Pending";

            string connString = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();

                // 1. Group Selection Check
                using (var cmd = new NpgsqlCommand("SELECT 1 FROM hf_rel_manage_group_list WHERE isallocatedby = @InstituteID AND maincourseid = @CourseID LIMIT 1;", conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                    cmd.Parameters.AddWithValue("@CourseID", course);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null) groupSelectionStatus = "Done";
                }

                // 2. Image Upload Check
                using (var cmd = new NpgsqlCommand("SELECT 1 FROM creativityactivitessubmission2021 WHERE instituteid = @InstituteID AND imageactivity IS NOT NULL AND maincourseid = @CourseID LIMIT 1;", conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                    cmd.Parameters.AddWithValue("@CourseID", course);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null) imageUploadStatus = "Done";
                }

                // 3. Video Upload Check
                using (var cmd = new NpgsqlCommand("SELECT 1 FROM creativityactivitessubmission2021 WHERE instituteid = @InstituteID AND videoactivity IS NOT NULL AND maincourseid = @CourseID LIMIT 1;", conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                    cmd.Parameters.AddWithValue("@CourseID", course);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null) videoUploadStatus = "Done";
                }

                // 4. Project Explanation Check
                using (var cmd = new NpgsqlCommand("SELECT 1 FROM creativityactivitessubmission2021 WHERE instituteid = @InstituteID AND shortdescription IS NOT NULL AND maincourseid = @CourseID LIMIT 1;", conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                    cmd.Parameters.AddWithValue("@CourseID", course);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null) projectexplanationstatus = "Done";
                }
            }

            return Json(new
            {
                groupSelectionStatus = groupSelectionStatus,
                imageUploadStatus = imageUploadStatus,
                videoUploadStatus = videoUploadStatus,
                projectexplanationstatus = projectexplanationstatus
            });
        }

        [HttpPost]
        public async Task<IActionResult> ExportSubmissionStatus()
        {
            int instituteID = 0;
            if (HttpContext.Session.GetInt32("UserID") is int sessionId && sessionId > 0)
            {
                instituteID = sessionId;
            }
            else if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedClaim))
            {
                instituteID = parsedClaim;
            }

            DataTable dt = new DataTable("SubmissionStatusReport");
            string connString = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();
                string sqlQuery = "SELECT * FROM public.\"SubmissionActivity_Status\"(@InstituteID);";

                using (var cmd = new NpgsqlCommand(sqlQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        dt.Load(reader);
                    }
                }
            }

            using (XLWorkbook wb = new XLWorkbook())
            {
                wb.Worksheets.Add(dt, "SubmissionStatusReport");

                using (MemoryStream stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    stream.Position = 0; // CRITICAL: Reset position to prevent 0 KB downloads
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SubmissionStatusReport.xlsx");
                }
            }
        }
        [HttpGet]
        public async Task<IActionResult> PrintInstituteReport(int? instituteId)
        {
            try
            {

                // If instituteId is not passed, get it from session
                if (!instituteId.HasValue || instituteId.Value <= 0)
                {
                    instituteId = HttpContext.Session.GetInt32("UserID");
                }

                // Still not available
                if (!instituteId.HasValue || instituteId.Value <= 0)
                {
                    return BadRequest("Institute ID is required.");
                }

                var model = await GetreportData(instituteId.Value);

                if (model == null)
                {
                    return NotFound("Institute report data not found.");
                }

                var pdf = new Rotativa.AspNetCore.ViewAsPdf(
                    "PrintReport",
                    model
                    )
                {
                    FileName = $"InstituteReport_{instituteId}.pdf",

                    PageSize = Rotativa.AspNetCore.Options.Size.A4,

                    PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,

                    PageMargins = new Rotativa.AspNetCore.Options.Margins
                    {
                        Top = 10,
                        Bottom = 10,
                        Left = 10,
                        Right = 10
                    },

                    CustomSwitches =
                        "--enable-local-file-access " +
                        "--javascript-delay 3000 " +
                        "--no-stop-slow-scripts " +
                        "--load-error-handling ignore " +
                        "--load-media-error-handling ignore " +
                        "--enable-javascript"
                       
                };

                pdf.ViewData["IsPdf"] = true;

                return pdf;
            
            }
            catch (Exception ex)
            {
                await _exceptionLogger.LogExceptionAsync(ex);
                // your existing exception logger can be used here
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Unable to generate institute report PDF."
                );
            }
        }
        public async Task<IActionResult> PrintReport(int? instituteId)
        {

            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                instituteId = HttpContext.Session.GetInt32("UserID");
            }

            // Still not available
            if (!instituteId.HasValue || instituteId.Value <= 0)
            {
                return BadRequest("Institute ID is required.");
            }

            ViewBag.IsPdf = true;

            // Get all 9 report result sets from PostgreSQL
            var model = await GetreportData(instituteId.Value);

            // Generate PDF using Rotativa
            return new ViewAsPdf("PrintReport", model)
            {
                FileName = "InstituteReport.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                CustomSwitches = "--javascript-delay 1000 --no-stop-slow-scripts"
            };
        }

        public async Task<SchoolReportViewModel> GetreportData(int instituteId)
        {
            var model = new SchoolReportViewModel();

            string connString = _configuration.GetConnectionString("DefaultConnection");

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            string batchedQuery = @"

        -- =========================================================
        -- 1. Institute Header Details
        -- =========================================================
        SELECT
            institute_name AS ""InstituteName"",
            CONCAT(addressline1, cityname) AS ""Address"",
            cityname AS ""City"",
            pincode AS ""Pincode"",
            emailid AS ""Email"",
            mobileno AS ""MobileNo""
        FROM view_wj_institutes
        WHERE instituteid = @InstituteID
        LIMIT 1;


        -- =========================================================
        -- 2. Student Counts
        -- =========================================================
        SELECT
            associatedstudents AS ""TotalStudents"",
            activestudents AS ""ActiveStudents"",
            (associatedstudents - activestudents) AS ""InactiveStudents""
        FROM vw_school_profile_details
        WHERE instituteid = @InstituteID
        LIMIT 1;


        -- =========================================================
        -- 3. Teacher Counts
        -- =========================================================
        SELECT
            totalteachers AS ""TotalTeacher"",
            activeteachers AS ""ActiveTeacher"",
            (totalteachers - activeteachers) AS ""InActiveTeacher""
        FROM vw_school_profile_details
        WHERE instituteid = @InstituteID
        LIMIT 1;


        -- =========================================================
        -- 4. Teacher Course Summary
        -- =========================================================
        SELECT
            teacher_name AS ""Teacher_Name"",
            coursecompleted AS ""CourseCompleted"",
            coursepending AS ""CoursePending"",
            noofcoursecompleted AS ""NoOFCourseCompleted"",
            noofcoursepending AS ""NoofCoursePending"",
            certifiedinmaincoursecount AS ""CertifiedinMainCourseCount"",
            totalobtainedmarks AS ""TotalObtainedMarks""
        FROM hf_view_teachercoursesummary
        WHERE instituteid = @InstituteID;


        -- =========================================================
        -- 5. Standard Wise Student Summary
        -- =========================================================
        SELECT
            ""Standard"",
            ""TotalStudents"",
            ""ActiveStudents"",
            ""InactiveStudents""
        FROM hf_view_institutestandardstudentsummary
        WHERE ""InstituteID"" = @InstituteID
        ORDER BY ""Standard"";


        -- =========================================================
        -- 6. Top 10 Institute Performers
        -- =========================================================
        SELECT
            standard AS ""Standard"",
            student_name AS ""Student_Name"",
            totalpoints AS ""TotalPoints"",
            instrank AS ""InstRank""
        FROM hf_view_institutewisetop10studentperformer
        WHERE instituteid = @InstituteID
        ORDER BY instrank
        LIMIT 10;


        -- =========================================================
        -- 7. Standard Wise Top 3 Performers
        -- =========================================================
        SELECT
            standard AS ""Standard"",
            student_name AS ""Student_Name"",
            totalpoints AS ""TotalPoints"",
            stdrank AS ""StdRank""
        FROM hf_view_institutewisestdwiseTop3studentperformer
        WHERE instituteid = @InstituteID
        ORDER BY standard, stdrank;


        -- =========================================================
        -- 8. Project Summary
        -- =========================================================
        SELECT
            standard AS ""Standard"",
            totalstudents AS ""TotalStudents"",
            grouppblcompleted AS ""GroupPBLCompleted"",
            individualpblcompleted AS ""IndividualPBLCompleted"",
            familypblcompleted AS ""FamilyPBLCompleted""
        FROM hf_view_institutestudentpblsummary
        WHERE instituteid = @InstituteID
        ORDER BY standard;


        -- =========================================================
        -- 9. Students Wise Certified List
        -- =========================================================
        SELECT
            ""Student_Name"",
            ""Standard_"",
            ""No_of_Courses_Completed"",
           ""No_of_Courses_Pending"",
            ""MarksObtain"",
            ""Courses_Completed"",
            ""Courses_Pending""
        FROM hf_view_studentwisecompletedmaincourselistascertificatewise
        WHERE ""InstituteID"" = @InstituteID;

    ";

            await using var cmd = new NpgsqlCommand(batchedQuery, conn);

            cmd.Parameters.AddWithValue("@InstituteID", instituteId);

            await using var reader = await cmd.ExecuteReaderAsync();

            // =============================================================
            // RESULT SET 1 : Institute Header
            // =============================================================
            if (await reader.ReadAsync())
            {
                model.Header = new InstituteDetails
                {
                    InstituteName = reader["InstituteName"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["InstituteName"]),

                    Address = reader["Address"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Address"]),

                    City = reader["City"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["City"]),

                    Pincode = reader["Pincode"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Pincode"]),

                    Email = reader["Email"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Email"]),

                    MobileNo = reader["MobileNo"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["MobileNo"])
                };
            }

            // =============================================================
            // RESULT SET 2 : Student Counts
            // =============================================================
            await reader.NextResultAsync();

            if (await reader.ReadAsync())
            {
                int totalStudents = reader["TotalStudents"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(reader["TotalStudents"]);

                int activeStudents = reader["ActiveStudents"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(reader["ActiveStudents"]);

                int inactiveStudents = reader["InactiveStudents"] == DBNull.Value
                    ? totalStudents - activeStudents
                    : Convert.ToInt32(reader["InactiveStudents"]);

                model.studentStats = new StudentStats
                {
                    TotalStudents = totalStudents,
                    ActiveStudents = activeStudents,
                    InactiveStudents = inactiveStudents
                };
            }
            else
            {
                model.studentStats = new StudentStats
                {
                    TotalStudents = 0,
                    ActiveStudents = 0,
                    InactiveStudents = 0
                };
            }

            // =============================================================
            // RESULT SET 3 : Teacher Counts
            // =============================================================
            await reader.NextResultAsync();

            if (await reader.ReadAsync())
            {
                int totalTeachers = reader["TotalTeacher"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(reader["TotalTeacher"]);

                int activeTeachers = reader["ActiveTeacher"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(reader["ActiveTeacher"]);

                int inactiveTeachers = reader["InActiveTeacher"] == DBNull.Value
                    ? totalTeachers - activeTeachers
                    : Convert.ToInt32(reader["InActiveTeacher"]);

                model.teacherStats = new TeacherStats
                {
                    TotalTeacher = totalTeachers,
                    ActiveTeacher = activeTeachers,
                    InActiveTeacher = inactiveTeachers
                };
            }
            else
            {
                model.teacherStats = new TeacherStats
                {
                    TotalTeacher = 0,
                    ActiveTeacher = 0,
                    InActiveTeacher = 0
                };
            }

            // =============================================================
            // RESULT SET 4 : Teacher Course Summary
            // =============================================================
            await reader.NextResultAsync();

            model.teacherCourseSummary = new List<TeacherCourseSummary>();

            while (await reader.ReadAsync())
            {
                model.teacherCourseSummary.Add(new TeacherCourseSummary
                {
                    Teacher_Name = reader["Teacher_Name"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Teacher_Name"]),

                    CourseCompleted = reader["CourseCompleted"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["CourseCompleted"]),

                    CoursePending = reader["CoursePending"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["CoursePending"]),

                    NoOFCourseCompleted = reader["NoOFCourseCompleted"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["NoOFCourseCompleted"]),

                    NoofCoursePending = reader["NoofCoursePending"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["NoofCoursePending"]),

                    CertifiedinMainCourseCount = reader["CertifiedinMainCourseCount"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["CertifiedinMainCourseCount"]),

                    TotalObtainedMarks = reader["TotalObtainedMarks"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["TotalObtainedMarks"])
                });
            }

            // =============================================================
            // RESULT SET 5 : Standard Wise Student Summary
            // =============================================================
            await reader.NextResultAsync();

            model.standardSummaries = new List<StandardSummary>();

            while (await reader.ReadAsync())
            {
                model.standardSummaries.Add(new StandardSummary
                {
                    Standard = reader["Standard"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Standard"]),

                    TotalStudents = reader["TotalStudents"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["TotalStudents"]),

                    ActiveStudents = reader["ActiveStudents"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["ActiveStudents"]),

                    InactiveStudents = reader["InactiveStudents"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["InactiveStudents"])
                });
            }

            // =============================================================
            // RESULT SET 6 : Top 10 Institute Performers
            // =============================================================
            await reader.NextResultAsync();

            model.top10Performer = new List<Top10Performer>();

            while (await reader.ReadAsync())
            {
                model.top10Performer.Add(new Top10Performer
                {
                    Standard = reader["Standard"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Standard"]),

                    Student_Name = reader["Student_Name"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Student_Name"]),

                    TotalPoints = reader["TotalPoints"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["TotalPoints"]),

                    InstRank = reader["InstRank"] == DBNull.Value
                        ? 0
                        : Convert.ToInt64(reader["InstRank"])
                });
            }

            // =============================================================
            // RESULT SET 7 : Standard Wise Top 3 Performers
            // =============================================================
            await reader.NextResultAsync();

            model.Top3StdPerformers = new List<TopStd3Performer>();

            while (await reader.ReadAsync())
            {
                model.Top3StdPerformers.Add(new TopStd3Performer
                {
                    Standard = reader["Standard"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Standard"]),

                    Student_Name = reader["Student_Name"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Student_Name"]),

                    TotalPoints = reader["TotalPoints"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["TotalPoints"]),

                    StdRank = reader["StdRank"] == DBNull.Value
                        ? 0
                        : Convert.ToInt64(reader["StdRank"])
                });
            }

            // =============================================================
            // RESULT SET 8 : Project Summary
            // =============================================================
            await reader.NextResultAsync();

            model.ProjectSummaries = new List<ProjectStats>();

            while (await reader.ReadAsync())
            {
                model.ProjectSummaries.Add(new ProjectStats
                {
                    Standard = reader["Standard"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Standard"]),

                    TotalStudents = reader["TotalStudents"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["TotalStudents"]),

                    GroupPBLCompleted = reader["GroupPBLCompleted"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["GroupPBLCompleted"]),

                    IndividualPBLCompleted = reader["IndividualPBLCompleted"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["IndividualPBLCompleted"]),

                    FamilyPBLCompleted = reader["FamilyPBLCompleted"] == DBNull.Value
                        ? 0
                        : Convert.ToInt32(reader["FamilyPBLCompleted"])
                });
            }

            // =============================================================
            // RESULT SET 9 : Students Wise Certified List
            // =============================================================
            await reader.NextResultAsync();

            model.studentsCertifiedStats = new List<StudentsCertifiedStats>();

            while (await reader.ReadAsync())
            {
                model.studentsCertifiedStats.Add(new StudentsCertifiedStats
                {
                    Student_Name = reader["Student_Name"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Student_Name"]),

                    Standard_ = reader["Standard_"] == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(reader["Standard_"]),

                    No_of_Courses_Completed =
                        reader["No_of_Courses_Completed"] == DBNull.Value
                            ? 0
                            : Convert.ToInt32(reader["No_of_Courses_Completed"]),

                    No_of_Courses_Pending =
                        reader["No_of_Courses_Pending"] == DBNull.Value
                            ? 0
                            : Convert.ToInt32(reader["No_of_Courses_Pending"]),

                    MarksObtain =
                        reader["MarksObtain"] == DBNull.Value
                            ? 0
                            : Convert.ToInt32(reader["MarksObtain"]),

                    Courses_Completed =
                        reader["Courses_Completed"] == DBNull.Value
                            ? string.Empty
                            : Convert.ToString(reader["Courses_Completed"]),

                    Courses_Pending =
                        reader["Courses_Pending"] == DBNull.Value
                            ? string.Empty
                            : Convert.ToString(reader["Courses_Pending"])
                });
            }

            return model;
        }
        public async Task<SchoolReportViewModel> GetreportData1(int instituteId)
        {
            var model = new SchoolReportViewModel();
            string connString = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();

                // Batched SQL containing all 9 query blocks in exact order of reading
                string batchedQuery = @"

            -- =========================================================
            -- 1. Institute Header Details
            -- =========================================================
            SELECT
                institute_name AS ""InstituteName"",
                CONCAT(addressline1, cityname) AS ""Address"",
                cityname AS ""City"",
                pincode AS ""Pincode"",
                emailid AS ""Email"",
                mobileno AS ""MobileNo""
            FROM view_wj_institutes
            WHERE instituteid = 442074
            LIMIT 1;


            -- =========================================================
            -- 2. Student Counts
            -- =========================================================
            SELECT
                associatedstudents AS ""TotalStudents"",
                activestudents AS ""ActiveStudents"",
                (associatedstudents-activestudents) AS ""InactiveStudents""
            FROM vw_school_profile_details
            WHERE instituteid = @InstituteID
            LIMIT 1;


            -- =========================================================
            -- 3. Teacher Counts
            -- =========================================================
            SELECT
                totalteachers AS ""TotalTeacher"",
                activeteachers AS ""ActiveTeacher"",
                (totalteachers-activeteachers) AS ""InActiveTeacher""
            FROM vw_school_profile_details
            WHERE instituteid = @InstituteID
            LIMIT 1;


            -- =========================================================
            -- 4. Teacher Course Summary
            -- =========================================================
            SELECT
                teacher_name AS ""Teacher_Name"",
                coursecompleted AS ""CourseCompleted"",
                coursepending AS ""CoursePending"",
                noofcoursecompleted AS ""NoOFCourseCompleted"",
                noofcoursepending AS ""NoofCoursePending"",
                certifiedinmaincoursecount AS ""CertifiedinMainCourseCount"",
                totalobtainedmarks AS ""TotalObtainedMarks""
            FROM hf_view_teachercoursesummary
            WHERE instituteid = @InstituteID;


            -- =========================================================
            -- 5. Standard Wise Student Summary
            -- =========================================================
            SELECT
                 ""Standard"",
                ""TotalStudents"",
                ""ActiveStudents"",
                 ""InactiveStudents""
            FROM hf_view_institutestandardstudentsummary
            WHERE instituteid = @InstituteID
            ORDER BY standard;


            -- =========================================================
            -- 6. Top 10 Institute Performers
            -- =========================================================
            SELECT
                standard AS ""Standard"",
                student_name AS ""Student_Name"",
                totalpoints AS ""TotalPoints"",
                instrank AS ""InstRank""
            FROM hf_view_institutewisetop10studentperformer
            WHERE instituteid = @InstituteID
            ORDER BY instrank
            LIMIT 10;


            -- =========================================================
            -- 7. Standard Wise Top 3 Performers
            -- =========================================================
            SELECT
                standard AS ""Standard"",
                student_name AS ""Student_Name"",
                totalpoints AS ""TotalPoints"",
                stdrank AS ""StdRank""
            FROM hf_view_institutewisestdwiseTop3studentperformer
            WHERE instituteid = @InstituteID
            ORDER BY standard, stdrank;


            -- =========================================================
            -- 8. Project Summary
            -- =========================================================
            SELECT
                standard AS ""Standard"",
                totalstudents AS ""TotalStudents"",
                grouppblcompleted AS ""GroupPBLCompleted"",
                individualpblcompleted AS ""IndividualPBLCompleted"",
                familypblcompleted AS ""FamilyPBLCompleted""
            FROM hf_view_institutestudentpblsummary
            WHERE instituteid = @InstituteID
            ORDER BY standard;


            -- =========================================================
            -- 9. Students Wise Certified List
            -- =========================================================
            SELECT
                student_name AS ""Student_Name"",
                standard_ AS ""Standard_"",
                no_of_courses_completed AS ""No_of_Courses_Completed"",
                no_of_courses_pending AS ""No_of_Courses_Pending"",
                marksobtain AS ""MarksObtain"",
                courses_completed AS ""Courses_Completed"",
                courses_pending AS ""Courses_Pending""
            FROM hf_view_studentwisecompletedmaincourselistascertificatewise
            WHERE instituteid = @InstituteID;

            ";
                using (var cmd = new NpgsqlCommand(batchedQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", instituteId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        // ---------------------------------------------------------
                        // Read 1: Header & Profile Counts (Students & Teachers)
                        // ---------------------------------------------------------
                        if (await reader.ReadAsync())
                        {
                            model.Header = new InstituteDetails
                            {
                                InstituteName = Convert.ToString(reader["InstituteName"]),
                                Address = Convert.ToString(reader["Address"]),
                                City = Convert.ToString(reader["City"]),
                                Pincode = Convert.ToString(reader["Pincode"]),
                                Email = Convert.ToString(reader["Email"]),
                                MobileNo = Convert.ToString(reader["MobileNo"])
                            };

                            // Extract student stats from vw_school_profile_details
                            int totalStudents = reader["associatedstudents"] == DBNull.Value ? 0 : Convert.ToInt32(reader["associatedstudents"]);
                            int activeStudents = reader["activestudents"] == DBNull.Value ? 0 : Convert.ToInt32(reader["activestudents"]);
                            model.studentStats = new StudentStats
                            {
                                TotalStudents = totalStudents,
                                ActiveStudents = activeStudents,
                                InactiveStudents = totalStudents - activeStudents // Inactive minus active from total
                            };

                            // Extract teacher stats from vw_school_profile_details
                            int totalTeachers = reader["totalteachers"] == DBNull.Value ? 0 : Convert.ToInt32(reader["totalteachers"]);
                            int activeTeachers = reader["activeteachers"] == DBNull.Value ? 0 : Convert.ToInt32(reader["activeteachers"]);
                            model.teacherStats = new TeacherStats
                            {
                                TotalTeacher = totalTeachers,
                                ActiveTeacher = activeTeachers,
                                InActiveTeacher = totalTeachers - activeTeachers // Inactive minus active from total
                            };
                        }

                        // ---------------------------------------------------------
                        // Read 2: Skip or remove separate Student Stats query block 
                        // since it's now handled entirely in Read 1 from profile details!
                        // ---------------------------------------------------------
                        // ---------------------------------------------------------
                        // Read 2: Skip or remove separate Student Stats query block 
                        // ---------------------------------------------------------
                        await reader.NextResultAsync();

                        // ---------------------------------------------------------
                        // Read 3: Skip or remove separate Teacher Counts query block 
                        // ---------------------------------------------------------
                        await reader.NextResultAsync();

                        // ---------------------------------------------------------
                        // Read 4: Teacher Course Summary
                        // ---------------------------------------------------------
                        await reader.NextResultAsync();
                        model.teacherCourseSummary = new List<TeacherCourseSummary>();
                        while (await reader.ReadAsync())
                        {
                            model.teacherCourseSummary.Add(new TeacherCourseSummary
                            {
                                Teacher_Name = Convert.ToString(reader["Teacher_Name"]),
                                CourseCompleted = Convert.ToString(reader["CourseCompleted"]),
                                CoursePending = Convert.ToString(reader["CoursePending"]),
                                NoOFCourseCompleted = reader["NoOFCourseCompleted"] == DBNull.Value ? 0 : Convert.ToInt32(reader["NoOFCourseCompleted"]),
                                NoofCoursePending = reader["NoofCoursePending"] == DBNull.Value ? 0 : Convert.ToInt32(reader["NoofCoursePending"]),
                                CertifiedinMainCourseCount = reader["CertifiedinMainCourseCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CertifiedinMainCourseCount"]),
                                TotalObtainedMarks = reader["TotalObtainedMarks"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalObtainedMarks"])
                            });
                        }

                        // ---------------------------------------------------------
                        // Read 5: Standard Summaries
                        // ---------------------------------------------------------
                        await reader.NextResultAsync();
                        model.standardSummaries = new List<StandardSummary>();
                        while (await reader.ReadAsync())
                        {
                            model.standardSummaries.Add(new StandardSummary
                            {
                                Standard = Convert.ToString(reader["Standard"]),
                                TotalStudents = reader["TotalStudents"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalStudents"]),
                                ActiveStudents = reader["ActiveStudents"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ActiveStudents"]),
                                InactiveStudents = reader["InactiveStudents"] == DBNull.Value ? 0 : Convert.ToInt32(reader["InactiveStudents"])
                            });
                        }

                        // ---------------------------------------------------------
                        // Read 6: Top 10 Performers
                        // ---------------------------------------------------------
                        await reader.NextResultAsync();
                        model.top10Performer = new List<Top10Performer>();
                        while (await reader.ReadAsync())
                        {
                            model.top10Performer.Add(new Top10Performer
                            {
                                Standard = Convert.ToString(reader["Standard"]),
                                Student_Name = Convert.ToString(reader["student_name"]),
                                InstRank = reader["InstRank"] == DBNull.Value ? 0 : Convert.ToInt64(reader["InstRank"]),
                                TotalPoints = reader["totalpoints"] == DBNull.Value ? 0 : Convert.ToInt32(reader["totalpoints"])
                            });
                        }

                        // ---------------------------------------------------------
                        // Read 7: Std wise top 3 performer
                        // ---------------------------------------------------------
                        await reader.NextResultAsync();
                        model.Top3StdPerformers = new List<TopStd3Performer>();
                        while (await reader.ReadAsync())
                        {
                            model.Top3StdPerformers.Add(new TopStd3Performer
                            {
                                Standard = Convert.ToString(reader["Standard"]),
                                Student_Name = Convert.ToString(reader["student_name"]),
                                TotalPoints = reader["totalpoints"] == DBNull.Value ? 0 : Convert.ToInt32(reader["totalpoints"]),
                                StdRank = reader["StdRank"] == DBNull.Value ? 0 : Convert.ToInt64(reader["StdRank"])
                            });
                        }

                        // ---------------------------------------------------------
                        // Read 8: Project Summary
                        // ---------------------------------------------------------
                        await reader.NextResultAsync();
                        model.ProjectSummaries = new List<ProjectStats>();
                        while (await reader.ReadAsync())
                        {
                            model.ProjectSummaries.Add(new ProjectStats
                            {
                                Standard = Convert.ToString(reader["Standard"]),
                                TotalStudents = reader["TotalStudents"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TotalStudents"]),
                                GroupPBLCompleted = reader["GroupPBLCompleted"] == DBNull.Value ? 0 : Convert.ToInt32(reader["GroupPBLCompleted"]),
                                IndividualPBLCompleted = reader["IndividualPBLCompleted"] == DBNull.Value ? 0 : Convert.ToInt32(reader["IndividualPBLCompleted"]),
                                FamilyPBLCompleted = reader["FamilyPBLCompleted"] == DBNull.Value ? 0 : Convert.ToInt32(reader["FamilyPBLCompleted"])
                            });
                        }

                        // ---------------------------------------------------------
                        // Read 9: Students wise certified list
                        // ---------------------------------------------------------
                        await reader.NextResultAsync();
                        model.studentsCertifiedStats = new List<StudentsCertifiedStats>();
                        while (await reader.ReadAsync())
                        {
                            model.studentsCertifiedStats.Add(new StudentsCertifiedStats
                            {
                                Student_Name = Convert.ToString(reader["Student_Name"]),
                                Standard_ = Convert.ToString(reader["Standard_"]),
                                No_of_Courses_Completed = reader["No_of_Courses_Completed"] == DBNull.Value ? 0 : Convert.ToInt32(reader["No_of_Courses_Completed"]),
                                No_of_Courses_Pending = reader["No_of_Courses_Pending"] == DBNull.Value ? 0 : Convert.ToInt32(reader["No_of_Courses_Pending"]),
                                MarksObtain = reader["MarksObtain"] == DBNull.Value ? 0 : Convert.ToInt32(reader["MarksObtain"]),
                                Courses_Completed = Convert.ToString(reader["Courses_Completed"]),
                                Courses_Pending = Convert.ToString(reader["Courses_Pending"])
                            });
                        }
                    }
                }
            }
            return model;
        }

        [HttpGet]
        public IActionResult TestPdf()
        {
            return new Rotativa.AspNetCore.ViewAsPdf("TestPdf")
            {
                FileName = "Test.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                CustomSwitches =
                    "--enable-local-file-access " +
                    "--javascript-delay 1000"
            };
        }
    }

    internal class View_WJ_Teachercertificate
    {
        public string StudentName { get; set; }
        public string Standard { get; set; }
        public string InstituteName { get; set; }
        public string EmailID { get; set; }
        public string MobileNo { get; set; }
        public int CourseID { get; set; }
        public string CourseName { get; set; }

        public int Id { get; set; }
        public string CertificationNo { get; set; }
        public string Type { get; set; }
        public Nullable<int> UserID { get; set; }
    }
}