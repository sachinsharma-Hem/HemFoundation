using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;
using HemFoundation.Services;

namespace HemFoundation.Services
{
    public interface IGroupProjectService
    {
        Task<string> RaiseRequestAsync(int courseid, int userid);
        Task<string> RejectRequestAsync(int courseid, int studentsID, int instituteID);
        Task<string> ApproveRequestAsync(int courseid, int studentsID, int instituteID);
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
        public async Task<string> RejectRequestAsync(int courseid, int studentsID, int instituteID) { /* implementation */ return ""; }
        public async Task<string> ApproveRequestAsync(int courseid, int studentsID, int instituteID) { /* implementation */ return ""; }
    }
}