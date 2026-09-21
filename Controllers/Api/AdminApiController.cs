using HemFoundation.Common;
using HemFoundation.Data;
using HemFoundation.DTOs;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Route("api/admin")]
    [ApiController]
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly SMSManager _smsManager;

        public AdminApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            SMSManager smsManager)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _smsManager = smsManager;
        }

        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized(new { success = false, message = "Unauthorized access." });

            var totalUsers = await _userManager.Users.CountAsync();
            var totalMenus = await _context.Menus.CountAsync();

            return Ok(new
            {
                success = true,
                stats = new
                {
                    totalUsers,
                    totalMenus,
                    serverTime = DateTime.Now
                }
            });
        }

        [HttpGet("bulk-logs")]
        public async Task<IActionResult> GetBulkLogs()
        {
            var logs = await (from l in _context.BulkUploadLogs
                              join i in _context.WJ_Institutes on l.InstituteID equals i.InstituteID
                              orderby l.UploadedDate descending
                              select new
                              {
                                  l.LogID,
                                  SchoolName = i.Name,
                                  l.UploadType,
                                  l.FileName,
                                  l.FilePath,
                                  l.Status,
                                  l.UploadedDate
                              }).ToListAsync();

            return Ok(new { success = true, data = logs });
        }

        [HttpPost("update-bulk-status")]
        public async Task<IActionResult> UpdateBulkStatus([FromForm] int logId, [FromForm] string status)
        {
            var log = await _context.BulkUploadLogs.FindAsync(logId);
            if (log == null)
            {
                return NotFound(new { success = false, message = "Upload record not found." });
            }

            log.Status = status;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Bulk upload status updated successfully!" });
        }

        [HttpPost("upload-and-process")]
        public async Task<IActionResult> UploadAndProcess([FromForm] int logId, [FromForm] IFormFile excelFile)
        {
            var log = await _context.BulkUploadLogs
                .FirstOrDefaultAsync(l => l.LogID == logId);

            if (log == null)
                return NotFound(new { success = false, message = "Log record not found." });

            if (excelFile == null || excelFile.Length == 0)
                return BadRequest(new { success = false, message = "Please select a valid Excel file." });

            try
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "adminuploadfileserver");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = $"{log.LogID}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{excelFile.FileName}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await excelFile.CopyToAsync(fileStream);
                }

                log.FilePath = $"/adminuploadfileserver/{uniqueFileName}";
                log.FileName = excelFile.FileName;
                log.Status = "In Progress";
                await _context.SaveChangesAsync();

                int successCount = 0;
                int instituteId = log.InstituteID;
                string uploadType = log.UploadType?.Trim().ToLower() ?? "student";

                using (var workbook = new ClosedXML.Excel.XLWorkbook(filePath))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RowsUsed().Skip(1);

                    string portalNameToMatch = uploadType switch
                    {
                        "student" => "WJ_Students",
                        "teacher" => "WJ_Teacher",
                        "volunteer" => "Volunteer",
                        _ => "WJ_Students"
                    };

                    var portal = await _context.PortalMasters
                        .FirstOrDefaultAsync(p => p.PortalName.ToLower() == portalNameToMatch.ToLower() && p.IsActive);

                    string roleName = "WJ_Students"; 

                    if (uploadType.Trim().ToLower() == "volunteer")
                    {
                        roleName = "HF_Volunteers";
                    }
                    else
                    {
                        roleName = portal?.PortalName ?? "WJ_Students";
                    }


                    foreach (var row in rows)
                    {
                        try
                        {
                            string firstName = row.Cell(1).GetString().Trim();
                            string lastName = row.Cell(2).GetString().Trim();
                            string mobileNo = row.Cell(3).GetString().Trim();
                            string emailID = row.Cell(4).GetString().Trim();

                            if (string.IsNullOrEmpty(mobileNo) || string.IsNullOrEmpty(firstName))
                                continue;

                            string uniqueUsername = mobileNo;
                            int counter = 1;
                            while (await HemFoundation.Common.Utility.CheckUsernameExistsAsync(_context, uniqueUsername))
                            {
                                uniqueUsername = $"{mobileNo}_{counter}";
                                counter++;
                            }

                            string aspNetUserId = await HemFoundation.Common.Utility.CreateIdentityUserRawAsync(_context, uniqueUsername, emailID, mobileNo, roleName);

                            var coreUser = new HemFoundation.Models.Entities.Users
                            {
                                FirstName = firstName,
                                LastName = lastName,
                                MobileNo = mobileNo,
                                EmailID = emailID,
                                IsActive = true,
                                CreatedBy = instituteId,
                                CreatedDate = DateTime.UtcNow,
                                AspNetUserID = aspNetUserId,
                                Password_ = mobileNo,
                                RegisterFrom = "AdminBulkUpload",
                                PhotoUrl = "-"
                            };
                            _context.Users.Add(coreUser);
                            await _context.SaveChangesAsync();

                            int generatedUserId = coreUser.UserID;

                            if (roleName == "WJ_Students")
                            {
                                var student = new WJ_Students
                                {
                                    StudentID = generatedUserId,
                                    ProfileUrl = uniqueUsername,
                                    IsActive = true,
                                    IsPaid = false,
                                    SharingPermission = false
                                };
                                _context.WJ_Students.Add(student);

                                _context.WJ_StudentAssociates.Add(new WJ_StudentAssociates
                                {
                                    StudentID = generatedUserId,
                                    InstituteID = instituteId,
                                    IsPrimaryAssociate = true
                                });
                            }
                            else if (roleName == "WJ_Teacher")
                            {
                                var teacher = new WJ_Teacher
                                {
                                    TeacherID = generatedUserId,
                                    ProfileUrl = uniqueUsername
                                };
                                _context.WJ_Teachers.Add(teacher);

                                _context.WJ_TeacherAssociates.Add(new WJ_TeacherAssociates
                                {
                                    TeacherID = generatedUserId,
                                    InstituteID = instituteId,
                                    IsPrimaryAssociate = true
                                });
                            }
                            else if (roleName == "HF_Volunteers")
                            {
                                var volunteer = new Volunteer
                                {
                                    Id = generatedUserId,
                                    FirstName = firstName,
                                    LastName = lastName,
                                    MobileNo = mobileNo,
                                    Email = emailID,
                                    InstituteId = instituteId,
                                    AspNetUserId = aspNetUserId,
                                    Password_ = mobileNo,
                                    IsActive = true,
                                    IsDeleted = false
                                };
                                _context.Volunteers.Add(volunteer);
                            }

                            await _context.SaveChangesAsync();

                            // --- Notifications (SMS & Email Handling) ---
                            try
                            {
                                if (!string.IsNullOrEmpty(mobileNo))
                                {
                                    _smsManager.StudentRegistration(mobileNo, firstName, uniqueUsername, mobileNo);
                                }

                                if (!string.IsNullOrEmpty(emailID))
                                {
                                    var studentRegObj = new StudentRegistration
                                    {
                                        FirstName = firstName,
                                        LastName = lastName,
                                        EmailID = emailID,
                                        UserName = uniqueUsername,
                                        Password = mobileNo,
                                        MobileNo = mobileNo
                                    };
                                    Utility.StudentRegistration(studentRegObj);
                                }
                            }
                            catch (Exception notifEx)
                            {
                                System.Diagnostics.Debug.WriteLine("Notification dispatch failed: " + notifEx.ToString());
                            }

                            successCount++;
                        }
                        catch (Exception rowEx)
                        {
                            System.Diagnostics.Debug.WriteLine("Row import error: " + rowEx.Message);
                        }
                    }
                }

                log.Status = "Completed";
                log.TotalRecords = successCount;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = $"File uploaded and {successCount} users registered successfully!" });
            }
            catch (Exception ex)
            {
                log.Status = "Failed";
                await _context.SaveChangesAsync();
                return BadRequest(new { success = false, message = "Error processing file: " + ex.Message });
            }
        }
    }
}