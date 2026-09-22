using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing;
using HemFoundation.Common; // Required for DBManager
using HemFoundation.Models; 
using HemFoundation.Models.Entities; // Required for View_WJ_Teachercertificate (adjust namespace if needed)
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration; // Required for IConfiguration
using Npgsql;
using System.Collections.Generic;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using HemFoundation.DTOs;

namespace HemFoundation.Controllers
{
    public class SchoolController : Controller
    {
        private readonly IConfiguration _configuration;

        // Constructor injection for configuration
        public SchoolController(IConfiguration configuration)
        {
            _configuration = configuration;
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
        public async Task<IActionResult> PrintInstituteReport(int instituteId)
        {
            var model = await GetreportData(instituteId);

            // Flag to tell the View we are in "PDF Mode"
            ViewBag.IsPdf = true;

            // Note: Rotativa may require rotativa.aspnetcore NuGet package in .NET Core
            return new Rotativa.AspNetCore.ViewAsPdf("PrintReport", model)
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

            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();

                // Batched SQL containing all 9 query blocks in exact order of reading
                string batchedQuery = @"
                            -- 1. Header & Profile Stats (Students & Teachers)
                           SELECT institute_name AS InstituteName,addressline1 Address,cityname City,pincode Pincode,
                                 emailid Email,mobileno MobileNo, 
                                        associatedstudents, activestudents, totalteachers, activeteachers 
                                 FROM vw_school_profile_details
                            WHERE instituteid = @InstituteID LIMIT 1;

                            -- 2. Placeholder/Skipped (Integrated into Read 1)
                            SELECT 1;

                            -- 3. Placeholder/Skipped (Integrated into Read 1)
                            SELECT 1;

                            -- 4. Teacher Course Summary
                            SELECT 
                                u.firstname || ' ' || u.lastname AS Teacher_Name,
                                '' AS CourseCompleted,
                                '' AS CoursePending,
                                0 AS NoOFCourseCompleted,
                                0 AS NoofCoursePending,
                                0 AS CertifiedinMainCourseCount,
                                0 AS TotalObtainedMarks
                            FROM wj_teacherassociates AS wjta
                            INNER JOIN users AS u ON u.userid = wjta.teacherid
                            WHERE wjta.instituteid = @InstituteID;

                            -- 5. Standard Summaries
                            SELECT 
                                standard AS Standard,
                                COUNT(*) AS TotalStudents,
                                SUM(CASE WHEN isactive THEN 1 ELSE 0 END) AS ActiveStudents,
                                SUM(CASE WHEN NOT isactive THEN 1 ELSE 0 END) AS InactiveStudents
                            FROM view_wj_students_details_info 
                            WHERE instituteid = @InstituteID
                            GROUP BY standard;

                            -- 6. Top 10 Performers
                           SELECT ""Standard"", ""Student_Name"", ""TotalPoints"", ""InstRank""        
                            FROM ""HF_View_InstitutewiseTop10StudentPerformer"" 
                            WHERE ""InstituteID"" = @InstituteID;

                            -- 7. Std wise top 3 performer
                            SELECT standard, student_name, totalpoints, 1 AS StdRank 
                            FROM view_wj_students_details_info 
                            WHERE instituteid = @InstituteID 
                            ORDER BY standard, totalpoints DESC;

                            -- 8. Project Summary
                            SELECT 
                                standard AS Standard,
                                COUNT(*) AS TotalStudents,
                                0 AS GroupPBLCompleted,
                                0 AS IndividualPBLCompleted,
                                0 AS FamilyPBLCompleted
                            FROM view_wj_students_details_info 
                            WHERE instituteid = @InstituteID
                            GROUP BY standard;

                            -- 9. Students wise certified list
                            SELECT 
                                student_name AS Student_Name,
                                standard AS Standard_,
                                0 AS No_of_Courses_Completed,
                                0 AS No_of_Courses_Pending,
                                0 AS MarksObtain,
                                '' AS Courses_Completed,
                                '' AS Courses_Pending
                            FROM view_wj_students_details_info 
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