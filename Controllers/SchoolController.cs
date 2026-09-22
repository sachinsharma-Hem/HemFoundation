using HemFoundation.Common; // Required for DBManager
using HemFoundation.Models.Entities; // Required for View_WJ_Teachercertificate (adjust namespace if needed)
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration; // Required for IConfiguration
using Npgsql;
using System.Collections.Generic;
using System.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using HemFoundation.Models; 

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

            return PartialView("~/Views/Admin/Shared/_GetStudentCertificateView.cshtml", model);
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