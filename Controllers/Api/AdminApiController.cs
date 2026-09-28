using HemFoundation.Common;
using HemFoundation.Data;
using HemFoundation.DTOs;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Humanizer.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Configuration;
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
        private readonly IConfiguration _configuration;
        public AdminApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            SMSManager smsManager, IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _smsManager = smsManager;
            _configuration = configuration;
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

        [HttpPost("preview-excel")]
        public async Task<IActionResult> PreviewExcel([FromForm] int logId, [FromForm] IFormFile excelFile)
        {
            var log = await _context.BulkUploadLogs.FirstOrDefaultAsync(l => l.LogID == logId);
            if (log == null) return NotFound(new { success = false, message = "Log record not found." });

            if (excelFile == null || excelFile.Length == 0)
                return BadRequest(new { success = false, message = "Please select a valid Excel file." });

            try
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "adminuploadfileserver");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = $"{log.LogID}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{excelFile.FileName}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await excelFile.CopyToAsync(fileStream);
                }

                log.FilePath = $"/adminuploadfileserver/{uniqueFileName}";
                log.FileName = excelFile.FileName;
                await _context.SaveChangesAsync();

                var previewRows = new List<Dictionary<string, string>>();
                var detectedColumns = new List<string>();
                var columnMappingInfo = new Dictionary<string, string>();

                using (var workbook = new ClosedXML.Excel.XLWorkbook(filePath))
                {
                    var worksheet = workbook.Worksheet(1);
                    var headerRow = worksheet.Row(1);
                    int colCount = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 4;

                    for (int c = 1; c <= colCount; c++)
                    {
                        string colName = headerRow.Cell(c).GetString().Trim();
                        detectedColumns.Add(colName);

                        string lower = colName.ToLower();
                        string mappedTo = "Ignored / Extra Column";
                        if (lower.Contains("first") || lower.Contains("fname")) mappedTo = "users.firstname";
                        else if (lower.Contains("last") || lower.Contains("lname") || lower.Contains("surname")) mappedTo = "users.lastname";
                        else if (lower.Contains("mob") || lower.Contains("phone") || lower.Contains("contact")) mappedTo = "users.mobileno";
                        else if (lower.Contains("email") || lower.Contains("mail")) mappedTo = "users.emailid";
                        else if (lower.Contains("standard") || lower.Contains("class") || lower.Contains("std")) mappedTo = "wj_students.standard";
                        else if (lower.Contains("div") || lower.Contains("section")) mappedTo = "wj_students.division";
                        else if (lower.Contains("gardian") || lower.Contains("guardian") || lower.Contains("parent")) mappedTo = "wj_students.gardianname";
                        else if (lower.Contains("guardianmail") || lower.Contains("parentemail")) mappedTo = "wj_students.guardianemailid";

                        columnMappingInfo[colName] = mappedTo;
                    }

                    int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                    for (int r = 2; r <= lastRow; r++)
                    {
                        var row = worksheet.Row(r);
                        var rowData = new Dictionary<string, string>();
                        bool hasData = false;

                        for (int c = 1; c <= colCount; c++)
                        {
                            string colName = detectedColumns[c - 1];
                            string val = row.Cell(c).GetString().Trim();
                            rowData[colName] = val;
                            if (!string.IsNullOrEmpty(val)) hasData = true;
                        }

                        if (hasData) previewRows.Add(rowData);
                    }
                }

                return Ok(new { success = true, filePath = log.FilePath, columns = detectedColumns, columnMapping = columnMappingInfo, previewData = previewRows });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Error reading Excel: " + ex.Message });
            }
        }

        [HttpPost("process-mapped-excel")]
        public async Task<IActionResult> ProcessMappedExcel([FromForm] int logId, [FromForm] string filePath)
        {
            var log = await _context.BulkUploadLogs.FirstOrDefaultAsync(l => l.LogID == logId);
            if (log == null) return NotFound(new { success = false, message = "Log record not found." });

            string fullPath = Path.Combine(_env.WebRootPath, filePath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath)) return NotFound(new { success = false, message = "Server file not found." });

            log.Status = "In Progress";
            await _context.SaveChangesAsync();

            int successCount = 0;
            int failCount = 0;
            int instituteId = log.InstituteID;
            string uploadType = log.UploadType?.Trim().ToLower() ?? "student";

            var notificationList = new List<(string Mobile, string FirstName, string Username, string Email, string LastName)>();

            try
            {
                using (var workbook = new ClosedXML.Excel.XLWorkbook(fullPath))
                {
                    var worksheet = workbook.Worksheet(1);
                    var headers = worksheet.Row(1).CellsUsed().ToDictionary(c => c.GetString().Trim().ToLower(), c => c.Address.ColumnNumber);

                    int GetCol(params string[] keys)
                    {
                        foreach (var k in keys)
                        {
                            var match = headers.Keys.FirstOrDefault(h => h.Contains(k));
                            if (match != null) return headers[match];
                        }
                        return -1;
                    }

                    int fNameCol = GetCol("first", "name", "fname");
                    int lNameCol = GetCol("last", "surname", "lname");
                    int mobCol = GetCol("mob", "phone", "contact", "cell", "number");
                    int emailCol = GetCol("email", "mail");
                    int stdCol = GetCol("standard", "class", "std");
                    int divCol = GetCol("div", "section");
                    int guardianNameCol = GetCol("gardian", "guardian", "parent", "father");
                    int guardianMobCol = GetCol("guardianmob", "parentmob", "guardianphone");
                    int guardianEmailCol = GetCol("guardianemail", "parentemail");

                    int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

                    string portalNameToMatch = uploadType switch
                    {
                        "student" => "WJ_Students",
                        "teacher" => "WJ_Teacher",
                        "volunteer" => "Volunteer",
                        _ => "WJ_Students"
                    };

                    var portal = await _context.PortalMasters.FirstOrDefaultAsync(p => p.PortalName.ToLower() == portalNameToMatch.ToLower() && p.IsActive);
                    string roleName = uploadType == "volunteer" ? "HF_Volunteers" : (portal?.PortalName ?? "WJ_Students");

                    for (int r = 2; r <= lastRow; r++)
                    {
                        var row = worksheet.Row(r);
                        try
                        {
                            string firstName = fNameCol > 0 ? row.Cell(fNameCol).GetString().Trim() : "User";
                            string lastName = lNameCol > 0 ? row.Cell(lNameCol).GetString().Trim() : "";
                            string mobileNo = mobCol > 0 ? row.Cell(mobCol).GetString().Trim() : "";
                            string emailID = emailCol > 0 ? row.Cell(emailCol).GetString().Trim() : "";
                            string standard = stdCol > 0 ? row.Cell(stdCol).GetString().Trim() : "";
                            string division = divCol > 0 ? row.Cell(divCol).GetString().Trim() : "";
                            string guardianName = guardianNameCol > 0 ? row.Cell(guardianNameCol).GetString().Trim() : "";
                            string guardianMob = guardianMobCol > 0 ? row.Cell(guardianMobCol).GetString().Trim() : "";
                            string guardianEmail = guardianEmailCol > 0 ? row.Cell(guardianEmailCol).GetString().Trim() : "";

                            if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(mobileNo))
                            {
                                failCount++;
                                continue;
                            }

                            if (string.IsNullOrEmpty(mobileNo))
                            {
                                mobileNo = "9999999999";
                            }



                            //if (!string.IsNullOrEmpty(standard) && !standard.EndsWith("th", StringComparison.OrdinalIgnoreCase) && !standard.EndsWith("st", StringComparison.OrdinalIgnoreCase) && !standard.EndsWith("nd", StringComparison.OrdinalIgnoreCase) && !standard.EndsWith("rd", StringComparison.OrdinalIgnoreCase))
                            //{
                            //    standard += "th";
                            //}
                            if (!string.IsNullOrEmpty(standard))
                            {
                                // Extract any leading numeric digits from the standard string (e.g., "11th" -> 11, "1" -> 1)
                                string digits = new string(standard.Where(char.IsDigit).ToArray());



                                if (int.TryParse(digits, out int number))
                                {
                                    string suffix = "th";

                                    int lastTwo = number % 100;
                                    if (lastTwo < 11 || lastTwo > 13)
                                    {
                                        switch (number % 10)
                                        {
                                            case 1: suffix = "st"; break;
                                            case 2: suffix = "nd"; break;
                                            case 3: suffix = "rd"; break;
                                        }
                                    }

                                    standard = number + suffix;
                                }
                            }
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
                                FirstName = string.IsNullOrEmpty(firstName) ? "User" : firstName,
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
                                    Standard = standard,
                                    Division = division,
                                    GardianName = guardianName,
                                    GuardianMobileNo = guardianMob,
                                    GuardianEmailID = guardianEmail,
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

                            notificationList.Add((mobileNo, firstName, uniqueUsername, emailID, lastName));

                            successCount++;
                        }
                        catch
                        {
                            failCount++;
                        }
                    }

                    log.Status = "Completed";
                    log.TotalRecords = successCount;
                    await _context.SaveChangesAsync();

                    _ = Task.Run(async () =>
                    {
                        foreach (var item in notificationList)
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(item.Mobile))
                                {
                                    _smsManager.StudentRegistration(item.Mobile, item.FirstName, item.Username, item.Mobile);
                                }

                                if (!string.IsNullOrEmpty(item.Email))
                                {
                                    var studentRegObj = new StudentRegistration
                                    {
                                        FirstName = item.FirstName,
                                        LastName = item.LastName,
                                        EmailID = item.Email,
                                        UserName = item.Username,
                                        Password = item.Mobile,
                                        MobileNo = item.Mobile
                                    };
                                    Utility.StudentRegistration(studentRegObj);
                                }
                            }
                            catch (Exception bgEx)
                            {
                                System.Diagnostics.Debug.WriteLine("Background notification failed: " + bgEx.Message);
                            }
                        }
                    });

                    return Ok(new { success = true, message = $"Registration complete! Successfully Inserted: {successCount}, Failed/Skipped: {failCount} (Notifications processing in background)" });
                }
            }
            catch (Exception ex)
            {
                log.Status = "Failed";
                await _context.SaveChangesAsync();
                return BadRequest(new { success = false, message = "Error processing data: " + ex.Message });
            }
        }
        [HttpDelete("delete-bulk-log/{id}")]
        public async Task<IActionResult> DeleteBulkLog(int id)
        {
            var log = await _context.BulkUploadLogs.FindAsync(id);
            if (log == null) return NotFound(new { success = false, message = "Record not found." });

            _context.BulkUploadLogs.Remove(log);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Bulk upload record deleted successfully." });
        }      

        [HttpGet("overall-dashboard-counts1")]
        public async Task<IActionResult> GetOverallDashboardCounts1()
        {
            try
            {
                

                var performanceReferralCodes = await _context.WJ_ReferralCodes
                    .AsNoTracking()
                    .Where(x => x.ReferralCode != null && x.ReferralCode != "")
                    .Select(x => x.ReferralCode)
                    .Distinct()
                    .ToListAsync();


                var performanceInstituteIds = await _context.SchoolProfileDetails
                    .AsNoTracking()                    
                    .Select(x => (int?)x.instituteid)
                    .Distinct()
                    .ToListAsync();


                // ============================================================
                // FOCUSED REPORT
                //
                // HfTblAffliateFocusedLists
                //       -> InstituteID
                // ============================================================

                var focusedInstituteIds = await _context.HfTblAffliateFocusedLists
                    .AsNoTracking()
                    .Where(x => x.InstituteId != null)
                    .Select(x => (int?)x.InstituteId)
                    .Distinct()
                    .ToListAsync();


                // ============================================================
                // PERFORMANCE COUNTS
                // ============================================================

                int performanceSchoolCount = performanceInstituteIds.Count;

                int performanceTotalStudents = 0;
                int performanceActiveStudents = 0;
                int performanceTotalTeachers = 0;
                int performanceActiveTeachers = 0;
                int performanceTotalSelProjects = 0;
                int performanceFamilyProjects = 0;
                int performanceIndividualProjects = 0;
                int performanceGroupPbl = 0;


                if (performanceInstituteIds.Any())
                {
                    performanceTotalStudents =
                        await _context.Set<ViewWjStudentsDetailsInfo>()
                            .AsNoTracking()
                            .CountAsync(x =>
                                x.InstituteID != null &&
                                performanceInstituteIds.Contains(x.InstituteID));


                    performanceActiveStudents =
                        await _context.StudentActivityLogs
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                performanceInstituteIds.Contains(x.InstituteID) &&
                                x.ActivityType != "Survey")
                            .Select(x => x.StudentID)
                            .Distinct()
                            .CountAsync();


                    performanceTotalTeachers =
                        await _context.Set<ViewHfTeacherDetails>()
                            .AsNoTracking()
                            .CountAsync(x =>
                                x.InstituteID != null &&
                                performanceInstituteIds.Contains(x.InstituteID));


                    performanceActiveTeachers =
                        await _context.TeacherActivityLogs
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                performanceInstituteIds.Contains(x.InstituteID))
                            .Select(x => x.TeacherID)
                            .Distinct()
                            .CountAsync();


                    performanceTotalSelProjects =
                        await _context.creativityActivitiesSubmission2021s
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                performanceInstituteIds.Contains(x.InstituteID))
                            .Select(x => x.InstituteID)
                            .Distinct()
                            .CountAsync();


                    performanceFamilyProjects =
                        await _context.StudentActivityLogs
                            .AsNoTracking()
                            .CountAsync(x =>                               
                                x.ActivityType == "Family PBL");


                    performanceIndividualProjects =
                        await _context.StudentActivityLogs
                            .AsNoTracking()
                            .CountAsync(x =>
                               
                                x.ActivityType == "Individual PBL");


                    performanceGroupPbl =
                        await _context.creativityActivitiesSubmission2021s
                            .AsNoTracking()
                            .CountAsync(x =>
                                x.InstituteID != null &&
                                performanceInstituteIds.Contains(x.InstituteID));
                }


                // ============================================================
                // FOCUSED COUNTS
                // ============================================================

                int focusedSchoolCount = focusedInstituteIds.Count;

                int focusedTotalStudents = 0;
                int focusedActiveStudents = 0;
                int focusedTotalTeachers = 0;
                int focusedActiveTeachers = 0;
                int focusedTotalSelProjects = 0;
                int focusedFamilyProjects = 0;
                int focusedIndividualProjects = 0;
                int focusedGroupPbl = 0;


                if (focusedInstituteIds.Any())
                {
                    focusedTotalStudents =
                        await _context.Set<ViewWjStudentsDetailsInfo>()
                            .AsNoTracking()
                            .CountAsync(x =>
                                x.InstituteID != null &&
                                focusedInstituteIds.Contains(x.InstituteID));


                    focusedActiveStudents =
                        await _context.StudentActivityLogs
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                focusedInstituteIds.Contains(x.InstituteID) &&
                                x.ActivityType != "Survey")
                            .Select(x => x.StudentID)
                            .Distinct()
                            .CountAsync();


                    focusedTotalTeachers =
                        await _context.Set<ViewHfTeacherDetails>()
                            .AsNoTracking()
                            .CountAsync(x =>
                                x.InstituteID != null &&
                                focusedInstituteIds.Contains(x.InstituteID));


                    focusedActiveTeachers =
                        await _context.TeacherActivityLogs
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                focusedInstituteIds.Contains(x.InstituteID))
                            .Select(x => x.TeacherID)
                            .Distinct()
                            .CountAsync();


                    focusedTotalSelProjects =
                        await _context.creativityActivitiesSubmission2021s
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                focusedInstituteIds.Contains(x.InstituteID))
                            .Select(x => x.InstituteID)
                            .Distinct()
                            .CountAsync();


                    focusedFamilyProjects =
                        await _context.StudentActivityLogs
                            .AsNoTracking()
                            .CountAsync(x =>
                                x.InstituteID != null &&
                                focusedInstituteIds.Contains(x.InstituteID) &&
                                x.ActivityType == "Family PBL");


                    focusedIndividualProjects =
                        await _context.StudentActivityLogs
                            .AsNoTracking()
                            .CountAsync(x =>
                                x.InstituteID != null &&
                                focusedInstituteIds.Contains(x.InstituteID) &&
                                x.ActivityType == "Individual PBL");


                    focusedGroupPbl =
                        await _context.creativityActivitiesSubmission2021s
                            .AsNoTracking()
                            .CountAsync(x =>
                                x.InstituteID != null &&
                                focusedInstituteIds.Contains(x.InstituteID));
                }


                // ============================================================
                // RESPONSE
                // ============================================================

                return Ok(new
                {
                    success = true,

                    data = new
                    {
                        performance = new
                        {
                            schoolCount = performanceSchoolCount,
                            totalStudents = performanceTotalStudents,
                            activeStudents = performanceActiveStudents,
                            totalTeachers = performanceTotalTeachers,
                            activeTeachers = performanceActiveTeachers,
                            totalSelProjects = performanceTotalSelProjects,
                            familyProjects = performanceFamilyProjects,
                            individualProjects = performanceIndividualProjects,
                            groupPbl = performanceGroupPbl
                        },

                        focused = new
                        {
                            schoolCount = focusedSchoolCount,
                            totalStudents = focusedTotalStudents,
                            activeStudents = focusedActiveStudents,
                            totalTeachers = focusedTotalTeachers,
                            activeTeachers = focusedActiveTeachers,
                            totalSelProjects = focusedTotalSelProjects,
                            familyProjects = focusedFamilyProjects,
                            individualProjects = focusedIndividualProjects,
                            groupPbl = focusedGroupPbl
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading dashboard counts.",
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }



        [HttpGet("overall-dashboard-counts")]
        public async Task<IActionResult> GetOverallDashboardCounts()
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection");

                int performanceSchoolCount = 0;
                int performanceTotalStudents = 0;
                int performanceActiveStudents = 0;
                int performanceTotalTeachers = 0;
                int performanceActiveTeachers = 0;
                int performanceTotalSelProjects = 0;
                int performanceFamilyProjects = 0;
                int performanceIndividualProjects = 0;
                int performanceGroupPbl = 0;

                int focusedSchoolCount = 0;
                int focusedTotalStudents = 0;
                int focusedActiveStudents = 0;
                int focusedTotalTeachers = 0;
                int focusedActiveTeachers = 0;
                int focusedTotalSelProjects = 0;
                int focusedFamilyProjects = 0;
                int focusedIndividualProjects = 0;
                int focusedGroupPbl = 0;

                using (var conn = new Npgsql.NpgsqlConnection(connString))
                {
                    await conn.OpenAsync();

                    string dashboardSql = "SELECT metric_type, schoolcount, totalstudents, activestudents, totalteachers, activeteachers, totalselprojects, familyprojects, individualprojects, grouppbl FROM public.get_overall_dashboard_summary()";

                    using (var cmd = new Npgsql.NpgsqlCommand(dashboardSql, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string metricType = reader.GetString(0);
                                if (metricType == "Performance")
                                {
                                    performanceSchoolCount = reader.GetInt32(1);
                                    performanceTotalStudents = reader.GetInt32(2);
                                    performanceActiveStudents = reader.GetInt32(3);
                                    performanceTotalTeachers = reader.GetInt32(4);
                                    performanceActiveTeachers = reader.GetInt32(5);
                                    performanceTotalSelProjects = reader.GetInt32(6);
                                    performanceFamilyProjects = reader.GetInt32(7);
                                    performanceIndividualProjects = reader.GetInt32(8);
                                    performanceGroupPbl = reader.GetInt32(9);
                                }
                                else if (metricType == "Focused")
                                {
                                    focusedSchoolCount = reader.GetInt32(1);
                                    focusedTotalStudents = reader.GetInt32(2);
                                    focusedActiveStudents = reader.GetInt32(3);
                                    focusedTotalTeachers = reader.GetInt32(4);
                                    focusedActiveTeachers = reader.GetInt32(5);
                                    focusedTotalSelProjects = reader.GetInt32(6);
                                    focusedFamilyProjects = reader.GetInt32(7);
                                    focusedIndividualProjects = reader.GetInt32(8);
                                    focusedGroupPbl = reader.GetInt32(9);
                                }
                            }
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        performance = new
                        {
                            schoolCount = performanceSchoolCount,
                            totalStudents = performanceTotalStudents,
                            activeStudents = performanceActiveStudents,
                            totalTeachers = performanceTotalTeachers,
                            activeTeachers = performanceActiveTeachers,
                            totalSelProjects = performanceTotalSelProjects,
                            familyProjects = performanceFamilyProjects,
                            individualProjects = performanceIndividualProjects,
                            groupPbl = performanceGroupPbl
                        },
                        focused = new
                        {
                            schoolCount = focusedSchoolCount,
                            totalStudents = focusedTotalStudents,
                            activeStudents = focusedActiveStudents,
                            totalTeachers = focusedTotalTeachers,
                            activeTeachers = focusedActiveTeachers,
                            totalSelProjects = focusedTotalSelProjects,
                            familyProjects = focusedFamilyProjects,
                            individualProjects = focusedIndividualProjects,
                            groupPbl = focusedGroupPbl
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading dashboard counts.",
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }
     


        [HttpGet("overall-details-list/{category}/{type}")]
        public async Task<IActionResult> GetOverallDetailsList(string category, string type)
        {
            try
            {
                category = category?.Trim().ToLower() ?? "";
                type = type?.Trim().ToLower() ?? "";

                List<int?> instituteIds;


                if (category == "performance")
                {
                  

                    var referralCodes = await _context.WJ_ReferralCodes
                        .AsNoTracking()
                        .Where(x =>
                            x.ReferralCode != null &&
                            x.ReferralCode != "")
                        .Select(x => x.ReferralCode)
                        .Distinct()
                        .ToListAsync();


                    instituteIds = await _context.SchoolProfileDetails
                        .AsNoTracking()
                        .Where(x =>
                            x.referralcode != null &&
                            referralCodes.Contains(x.referralcode))
                        .Select(x => (int?)x.instituteid)
                        .Distinct()
                        .ToListAsync();
                }
                else if (category == "focused")
                {
                    // --------------------------------------------------------
                    // FOCUSED
                    // HfTblAffliateFocusedLists -> InstituteId
                    // --------------------------------------------------------

                    instituteIds = await _context.HfTblAffliateFocusedLists
                        .AsNoTracking()
                        .Where(x => x.InstituteId != null)
                        .Select(x => (int?)x.InstituteId)
                        .Distinct()
                        .ToListAsync();
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid report category."
                    });
                }


                if (!instituteIds.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        data = new List<object>()
                    });
                }


                // ============================================================
                // DETAILS
                // ============================================================

                object result = type switch
                {
                    // ========================================================
                    // TOTAL STUDENTS
                    // ========================================================

                    "total-students" =>
                        await _context.Set<ViewWjStudentsDetailsInfo>()
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                instituteIds.Contains(x.InstituteID))
                            .Take(1000)
                            .ToListAsync(),


                    // ========================================================
                    // ACTIVE STUDENTS
                    // ========================================================

                    "active-students" =>
                        await (
                            from log in _context.StudentActivityLogs.AsNoTracking()

                            join student in _context.Set<ViewWjStudentsDetailsInfo>()
                                on log.StudentID equals student.StudentId

                            where
                                log.InstituteID != null &&
                                instituteIds.Contains(log.InstituteID) &&
                                log.ActivityType != "Survey"

                            select student
                        )
                        .Distinct()
                        .Take(1000)
                        .ToListAsync(),


                    // ========================================================
                    // TOTAL TEACHERS
                    // ========================================================

                    "total-teachers" =>
                        await _context.Set<ViewHfTeacherDetails>()
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                instituteIds.Contains(x.InstituteID))
                            .Take(1000)
                            .ToListAsync(),


                    // ========================================================
                    // ACTIVE TEACHERS
                    // ========================================================

                    "active-teachers" =>
                        await (
                            from log in _context.TeacherActivityLogs.AsNoTracking()

                            join teacher in _context.Set<ViewHfTeacherDetails>()
                                on log.TeacherID equals teacher.TeacherID

                            where
                                log.InstituteID != null &&
                                instituteIds.Contains(log.InstituteID)

                            select teacher
                        )
                        .Distinct()
                        .Take(1000)
                        .ToListAsync(),


                    // ========================================================
                    // SEL PROJECTS
                    // ========================================================

                    "sel-projects" =>
                        await _context.creativityActivitiesSubmission2021s
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                instituteIds.Contains(x.InstituteID))
                            .Take(1000)
                            .ToListAsync(),


                    // ========================================================
                    // FAMILY PROJECTS
                    // ========================================================

                    "family-projects" =>
                        await _context.StudentActivityLogs
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                instituteIds.Contains(x.InstituteID) &&
                                x.ActivityType == "Family PBL")
                            .Take(1000)
                            .ToListAsync(),


                    // ========================================================
                    // INDIVIDUAL PROJECTS
                    // ========================================================

                    "individual-projects" =>
                        await _context.StudentActivityLogs
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                instituteIds.Contains(x.InstituteID) &&
                                x.ActivityType == "Individual PBL")
                            .Take(1000)
                            .ToListAsync(),


                    // ========================================================
                    // GROUP PBL
                    // ========================================================

                    "group-pbl" =>
                        await _context.creativityActivitiesSubmission2021s
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                instituteIds.Contains(x.InstituteID))
                            .Take(1000)
                            .ToListAsync(),


                    // ========================================================
                    // TOTAL SCHOOLS
                    //
                    // We only use fields already confirmed in your
                    // SchoolProfileDetails usage.
                    // ========================================================

                    "total-schools" =>
                        await _context.SchoolProfileDetails
                            .AsNoTracking()
                            .Where(x =>
                                x.instituteid != null &&
                                instituteIds.Contains(x.instituteid))
                            .Select(x => new
                            {
                                InstituteID = x.instituteid,
                                ReferralCode = x.referralcode,
                                institute_name=x.institute_name,
                                firstname=x.firstname,
                                lastname=x.lastname,
                                mobilno=x.mobileno,
                                addressline1 = x.addressline1,
                                addressline3=x.addressline3,
                                cityname=x.cityname,
                                pincode=x.pincode,
                                username=x.username,
                                password=x.password_,
                                associatestudents=x.associatedstudents,
                                activestudents=x.activestudents,
                                associteteachers=x.totalteachers,
                                activeteachers=x.activeteachers,
                                isverfy=x.isverify




                            })
                            .Distinct()
                            .Take(1000)
                            .ToListAsync(),


                    _ => new List<object>()
                };


                return Ok(new
                {
                    success = true,
                    category = category,
                    type = type,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading report details.",
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

      
    }
}