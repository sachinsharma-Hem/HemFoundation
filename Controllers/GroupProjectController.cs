using HemFoundation.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HemFoundation.Services
{
    public interface IGroupProjectService
    {
        Task<string> RaiseRequestAsync(int courseid, int userid);
        Task<string> RejectRequestAsync(int courseid, int studentsID, int instituteID);
        Task<(bool Success, string HtmlContent, string Message)> ApproveRequestAsync(int courseid, int studentsID, int instituteID);
    }

    public class GroupProjectService : IGroupProjectService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string _connectionString;

        public GroupProjectService(IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            _webHostEnvironment = webHostEnvironment;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<string> RaiseRequestAsync(int courseid, int userid)
        {
            string coursename = "";
            string institutename = "";
            string firstName = "";
            string lastName = "";
            int instituteID = 0;
            string standard = "";
            string emailID = "";

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // 1. Fetch User details
                using (var cmd = new NpgsqlCommand("SELECT firstname, lastname, emailid FROM users WHERE userid = @UserID", conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", userid);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            firstName = reader["firstname"]?.ToString() ?? "";
                            lastName = reader["lastname"]?.ToString() ?? "";
                            emailID = reader["emailid"]?.ToString() ?? "";
                        }
                    }
                }

                // 2. Fetch Student Standard
                using (var cmd = new NpgsqlCommand("SELECT standard FROM wj_students WHERE studentid = @StudentID", conn))
                {
                    cmd.Parameters.AddWithValue("@StudentID", userid);
                    var res = await cmd.ExecuteScalarAsync();
                    standard = res?.ToString() ?? "";
                }

                // 3. Fetch Student Associate Institute ID
                using (var cmd = new NpgsqlCommand("SELECT instituteid FROM wj_studentassociates WHERE studentid = @StudentID", conn))
                {
                    cmd.Parameters.AddWithValue("@StudentID", userid);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null && int.TryParse(res.ToString(), out int instId))
                    {
                        instituteID = instId;
                    }
                }

                // 4. Fetch Institute Details
                string instituteEmail = "";
                using (var cmd = new NpgsqlCommand("SELECT i.\"Name\" ,emailid from users  u\r\nLeft join wj_institutes i on u.userid=i.instituteid\r\nwhere userid = @InstituteID", conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            institutename = reader["Name"]?.ToString() ?? "";
                            instituteEmail = reader["emailid"]?.ToString() ?? "";
                        }
                    }
                }

                // 5. Fetch Course & MainCourse ID details
                int mainCourseID = 0;
                using (var cmd = new NpgsqlCommand("SELECT coursename, maincourseid FROM view_wj_syllabus WHERE courseid = @CourseID", conn))
                {
                    cmd.Parameters.AddWithValue("@CourseID", courseid);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            coursename = reader["coursename"]?.ToString() ?? "";
                            mainCourseID = reader["maincourseid"] != DBNull.Value ? Convert.ToInt32(reader["maincourseid"]) : courseid;
                        }
                    }
                }

                // 6. Build and send email notification
                string host = "https://www.hemvirtues.com";
                string requestrejecturl = $"{host}/GroupProject/RejectRequest?courseid={mainCourseID}&StudntsID={userid}&InstituteID={instituteID}";
                string requestApproveRequestturl = $"{host}/GroupProject/ApproveRequest?courseid={mainCourseID}&StudntsID={userid}&InstituteID={instituteID}";

                string templatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", "template", "RequestForGroupProject.html");
                if (File.Exists(templatePath))
                {
                    StringBuilder body = new StringBuilder(File.ReadAllText(templatePath));
                    body.Replace("<%MainCourseName%>", coursename);
                    body.Replace("<%studentsname%>", $"{firstName} {lastName}");
                    body.Replace("<%studentsID%>", userid.ToString());
                    body.Replace("<%std%>", standard);
                    body.Replace("<%instituteName%>", institutename);
                    body.Replace("<%requestrejecturl%>", requestrejecturl);
                    body.Replace("<%requestApproveRequestturl%>", requestApproveRequestturl);

                    EmailManager.SendEmailAws(instituteEmail, $"Request for Group Project Mapping – {firstName} {lastName} , {userid}", body.ToString(), null, "", "");
                }
            }

            return "Your request has been successfully processed.";
        }

        // Add corresponding service methods for RejectRequest and ApproveRequest if needed...
        public async Task<string> RejectRequestAsync(int courseid, int studentsID, int instituteID)
        {
            string templatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "template", "Approval.html");
            StringBuilder body = new StringBuilder(File.Exists(templatePath) ? File.ReadAllText(templatePath) : "");

            string firstName = "";
            string studentEmail = "";
            string instituteName = "";
            string instituteEmail = "";

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Fetch User Details
                using (var cmd = new NpgsqlCommand("SELECT firstname, emailid FROM users WHERE userid = @UserID", conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", studentsID);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            firstName = reader["firstname"]?.ToString() ?? "";
                            studentEmail = reader["emailid"]?.ToString() ?? "";
                        }
                    }
                }

                // Fetch Institute Details
                using (var cmd = new NpgsqlCommand("SELECT i.\"Name\" ,emailid from users  u\r\nLeft join wj_institutes i on u.userid=i.instituteid\r\nwhere userid = @InstituteID", conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            instituteName = reader["Name"]?.ToString() ?? "";
                            instituteEmail = reader["emailid"]?.ToString() ?? "";
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(instituteName))
            {
                return "<html><body><h3>Error: Institute not found</h3></body></html>";
            }

            body.Replace("<%action%>", "Reject");
            body.Replace("<%instituteName%>", instituteName);

            EmailManager.SendEmailAws(instituteEmail, "Confirmation For Rejection Of Group Project Submission", body.ToString(), null, "", "");

            string studentTemplatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "template", "ApprovalConfirmationForStudents.html");
            StringBuilder body1 = new StringBuilder(File.Exists(studentTemplatePath) ? File.ReadAllText(studentTemplatePath) : "");

            body1.Replace("<%action%>", "Rejected");
            body1.Replace("<%studentsname%>", firstName);
            if (!string.IsNullOrEmpty(studentEmail) && studentEmail != "null")
            {
                EmailManager.SendEmailAws(studentEmail, "Confirmation For Rejection Of Group Project Submission by the school", body1.ToString(), null, "", "");
            }

            return body.ToString();
        }

        public async Task<(bool Success, string HtmlContent, string Message)> ApproveRequestAsync(int courseid, int studentsID, int instituteID)
        {
            string templatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "template", "Approval.html");
            StringBuilder body = new StringBuilder(File.Exists(templatePath) ? File.ReadAllText(templatePath) : "");

            string firstName = "";
            string studentEmail = "";
            string instituteName = "";
            string instituteEmail = "";

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // 1. Fetch User details
                using (var cmd = new NpgsqlCommand("SELECT firstname, emailid FROM users WHERE userid = @UserID", conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", studentsID);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            firstName = reader["firstname"]?.ToString() ?? "";
                            studentEmail = reader["emailid"]?.ToString() ?? "";
                        }
                    }
                }

                // 2. Check if already exists
                bool isAlreadyExists = false;
                using (var cmd = new NpgsqlCommand("SELECT 1 FROM hf_rel_manage_group_list WHERE isallocatedby = @InstituteID AND maincourseid = @CourseID AND studentid = @StudentID LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                    cmd.Parameters.AddWithValue("@CourseID", courseid);
                    cmd.Parameters.AddWithValue("@StudentID", studentsID);
                    var res = await cmd.ExecuteScalarAsync();
                    isAlreadyExists = (res != null);
                }

                if (isAlreadyExists)
                {
                    return (false, "", "Your request for the Approval of students for the group project submission has been already submitted!!.");
                }

                // 3. Fetch Institute details
                using (var cmd = new NpgsqlCommand("SELECT i.\"Name\" ,emailid from users  u\r\nLeft join wj_institutes i on u.userid=i.instituteid\r\nwhere userid= @InstituteID", conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            instituteName = reader["Name"]?.ToString() ?? "";
                            instituteEmail = reader["emailid"]?.ToString() ?? "";
                        }
                    }
                }

                if (string.IsNullOrEmpty(instituteName))
                {
                    return (false, "", "Institute not found");
                }

                // 4. Insert group allocation
                using (var cmd = new NpgsqlCommand("INSERT INTO hf_rel_manage_group_list(groupname, studentid, isallocated, isallocatedby, maincourseid) VALUES ('G1', @StudentID, B'1', @InstituteID, @CourseID)", conn))
                {
                    cmd.Parameters.AddWithValue("@StudentID", studentsID);
                    cmd.Parameters.AddWithValue("@InstituteID", instituteID);
                    cmd.Parameters.AddWithValue("@CourseID", courseid);
                    await cmd.ExecuteNonQueryAsync();
                }

                body.Replace("<%action%>", "Approval");
                body.Replace("<%instituteName%>", instituteName);

                EmailManager.SendEmailAws(instituteEmail, "Confirmation For Approval Of Group Project Submission", body.ToString(), null, "", "");

                string studentTemplatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "template", "ApprovalConfirmationForStudents.html");
                StringBuilder body1 = new StringBuilder(File.Exists(studentTemplatePath) ? File.ReadAllText(studentTemplatePath) : "");

                body1.Replace("<%action%>", "Approved");
                body1.Replace("<%studentsname%>", firstName);
                if (!string.IsNullOrEmpty(studentEmail) && studentEmail != "null")
                {
                    EmailManager.SendEmailAws(studentEmail, "Confirmation For Approval Of Group Project Submission by the school", body1.ToString(), null, "", "");
                }
            }

            return (true, body.ToString(), "");
        }
    }

    public class GroupProjectController : Controller
    {
        private readonly IGroupProjectService _groupProjectService;

        public GroupProjectController(IGroupProjectService groupProjectService)
        {
            _groupProjectService = groupProjectService;
        }

        [HttpGet]
        public async Task<IActionResult> RaiseRequest(int courseid)
        {
            int userid = HttpContext.Session.GetInt32("UserID") ?? 1;

            try
            {
                string message = await _groupProjectService.RaiseRequestAsync(courseid, userid);
                return Json(new { success = true, message });
            }
            catch (System.Exception)
            {
                return Json(new { success = false, message = "An error occurred while processing your request." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> RejectRequest(int courseid, int StudntsID, int InstituteID)
        {
            string htmlResult = await _groupProjectService.RejectRequestAsync(courseid, StudntsID, InstituteID);
            return Content(htmlResult, "text/html");
        }

        [HttpGet]
        public async Task<IActionResult> ApproveRequest(int courseid, int StudntsID, int InstituteID)
        {
            var result = await _groupProjectService.ApproveRequestAsync(courseid, StudntsID, InstituteID);

            if (!result.Success)
            {
                return Json(new { success = false, message = result.Message });
            }

            return Content(result.HtmlContent, "text/html");
        }
    }

}