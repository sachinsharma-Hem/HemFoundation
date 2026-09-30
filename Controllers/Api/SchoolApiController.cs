using HemFoundation.Common;
using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using HemFoundation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Configuration;
using System.Data;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Route("api/school")]
    [ApiController]
    public class SchoolApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;
        public SchoolApiController(ApplicationDbContext context, IWebHostEnvironment env, IConfiguration configuration)
        {
            _context = context;
            _env = env;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");    
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetSchoolStats()
        {

            // 1. Retrieve it as a nullable integer directly from the session
            int? userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null || userId == 0)
            {
                // Fall back to claims if session is missing
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                userId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }

            if (userId == null || userId == 0)
            {
                return Unauthorized(new { success = false, message = "User not logged in." });
            }
            if (userId==0)
                return Unauthorized(new { success = false, message = "User not logged in." });
            var institute = await (from i in _context.SchoolProfileDetails
                                   join u in _context.Users on i.instituteid equals u.UserID
                                   where u.UserID == userId
                                   select i).FirstOrDefaultAsync();

            if (institute == null)
                return NotFound(new { success = false, message = "Institute profile not found for this user." });

           

            return Ok(new
            {
                success = true,
                instituteName = institute.institute_name,
                isVerified = institute.isverify,
                studentsCount = institute.associatedstudents
            });
        }
        [HttpPost("Dashboard/UploadPartnershipLetter")]
        public async Task<IActionResult> UploadKnowledgePartnership([FromForm(Name = "letterFile")] IFormFile UploadDoc)
        {
            // ============================================
            // 1. Get logged-in User ID (Session or Claims)
            // ============================================
            // 1. Retrieve it as a nullable integer directly from the session
            int? userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null || userId == 0)
            {
                // Fall back to claims if session is missing
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                userId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }

            if (userId == null || userId == 0)
            {
                return Unauthorized(new { success = false, message = "User not logged in." });
            }

            // ============================================
            // 2. Find Institute from WJ_Institutes using AspNetUserID or UserID
            // ============================================
            var institute = await _context.WJ_Institutes
                .FirstOrDefaultAsync(i =>  i.InstituteID == userId);

            if (institute == null)
            {
                return NotFound(new { success = false, message = "Institute profile not found." });
            }

            int instituteId = institute.InstituteID;

            // ============================================
            // 3. Check if Partnership Letter is already uploaded
            // ============================================
            // Checking via Entity Framework instead of raw SQL injection-prone queries
            bool alreadyUploaded = await _context.KnowledgePartnersList
                .AnyAsync(k => k.InstituteID == instituteId && k.FileUrl != null);

            if (alreadyUploaded)
            {
                return BadRequest(new { success = false, message = "You have already uploaded Partnership Letter." });
            }

            // ============================================
            // 4. Validate file presence and extension
            // ============================================
            if (UploadDoc == null || UploadDoc.Length == 0)
            {
                return BadRequest(new { success = false, message = "Please select a valid file." });
            }

            var extension = Path.GetExtension(UploadDoc.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".txt", ".pdf" };

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Only .jpg, .jpeg, .png, .txt and .pdf extension files are allowed to upload."
                });
            }

            // Optional 10MB size limit check
            if (UploadDoc.Length > 10 * 1024 * 1024)
            {
                return BadRequest(new { success = false, message = "File size must not exceed 10 MB." });
            }

            // ============================================
            // 5. Prepare Directory & Unique File Path
            // ============================================
            string rawInstituteName = string.IsNullOrWhiteSpace(institute.Name) ? "School" : institute.Name.Trim();
            string sanitizedInstituteName = string.Concat(rawInstituteName.Where(char.IsLetterOrDigit));

            // Match legacy folder pattern: {InstituteID}_{InstituteName}
            string folderName = $"{instituteId}_{sanitizedInstituteName}";
            var uploadsFolder = Path.Combine(_env.WebRootPath, "Uploads", "KnowledgePartnerships", folderName);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string originalFileNameWithoutExt = Path.GetFileNameWithoutExtension(UploadDoc.FileName);
            string safeFileName = string.Concat(originalFileNameWithoutExt.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));
            string uniqueFileName = $"{safeFileName}_{Guid.NewGuid():N}{extension}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // ============================================
            // 6. Save File to Disk
            // ============================================
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await UploadDoc.CopyToAsync(stream);
            }

            // Relative file path structure for database storage
            string saveFileName = $"/Uploads/KnowledgePartnerships/{folderName}/{uniqueFileName}";

            // ============================================
            // 7. Insert Record into Database 
            // ============================================
            // Recommended: Use your EF Core Context DbData model instead of raw SqlCommand for PostgreSQL compatibility
            var partnerRecord = new KnowledgePartnersList
            {
                InstituteID = instituteId,
                InstituteName = rawInstituteName,
                FileUrl = saveFileName
                //CreatedOn = DateTime.UtcNow
            };

            _context.KnowledgePartnersList.Add(partnerRecord);
            await _context.SaveChangesAsync();

            // ============================================
            // 8. Return Success Response
            // ============================================
            return Ok(new
            {
                success = true,
                message = extension == ".jpg" || extension == ".jpeg" || extension == ".png"
                    ? "Image Uploaded Successfully!!!!...."
                    : "File Uploaded Successfully!!!!....",
                fileUrl = saveFileName
            });
        }

        [HttpGet("ChangeStandard")]
        public async Task<IActionResult> ChangeStandard(int studentId, string standard, string division)
        {
            var jsonResult = new { Success = false, Message = "", Data = (object)null };

            try
            {
                if (studentId > 0 && !string.IsNullOrEmpty(standard))
                {
                    using (var conn = new NpgsqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();

                        string query = "UPDATE wj_students SET standard = @Standard, division = @Division WHERE studentid = @StudentId";

                        using (var cmd = new NpgsqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Standard", standard);
                            cmd.Parameters.AddWithValue("@Division", (object)division ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@StudentId", studentId);

                            int rowsAffected = await cmd.ExecuteNonQueryAsync();

                            if (rowsAffected > 0)
                            {
                                //return Json(new { Success = true, Message = "Standard Changed Successfully", Data = (object)null });
                                return Ok(new { Success = true, Message = "Standard Changed Successfully" });
                            }
                            else
                            {
                                throw new Exception("Student not found or update failed.");
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception("Something Went Wrong. Please Contact Admin.");
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Success = false, Message = ex.Message, Data = (object)null });
            }
        }

        [HttpGet("UnmapStudent")]
        public async Task<IActionResult> UnmapStudent(int studentId)
        {
            var jsonResult = new { Success = false, Message = "", Data = (object)null };

            try
            {
                if (studentId <= 0)
                {
                    throw new Exception("Invalid Student ID.");
                }

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // 1. Execute the stored procedure to unmap the student
                    using (var cmd = new NpgsqlCommand("CALL public.hf_sp_unmapstudents(@StudentId)", conn))
                    {
                        cmd.Parameters.AddWithValue("@StudentId", studentId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Fetch student and school details for notifications
                    string query = @"
                SELECT 
                    TRIM(COALESCE(st.firstname, '') || ' ' || COALESCE(st.lastname, '')) AS studentname,
                    st.mobileno AS studentmobile,
                    st.emailid AS studentemail,
                    ins.""Name"" AS schoolname,
                    iu.emailid AS schoolemail,
                    iu.mobileno AS schoolmobileno 
                FROM users st             
                LEFT JOIN wj_studentassociates ass ON st.userid = ass.studentid             
                LEFT JOIN wj_institutes ins ON ass.instituteid = ins.instituteid             
                LEFT JOIN users iu ON ins.instituteid = iu.userid             
                WHERE st.userid = @StudentId";

                    DataTable dt = new DataTable();
                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@StudentId", studentId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            dt.Load(reader);
                        }
                    }

                    if (dt.Rows.Count == 0)
                    {
                        throw new Exception("No student data found.");
                    }

                    var row = dt.Rows[0];
                    string studentName = row["studentname"]?.ToString() ?? "";
                    string studentEmail = row["studentemail"]?.ToString() ?? "";
                    string schoolName = row["schoolname"]?.ToString() ?? "";
                    string schoolEmail = row["schoolemail"]?.ToString() ?? "";

                    // 3. Prepare Notification Content
                    string subject = $"Notice: Unmapping from {schoolName}";
                    string message = $"Dear {studentName},\n\n" +
                                     $"You have been unmapped from your school '{schoolName}'. " +
                                     $"Please contact your school admin for clarification.\n\n" +
                                     "Thank you,\nHem Virtues Team";

                    string schoolMessage = $"Dear {schoolName},\n\n" +
                                            $"Student '{studentName}' has been unmapped from your school.\n\n" +
                                            "Regards,\nHem Virtues System";

                    // 4. Send Emails asynchronously or via your manager classes
                    if (!string.IsNullOrWhiteSpace(studentEmail))
                    {
                        EmailManager.SendEmailAws(studentEmail, subject, message, null, "", "");
                    }

                    if (!string.IsNullOrWhiteSpace(schoolEmail))
                    {
                        EmailManager.SendEmailAws(schoolEmail, subject, schoolMessage, null, "", "");
                    }

                    return Ok(new { Success = true, Message = "Student unmapped successfully.", Data = "Student Unmapped" });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { Success = false, Message = ex.Message, Data = (object)null });
            }
        }
      
      [HttpPost("SendPasswordUser")]
        public async Task<IActionResult> SendPasswordUser([FromForm] int id)
        {
            string result = "0";
            try
            {
                if (id >= 0)
                {
                    using (var conn = new NpgsqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();

                        string query = @"
                    SELECT username, password_, mobileno, firstname 
                    FROM view_wj_students_detailsinfo 
                    WHERE studentid = @StudentID 
                    LIMIT 1";

                        string username = "", password = "", mobileNumber = "", name = "";

                        using (var cmd = new NpgsqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@StudentID", id);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    username = reader["username"]?.ToString() ?? "";
                                    password = reader["password_"]?.ToString() ?? "";
                                    mobileNumber = reader["mobileno"]?.ToString() ?? "";
                                    name = reader["firstname"]?.ToString() ?? "";
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(mobileNumber) && !string.IsNullOrEmpty(username))
                        {
                            // Trigger your SMS manager service
                            var smsManager = new SMSManager(_configuration);
                            smsManager.StudentRegistration(mobileNumber, name, username, password);
                           
                            result = "1";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Add your custom exception logger here if available
                // AddExceptionLogger.Exhandlexception(ex);
                result = "0";
            }

            // Returns "1" or "0" to match your legacy jQuery $.post callback condition (data == '1')
            return new JsonResult(result);
        }
        [HttpPost("SendPasswordTeacher")]
        public async Task<IActionResult> SendPasswordTeacher([FromForm] int id)
        {
            string result = "0";
            try
            {
                if (id >= 0)
                {
                    using (var conn = new NpgsqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();

                        string query = @"
                    SELECT username, password_, mobileno, firstname 
                    FROM view_hf_teacher_details 
                    WHERE teacherid = @StudentID 
                    LIMIT 1";

                        string username = "", password = "", mobileNumber = "", name = "";

                        using (var cmd = new NpgsqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@StudentID", id);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    username = reader["username"]?.ToString() ?? "";
                                    password = reader["password_"]?.ToString() ?? "";
                                    mobileNumber = reader["mobileno"]?.ToString() ?? "";
                                    name = reader["firstname"]?.ToString() ?? "";
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(mobileNumber) && !string.IsNullOrEmpty(username))
                        {
                            // Trigger your SMS manager service
                            var smsManager = new SMSManager(_configuration);
                            smsManager.StudentRegistration(mobileNumber, name, username, password);

                            result = "1";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Add your custom exception logger here if available
                // AddExceptionLogger.Exhandlexception(ex);
                result = "0";
            }

            // Returns "1" or "0" to match your legacy jQuery $.post callback condition (data == '1')
            return new JsonResult(result);
        }

        [HttpGet]
        [Route("assign-standards")]
        public IActionResult AssignStandardsnonaut(int teacherId, string standard)
        {
            try
            {
                using (var context = _context) // Or your DbContext instance variable name
                {
                    // Look for an existing teacher row
                    var teacherRecord = context.WJ_Teachers.FirstOrDefault(e => e.TeacherID == teacherId);

                    if (teacherRecord != null)
                    {
                        // Record exists -> Update properties
                        teacherRecord.Standard = standard;
                    }
                    else
                    {
                        // Record does not exist yet -> Insert new row
                        teacherRecord = new WJ_Teacher
                        {
                            TeacherID = teacherId,
                            Standard = standard
                        };
                        context.WJ_Teachers.Add(teacherRecord);
                    }

                    context.SaveChanges();

                    return Ok(new
                    {
                        success = true,
                        data = "Assign standards to teacher successfully",
                        message = "Assign standards to teacher successfully"
                    });
                }
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    data = "",
                    message = ex.InnerException?.Message ?? ex.Message
                });
            }
        }


        [HttpGet]
        [Route("deleteteacher")]
        public IActionResult DeleteTeacher(int teacherID)
        {
            try
            {
                // Example execution using Entity Framework Core Database facade for PostgreSQL parameters:
                _context.Database.ExecuteSqlRaw("CALL public.pro_wj_teacherdelete({0}, NULL)", teacherID, DBNull.Value);

                return Ok(new
                {
                    success = true,
                    data = "Teacher deleted successfully",
                    message = "Teacher deleted successfully"
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    data = "",
                    message = ex.InnerException?.Message ?? ex.Message
                });
            }
        }
    }
}