using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;
using HemFoundation.Services;

namespace HemFoundation.Controllers
{
    public class GroupProjectController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string _connectionString;

        public GroupProjectController(IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            _webHostEnvironment = webHostEnvironment;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // GET: GroupProject/RaiseRequest
        [HttpGet]
        public async Task<IActionResult> RaiseRequest(int courseid)
        {
            int userid = HttpContext.Session.GetInt32("UserID") ?? 1; // Fallback or handle appropriately

            string coursename = "";
            string institutename = "";
            string firstName = "";
            string lastName = "";
            int instituteID = 0;
            string standard = "";
            string emailID = "";

            try
            {
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
                    using (var cmd = new NpgsqlCommand("SELECT \"Name\", emailid FROM wj_institutes WHERE instituteid = @InstituteID", conn))
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

                    // 5. Fetch Course & MainCourse ID details via Syllabus View or Courses table
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

                    string templatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "template", "RequestForGroupProject.html");
                    if (System.IO.File.Exists(templatePath))
                    {
                        StringBuilder body = new StringBuilder(System.IO.File.ReadAllText(templatePath));
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
            }
            catch (Exception ex)
            {
                // Add your modern exception logging service or handler here
            }

            var data1 = new { success = true, message = "Your request has been successfully processed." };
            return Json(data1);
        }

        // GET: GroupProject/RejectRequest
        [HttpGet]
        public async Task<IActionResult> RejectRequest(int courseid, int StudntsID, int InstituteID)
        {
            string templatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "template", "Approval.html");
            StringBuilder body = new StringBuilder(System.IO.File.Exists(templatePath) ? System.IO.File.ReadAllText(templatePath) : "");

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
                    cmd.Parameters.AddWithValue("@UserID", StudntsID);
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
                using (var cmd = new NpgsqlCommand("SELECT \"Name\", emailid FROM wj_institutes WHERE instituteid = @InstituteID", conn))
                {
                    cmd.Parameters.AddWithValue("@InstituteID", InstituteID);
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
                return Json(new { success = false, message = "Institute not found" });
            }

            body.Replace("<%action%>", "Reject");
            body.Replace("<%instituteName%>", instituteName);

            EmailManager.SendEmailAws(instituteEmail, "Confirmation For Rejection Of Group Project Submission", body.ToString(), null, "", "");

            string studentTemplatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "template", "ApprovalConfirmationForStudents.html");
            StringBuilder body1 = new StringBuilder(System.IO.File.Exists(studentTemplatePath) ? System.IO.File.ReadAllText(studentTemplatePath) : "");

            body1.Replace("<%action%>", "Rejected");
            body1.Replace("<%studentsname%>", firstName);
            if (!string.IsNullOrEmpty(studentEmail) && studentEmail != "null")
            {
                EmailManager.SendEmailAws(studentEmail, "Confirmation For Rejection Of Group Project Submission by the school", body1.ToString(), null, "", "");
            }

            return Content(body.ToString(), "text/html");
        }

        // GET: GroupProject/ApproveRequest
        [HttpGet]
        public async Task<IActionResult> ApproveRequest(int courseid, int StudntsID, int InstituteID)
        {
            string templatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "template", "Approval.html");
            StringBuilder body = new StringBuilder(System.IO.File.Exists(templatePath) ? System.IO.File.ReadAllText(templatePath) : "");

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
                    cmd.Parameters.AddWithValue("@UserID", StudntsID);
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
                    cmd.Parameters.AddWithValue("@InstituteID", InstituteID);
                    cmd.Parameters.AddWithValue("@CourseID", courseid);
                    cmd.Parameters.AddWithValue("@StudentID", StudntsID);
                    var res = await cmd.ExecuteScalarAsync();
                    isAlreadyExists = (res != null);
                }

                if (!isAlreadyExists)
                {
                    // 3. Fetch Institute details
                    using (var cmd = new NpgsqlCommand("SELECT \"Name\", emailid FROM wj_institutes WHERE instituteid = @InstituteID", conn))
                    {
                        cmd.Parameters.AddWithValue("@InstituteID", InstituteID);
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
                        return Json(new { success = false, message = "Institute not found" });
                    }

                    // 4. Insert group allocation
                    using (var cmd = new NpgsqlCommand("INSERT INTO hf_rel_manage_group_list(groupname, studentid, isallocated, isallocatedby, maincourseid) VALUES ('G1', @StudentID, 1, @InstituteID, @CourseID)", conn))
                    {
                        cmd.Parameters.AddWithValue("@StudentID", StudntsID);
                        cmd.Parameters.AddWithValue("@InstituteID", InstituteID);
                        cmd.Parameters.AddWithValue("@CourseID", courseid);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    body.Replace("<%action%>", "Approval");
                    body.Replace("<%instituteName%>", instituteName);

                    EmailManager.SendEmailAws(instituteEmail, "Confirmation For Approval Of Group Project Submission", body.ToString(), null, "", "");

                    string studentTemplatePath = Path.Combine(_webHostEnvironment.ContentRootPath, "template", "ApprovalConfirmationForStudents.html");
                    StringBuilder body1 = new StringBuilder(System.IO.File.Exists(studentTemplatePath) ? System.IO.File.ReadAllText(studentTemplatePath) : "");

                    body1.Replace("<%action%>", "Approved");
                    body1.Replace("<%studentsname%>", firstName);
                    if (!string.IsNullOrEmpty(studentEmail) && studentEmail != "null")
                    {
                        EmailManager.SendEmailAws(studentEmail, "Confirmation For Approval Of Group Project Submission by the school", body1.ToString(), null, "", "");
                    }
                }
                else
                {
                    return Json(new { success = true, message = "Your request for the Approval of students for the group project submission has been already submitted!!." });
                }
            }

            return Content(body.ToString(), "text/html");
        }
    }
}