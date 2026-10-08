using ClosedXML.Excel;
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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml.Interfaces.SensitivityLabels;
using System.Configuration;
using System.Data;
using System.Reflection;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using HemFoundation.Services;

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
        private readonly IFileStorageService _fileStorage;
        public AdminApiController(
      ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      IWebHostEnvironment env,
      SMSManager smsManager,
      IConfiguration configuration,
      IFileStorageService fileStorage)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _smsManager = smsManager;
            _configuration = configuration;
            _fileStorage = fileStorage;
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
        public async Task<IActionResult> PreviewExcel(
            [FromForm] int logId,
            [FromForm] IFormFile excelFile)
        {
            var log = await _context.BulkUploadLogs
                .FirstOrDefaultAsync(l => l.LogID == logId);

            if (log == null)
                return NotFound(new
                {
                    success = false,
                    message = "Log record not found."
                });

            if (excelFile == null || excelFile.Length == 0)
                return BadRequest(new
                {
                    success = false,
                    message = "Please select a valid Excel file."
                });

            try
            {
                // ============================================================
                // 1. UPLOAD EXCEL TO CENTRAL FILE SERVER
                // ============================================================

                string folderPath = "Uploads/AdminBulkUpload";

                var uploadResult = await _fileStorage.UploadAsync(
                    excelFile,
                    folderPath);

                if (uploadResult == null ||
                    !uploadResult.success ||
                    string.IsNullOrWhiteSpace(uploadResult.filePath))
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = uploadResult?.message
                            ?? "Excel file upload to central server failed."
                    });
                }

                // ============================================================
                // 2. SAVE CENTRAL FILE PATH IN DATABASE
                // ============================================================

                log.FilePath = uploadResult.filePath;
                log.FileName = excelFile.FileName;

                await _context.SaveChangesAsync();

                // ============================================================
                // 3. READ EXCEL DIRECTLY FROM UPLOADED FILE STREAM
                // ============================================================

                var previewRows =
                    new List<Dictionary<string, string>>();

                var detectedColumns =
                    new List<string>();

                var columnMappingInfo =
                    new Dictionary<string, string>();

                // IFormFile stream ko directly ClosedXML ko diya ja raha hai.
                // Koi local wwwroot file create nahi hogi.
                using (var excelStream = excelFile.OpenReadStream())
                using (var workbook = new ClosedXML.Excel.XLWorkbook(excelStream))
                {
                    var worksheet = workbook.Worksheet(1);

                    var headerRow = worksheet.Row(1);

                    int colCount =
                        headerRow.LastCellUsed()?.Address.ColumnNumber ?? 4;

                    // ========================================================
                    // DETECT COLUMNS
                    // ========================================================

                    for (int c = 1; c <= colCount; c++)
                    {
                        string colName =
                            headerRow.Cell(c).GetString().Trim();

                        detectedColumns.Add(colName);

                        string lower =
                            colName.ToLower();

                        string mappedTo =
                            "Ignored / Extra Column";

                        if (lower.Contains("first") ||
                            lower.Contains("fname"))
                        {
                            mappedTo = "users.firstname";
                        }
                        else if (lower.Contains("last") ||
                                 lower.Contains("lname") ||
                                 lower.Contains("surname"))
                        {
                            mappedTo = "users.lastname";
                        }
                        else if (lower.Contains("mob") ||
                                 lower.Contains("phone") ||
                                 lower.Contains("contact"))
                        {
                            mappedTo = "users.mobileno";
                        }
                        else if (lower.Contains("email") ||
                                 lower.Contains("mail"))
                        {
                            mappedTo = "users.emailid";
                        }
                        else if (lower.Contains("standard") ||
                                 lower.Contains("class") ||
                                 lower.Contains("std"))
                        {
                            mappedTo = "wj_students.standard";
                        }
                        else if (lower.Contains("div") ||
                                 lower.Contains("section"))
                        {
                            mappedTo = "wj_students.division";
                        }
                        else if (lower.Contains("gardian") ||
                                 lower.Contains("guardian") ||
                                 lower.Contains("parent"))
                        {
                            mappedTo = "wj_students.gardianname";
                        }
                        else if (lower.Contains("guardianmail") ||
                                 lower.Contains("parentemail"))
                        {
                            mappedTo = "wj_students.guardianemailid";
                        }

                        columnMappingInfo[colName] = mappedTo;
                    }

                    // ========================================================
                    // READ PREVIEW ROWS
                    // ========================================================

                    int lastRow =
                        worksheet.LastRowUsed()?.RowNumber() ?? 1;

                    for (int r = 2; r <= lastRow; r++)
                    {
                        var row = worksheet.Row(r);

                        var rowData =
                            new Dictionary<string, string>();

                        bool hasData = false;

                        for (int c = 1; c <= colCount; c++)
                        {
                            string colName =
                                detectedColumns[c - 1];

                            string val =
                                row.Cell(c).GetString().Trim();

                            rowData[colName] = val;

                            if (!string.IsNullOrEmpty(val))
                            {
                                hasData = true;
                            }
                        }

                        if (hasData)
                        {
                            previewRows.Add(rowData);
                        }
                    }
                }

                // ============================================================
                // 4. RETURN SAME RESPONSE STRUCTURE
                // ============================================================

                return Ok(new
                {
                    success = true,
                    filePath = log.FilePath,
                    columns = detectedColumns,
                    columnMapping = columnMappingInfo,
                    previewData = previewRows
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Error reading Excel: " + ex.Message
                });
            }
        }

        [HttpPost("process-mapped-excel")]
        public async Task<IActionResult> ProcessMappedExcel(
     [FromForm] int logId,
     [FromForm] string filePath)
        {
            var log = await _context.BulkUploadLogs
                .FirstOrDefaultAsync(l => l.LogID == logId);

            if (log == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Log record not found."
                });
            }


            FileDownloadResult? downloadedFile = null;

            try
            {
                downloadedFile =
                    await _fileStorage.DownloadAsync(filePath);
            }
            catch
            {
                downloadedFile = null;
            }

            Stream? excelStream = null;

            try
            {
                if (downloadedFile != null &&
                    downloadedFile.FileBytes != null &&
                    downloadedFile.FileBytes.Length > 0)
                {
                    excelStream =
                        new MemoryStream(downloadedFile.FileBytes);
                }
                else
                {
                    // --------------------------------------------------------
                    // OLD LOCAL FILE FALLBACK
                    // --------------------------------------------------------

                    string fullPath =
                        Path.Combine(
                            _env.WebRootPath,
                            filePath.TrimStart('/'));

                    if (!System.IO.File.Exists(fullPath))
                    {
                        return NotFound(new
                        {
                            success = false,
                            message = "Excel file not found on central server or local server."
                        });
                    }

                    var localBytes =
                        await System.IO.File.ReadAllBytesAsync(fullPath);

                    if (localBytes == null ||
                        localBytes.Length == 0)
                    {
                        return NotFound(new
                        {
                            success = false,
                            message = "Excel file is empty."
                        });
                    }

                    excelStream =
                        new MemoryStream(localBytes);
                }

                // ============================================================
                // 3. MARK PROCESSING STARTED
                // ============================================================

                log.Status = "In Progress";

                await _context.SaveChangesAsync();

                int successCount = 0;
                int failCount = 0;

                int instituteId = log.InstituteID;

                string uploadType =
                    log.UploadType?.Trim().ToLower()
                    ?? "student";

                var notificationList =
                    new List<(
                        string Mobile,
                        string FirstName,
                        string Username,
                        string Email,
                        string LastName
                    )>();

                // ============================================================
                // 4. READ EXCEL FROM CENTRAL FILE BYTES
                // ============================================================

                excelStream.Position = 0;

                using (var workbook =
                    new ClosedXML.Excel.XLWorkbook(excelStream))
                {
                    var worksheet =
                        workbook.Worksheet(1);

                    var headers =
                        worksheet
                            .Row(1)
                            .CellsUsed()
                            .ToDictionary(
                                c => c.GetString()
                                    .Trim()
                                    .ToLower(),
                                c => c.Address.ColumnNumber);

                    // ========================================================
                    // COLUMN FINDER
                    // ========================================================

                    int GetCol(params string[] keys)
                    {
                        foreach (var k in keys)
                        {
                            var match =
                                headers.Keys.FirstOrDefault(
                                    h => h.Contains(k));

                            if (match != null)
                            {
                                return headers[match];
                            }
                        }

                        return -1;
                    }

                    int fNameCol =
                        GetCol("first", "name", "fname");

                    int lNameCol =
                        GetCol("last", "surname", "lname");

                    int mobCol =
                        GetCol(
                            "mob",
                            "phone",
                            "contact",
                            "cell",
                            "number");

                    int emailCol =
                        GetCol("email", "mail");

                    int stdCol =
                        GetCol(
                            "standard",
                            "class",
                            "std");

                    int divCol =
                        GetCol(
                            "div",
                            "section");

                    int guardianNameCol =
                        GetCol(
                            "gardian",
                            "guardian",
                            "parent",
                            "father");

                    int guardianMobCol =
                        GetCol(
                            "guardianmob",
                            "parentmob",
                            "guardianphone");

                    int guardianEmailCol =
                        GetCol(
                            "guardianemail",
                            "parentemail");

                    int lastRow =
                        worksheet.LastRowUsed()?.RowNumber()
                        ?? 1;

                    // ========================================================
                    // PORTAL / ROLE
                    // ========================================================

                    string portalNameToMatch =
                        uploadType switch
                        {
                            "student" => "WJ_Students",
                            "teacher" => "WJ_Teacher",
                            "volunteer" => "Volunteer",
                            _ => "WJ_Students"
                        };

                    var portal =
                        await _context.PortalMasters
                            .FirstOrDefaultAsync(
                                p =>
                                    p.PortalName.ToLower()
                                    ==
                                    portalNameToMatch.ToLower()
                                    &&
                                    p.IsActive);

                    string roleName =
                        uploadType == "volunteer"
                            ? "HF_Volunteers"
                            : (portal?.PortalName
                               ?? "WJ_Students");

                    // ========================================================
                    // PROCESS EACH ROW
                    // ========================================================

                    for (int r = 2; r <= lastRow; r++)
                    {
                        var row = worksheet.Row(r);

                        try
                        {
                            string firstName =
                                fNameCol > 0
                                    ? row.Cell(fNameCol)
                                        .GetString()
                                        .Trim()
                                    : "User";

                            string lastName =
                                lNameCol > 0
                                    ? row.Cell(lNameCol)
                                        .GetString()
                                        .Trim()
                                    : "";

                            string mobileNo =
                                mobCol > 0
                                    ? row.Cell(mobCol)
                                        .GetString()
                                        .Trim()
                                    : "";

                            string emailID =
                                emailCol > 0
                                    ? row.Cell(emailCol)
                                        .GetString()
                                        .Trim()
                                    : "";

                            string standard =
                                stdCol > 0
                                    ? row.Cell(stdCol)
                                        .GetString()
                                        .Trim()
                                    : "";

                            string division =
                                divCol > 0
                                    ? row.Cell(divCol)
                                        .GetString()
                                        .Trim()
                                    : "";

                            string guardianName =
                                guardianNameCol > 0
                                    ? row.Cell(guardianNameCol)
                                        .GetString()
                                        .Trim()
                                    : "";

                            string guardianMob =
                                guardianMobCol > 0
                                    ? row.Cell(guardianMobCol)
                                        .GetString()
                                        .Trim()
                                    : "";

                            string guardianEmail =
                                guardianEmailCol > 0
                                    ? row.Cell(guardianEmailCol)
                                        .GetString()
                                        .Trim()
                                    : "";

                            // =================================================
                            // VALIDATION
                            // =================================================

                            if (string.IsNullOrEmpty(firstName) &&
                                string.IsNullOrEmpty(mobileNo))
                            {
                                failCount++;
                                continue;
                            }

                            if (string.IsNullOrEmpty(mobileNo))
                            {
                                mobileNo = "9999999999";
                            }

                            if (!string.IsNullOrEmpty(standard))
                            {
                                string digits =
                                    new string(
                                        standard
                                            .Where(char.IsDigit)
                                            .ToArray());

                                if (int.TryParse(
                                    digits,
                                    out int number))
                                {
                                    string suffix = "th";

                                    int lastTwo =
                                        number % 100;

                                    if (lastTwo < 11 ||
                                        lastTwo > 13)
                                    {
                                        switch (number % 10)
                                        {
                                            case 1:
                                                suffix = "st";
                                                break;

                                            case 2:
                                                suffix = "nd";
                                                break;

                                            case 3:
                                                suffix = "rd";
                                                break;
                                        }
                                    }

                                    standard =
                                        number + suffix;
                                }
                            }

                            string uniqueUsername =
                                mobileNo;

                            int counter = 1;

                            while (
                                await HemFoundation.Common.Utility
                                    .CheckUsernameExistsAsync(
                                        _context,
                                        uniqueUsername))
                            {
                                uniqueUsername =
                                    $"{mobileNo}_{counter}";

                                counter++;
                            }

                            string aspNetUserId =
                                await HemFoundation.Common.Utility
                                    .CreateIdentityUserRawAsync(
                                        _context,
                                        uniqueUsername,
                                        emailID,
                                        mobileNo,
                                        roleName);

                            var coreUser =
                                new HemFoundation.Models.Entities.Users
                                {
                                    FirstName =
                                        string.IsNullOrEmpty(firstName)
                                            ? "User"
                                            : firstName,

                                    LastName = lastName,

                                    MobileNo = mobileNo,

                                    EmailID = emailID,

                                    IsActive = true,

                                    CreatedBy = instituteId,

                                    CreatedDate =
                                        DateTime.UtcNow,

                                    AspNetUserID =
                                        aspNetUserId,

                                    Password_ =
                                        mobileNo,

                                    RegisterFrom =
                                        "AdminBulkUpload",

                                    PhotoUrl = "-"
                                };

                            _context.Users.Add(coreUser);

                            await _context.SaveChangesAsync();

                            int generatedUserId =
                                coreUser.UserID;

                            if (roleName == "WJ_Students")
                            {
                                var student =
                                    new WJ_Students
                                    {
                                        StudentID =
                                            generatedUserId,

                                        Standard =
                                            standard,

                                        Division =
                                            division,

                                        GardianName =
                                            guardianName,

                                        GuardianMobileNo =
                                            guardianMob,

                                        GuardianEmailID =
                                            guardianEmail,

                                        ProfileUrl =
                                            uniqueUsername,

                                        IsActive = true,

                                        IsPaid = false,

                                        SharingPermission = false
                                    };

                                _context.WJ_Students
                                    .Add(student);

                                _context.WJ_StudentAssociates
                                    .Add(
                                        new WJ_StudentAssociates
                                        {
                                            StudentID =
                                                generatedUserId,

                                            InstituteID =
                                                instituteId,

                                            IsPrimaryAssociate =
                                                true
                                        });
                            }

                            else if (roleName == "WJ_Teacher")
                            {
                                var teacher =
                                    new WJ_Teacher
                                    {
                                        TeacherID =
                                            generatedUserId,

                                        ProfileUrl =
                                            uniqueUsername
                                    };

                                _context.WJ_Teachers
                                    .Add(teacher);

                                _context.WJ_TeacherAssociates
                                    .Add(
                                        new WJ_TeacherAssociates
                                        {
                                            TeacherID =
                                                generatedUserId,

                                            InstituteID =
                                                instituteId,

                                            IsPrimaryAssociate =
                                                true
                                        });
                            }

                            else if (roleName ==
                                     "HF_Volunteers")
                            {
                                var volunteer =
                                    new Volunteer
                                    {
                                        Id =
                                            generatedUserId,

                                        FirstName =
                                            firstName,

                                        LastName =
                                            lastName,

                                        MobileNo =
                                            mobileNo,

                                        Email =
                                            emailID,

                                        InstituteId =
                                            instituteId,

                                        AspNetUserId =
                                            aspNetUserId,

                                        Password_ =
                                            mobileNo,

                                        IsActive = true,

                                        IsDeleted = false
                                    };

                                _context.Volunteers
                                    .Add(volunteer);
                            }

                            await _context.SaveChangesAsync();

                            notificationList.Add(
                                (
                                    mobileNo,
                                    firstName,
                                    uniqueUsername,
                                    emailID,
                                    lastName
                                ));

                            successCount++;
                        }
                        catch
                        {
                            failCount++;
                        }
                    }

                    log.Status = "Completed";

                    log.TotalRecords =
                        successCount;

                    await _context.SaveChangesAsync();

                    _ = Task.Run(async () =>
                    {
                        foreach (var item in notificationList)
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(item.Mobile))
                                {
                                    _smsManager.StudentRegistration(
                                        item.Mobile,
                                        item.FirstName,
                                        item.Username,
                                        item.Mobile);
                                }

                                if (!string.IsNullOrEmpty(item.Email))
                                {
                                    var studentRegObj =
                                        new StudentRegistration
                                        {
                                            FirstName =
                                                item.FirstName,

                                            LastName =
                                                item.LastName,

                                            EmailID =
                                                item.Email,

                                            UserName =
                                                item.Username,

                                            Password =
                                                item.Mobile,

                                            MobileNo =
                                                item.Mobile
                                        };

                                    Utility.StudentRegistration(
                                        studentRegObj);
                                }
                            }
                            catch (Exception bgEx)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    "Background notification failed: "
                                    + bgEx.Message);
                            }
                        }
                    });

                    return Ok(new
                    {
                        success = true,
                        message =
                            $"Registration complete! Successfully Inserted: {successCount}, Failed/Skipped: {failCount} (Notifications processing in background)"
                    });
                }
            }
            catch (Exception ex)
            {
                log.Status = "Failed";

                await _context.SaveChangesAsync();

                return BadRequest(new
                {
                    success = false,
                    message =
                        "Error processing data: "
                        + ex.Message
                });
            }
            finally
            {
                if (excelStream != null)
                {
                    await excelStream.DisposeAsync();
                }
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
     
        [HttpGet("overall-details-list1/{category}/{type}")]
        public async Task<IActionResult> GetOverallDetailsList1(string category, string type)
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


                   

                    "total-teachers" =>
                        await _context.Set<ViewHfTeacherDetails>()
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                instituteIds.Contains(x.InstituteID))
                            .Take(1000)
                            .ToListAsync(),


                   

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


                   

                    "sel-projects" =>
                        await _context.creativityActivitiesSubmission2021s
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                instituteIds.Contains(x.InstituteID))
                            .Take(1000)
                            .ToListAsync(),


                  

                    "family-projects" =>
                        await _context.StudentActivityLogs
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                instituteIds.Contains(x.InstituteID) &&
                                x.ActivityType == "Family PBL")
                            .Take(1000)
                            .ToListAsync(),


                   

                    "individual-projects" =>
                        await _context.StudentActivityLogs
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                instituteIds.Contains(x.InstituteID) &&
                                x.ActivityType == "Individual PBL")
                            .Take(1000)
                            .ToListAsync(),


                    

                    "group-pbl" =>
                        await _context.creativityActivitiesSubmission2021s
                            .AsNoTracking()
                            .Where(x =>
                                x.InstituteID != null &&
                                instituteIds.Contains(x.InstituteID))
                            .Take(1000)
                            .ToListAsync(),


                   

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

        [HttpGet("overall-details-list/{category}/{type}")]
        public async Task<IActionResult> GetOverallDetailsList(string category, string type, [FromQuery] int limit = 100)
        {
            try
            {
                category = category?.Trim().ToLower() ?? "";
                type = type?.Trim().ToLower() ?? "";

                // Ensure limit bounds are safe (e.g., max 2000 rows)
                limit = limit > 0 && limit <= 2000 ? limit : 100;

                List<int?> instituteIds;

                if (category == "performance")
                {
                    var referralCodes = await _context.WJ_ReferralCodes
                        .AsNoTracking()
                        .Where(x => x.ReferralCode != null && x.ReferralCode != "")
                        .Select(x => x.ReferralCode)
                        .Distinct()
                        .ToListAsync();

                    instituteIds = await _context.view_wj_instituteslist
                        .AsNoTracking()
                        .Where(x => x.referralcode != null && referralCodes.Contains(x.referralcode))
                        .Select(x => (int?)x.instituteid)
                        .Distinct()
                        .ToListAsync();
                }
                else if (category == "focused")
                {
                    instituteIds = await _context.HfTblAffliateFocusedLists
                        .AsNoTracking()
                        .Where(x => x.InstituteId != null)
                        .Select(x => (int?)x.InstituteId)
                        .Distinct()
                        .ToListAsync();
                }
                else
                {
                    return BadRequest(new { success = false, message = "Invalid report category." });
                }

                if (!instituteIds.Any())
                {
                    return Ok(new { success = true, data = new List<object>() });
                }

                object result = type switch
                {
                    "total-students" => await _context.Set<hf_view_studentsdetails>()
                        .AsNoTracking()
                        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                        .Take(limit)
                        .ToListAsync(),

                    "active-students" => await (
                        from log in _context.StudentActivityLogs.AsNoTracking()
                        join student in _context.Set<hf_view_studentsdetails>()
                            on log.StudentID equals student.StudentId
                        where log.InstituteID != null &&
                              instituteIds.Contains(log.InstituteID) &&
                              log.ActivityType != "Survey"
                        select student
                    ).Distinct().Take(limit).ToListAsync(),

                    "total-teachers" => await _context.Set<ViewHfTeacherDetails>()
                        .AsNoTracking()
                        .Where(x => instituteIds.Contains(x.InstituteID))
                        .Take(limit)
                        .ToListAsync(),

                    "active-teachers" => await (
                        from log in _context.TeacherActivityLogs.AsNoTracking()
                        join teacher in _context.Set<ViewHfTeacherDetails>()
                            on log.TeacherID equals teacher.TeacherID
                        where 
                              instituteIds.Contains(log.InstituteID)
                        select teacher
                    ).Distinct().Take(limit).ToListAsync(),

                    "sel-projects" => await _context.creativityActivitiesSubmission2021s
                        .AsNoTracking()
                        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                        .Take(limit)
                        .ToListAsync(),

                    "family-projects" => await _context.StudentActivityLogs
                        .AsNoTracking()
                        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Family PBL")
                        .Take(limit)
                        .ToListAsync(),

                    "individual-projects" => await _context.StudentActivityLogs
                        .AsNoTracking()
                        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Individual PBL")
                        .Take(limit)
                        .ToListAsync(),

                    "group-pbl" => await _context.creativityActivitiesSubmission2021s
                        .AsNoTracking()
                        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                        .Take(limit)
                        .ToListAsync(),

                    "total-schools" => await _context.SchoolProfileDetails
                        .AsNoTracking()
                        .Where(x => x.instituteid != null && instituteIds.Contains(x.instituteid))
                        .Select(x => new
                        {
                            InstituteID = x.instituteid,
                            ReferralCode = x.referralcode,
                            institute_name = x.institute_name,
                            firstname = x.firstname,
                            lastname = x.lastname,
                            mobilno = x.mobileno,
                            addressline1 = x.addressline1,
                            addressline3 = x.addressline3,
                            cityname = x.cityname,
                            pincode = x.pincode,
                            username = x.username,
                            password = x.password_,
                            associatestudents = x.associatedstudents,
                            activestudents = x.activestudents,
                            associteteachers = x.totalteachers,
                            activeteachers = x.activeteachers,
                            isverfy = x.isverify
                        })
                        .Distinct()
                        .Take(limit)
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
                    error = ex.Message
                });
            }
        }

        [HttpGet("overall-details-paged")]
        public async Task<IActionResult> GetOverallDetailsPaged(string category, string type, int page = 1, int pageSize = 100, string search = "",int affiliateid=0)
        {
            try
            {
                category = category?.Trim().ToLower() ?? "";
                type = type?.Trim().ToLower() ?? "";
                page = page < 1 ? 1 : page;
                pageSize = pageSize <= 0 ? 100 : pageSize;
                search = search?.Trim() ?? "";

                List<int?> instituteIds = new List<int?>();

                string role = HttpContext.Session.GetString("UserRole") ?? "";
               

                if (affiliateid != 0)
                {
                    instituteIds =
                        await GetInstituteIdsForCategoryAsyncwithaffiliateid(
                            category,
                            affiliateid,
                            role
                        );
                }
                else if (
                    role == "WJ_Admin" ||
                    role == "Admin" ||
                    role == "Management"
                )
                {
                    instituteIds =
                        await GetInstituteIdsForCategoryAsync(category);
                }
                else if (
                    role == "WJ_Affiliate" ||
                    role == "HF_Volunteers"
                )
                {
                    Int32 userId =
                        HttpContext.Session.GetInt32("UserID") ?? 0;

                    instituteIds =
                        await GetInstituteIdsForCategoryAsyncwithaffiliateid(
                            category,
                            userId,
                            role
                        );
                }
                else
                {
                    return Ok(new
                    {
                        success = true,
                        data = new List<object>(),
                        totalRecords = 0,
                        totalPages = 0,
                        page = page,
                        message = "Unauthorised User"
                    });
                }

                if (!instituteIds.Any())
                {
                    return Ok(new { success = true, data = new List<object>(), totalRecords = 0, totalPages = 0, page });
                }

                var query = BuildDetailsQuery(category, type, instituteIds, search);

                int totalRecords = await query.CountAsync();
                int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                var pagedData = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    page,
                    pageSize,
                    totalRecords,
                    totalPages,
                    data = pagedData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("export-details-excel")]
        public async Task<IActionResult> ExportDetailsExcel(string category, string type, string search = "", int affiliateid = 0)
        {
            try
            {
                category = category?.Trim().ToLower() ?? "";
                type = type?.Trim().ToLower() ?? "";
                search = search?.Trim() ?? "";

                List<int?> instituteIds = new List<int?>();

                string role = HttpContext.Session.GetString("UserRole") ?? "";


                if (affiliateid != 0)
                {
                    instituteIds =
                        await GetInstituteIdsForCategoryAsyncwithaffiliateid(
                            category,
                            affiliateid,
                            role
                        );
                }
                else if (
                    role == "WJ_Admin" ||
                    role == "Admin" ||
                    role == "Management"
                )
                {
                    instituteIds =
                        await GetInstituteIdsForCategoryAsync(category);
                }
                else if (
                    role == "WJ_Affiliate" ||
                    role == "HF_Volunteers"
                )
                {
                    Int32 userId =
                        HttpContext.Session.GetInt32("UserID") ?? 0;

                    instituteIds =
                        await GetInstituteIdsForCategoryAsyncwithaffiliateid(
                            category,
                            userId,
                            role
                        );
                }
                else
                {
                    return Ok(new
                    {
                        success = true,
                        data = new List<object>(),
                        totalRecords = 0,
                        totalPages = 0,
                        
                        message = "Unauthorised User"
                    });
                }
                if (!instituteIds.Any())
                {
                    return BadRequest("No records found to export.");
                }

                var query = BuildDetailsQuery(category, type, instituteIds, search);
                var exportList = await query.ToListAsync();

                using (XLWorkbook wb = new XLWorkbook())
                {
                    DataTable dt = ConvertListToDataTable(exportList);
                    wb.Worksheets.Add(dt, $"{type.ToUpper()} Report");

                    using (MemoryStream stream = new MemoryStream())
                    {
                        wb.SaveAs(stream);
                        stream.Seek(0, SeekOrigin.Begin);
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{category}_{type}_Report.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Export error: {ex.Message}");
            }
        }

       
        private IQueryable<object> BuildDetailsQuery(string category, string type, List<int?> instituteIds, string search)
        {
            string pattern = $"%{search}%";
            bool hasSearch = !string.IsNullOrEmpty(search);

            return type switch
            {
                "total-students" => _context.Set<hf_view_studentsdetails>()
                    .AsNoTracking()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) &&
                           (!hasSearch ||
                            EF.Functions.ILike(x.FirstName ?? "", pattern) ||
                            EF.Functions.ILike(x.LastName ?? "", pattern) ||
                            EF.Functions.ILike(x.MobileNo ?? "", pattern) ||
                            EF.Functions.ILike(x.Standard ?? "", pattern) ||
                            EF.Functions.ILike(x.CityName ?? "", pattern) ||
                            EF.Functions.ILike(x.EmailId ?? "", pattern) ||
                            EF.Functions.ILike(x.Username ?? "", pattern))).Select(x => (object)new
                            {
                                StudentId = x.StudentId,
                                FirstName = x.FirstName,
                                LastName = x.LastName,
                                InstituteID = x.InstituteID,
                                Institute_Name = x.InstituteName,
                                Standard = x.Standard,
                                Division = x.Division,
                                MobileNo = x.MobileNo,
                                EmailId = x.EmailId,
                                GardianName = x.GardianName,
                                Gender = x.Gender,
                                Dob = x.Dob,
                                AddressLine1 = x.AddressLine1,
                                RegistrationDate=x.RegistrationDate,
                                CityName = x.CityName,
                                Pincode = x.Pincode,
                                Username = x.Username,
                                Password = x.Password
                            }),

                "active-students" => (from log in _context.StudentActivityLogs.AsNoTracking()
                                      join student in _context.Set<ViewWjStudentsDetailsInfo>() on log.StudentID equals student.StudentId
                                      where log.InstituteID != null && instituteIds.Contains(log.InstituteID) && log.ActivityType != "Survey" &&
                                            (!hasSearch ||
                                             EF.Functions.ILike(student.FirstName ?? "", pattern) ||
                                             EF.Functions.ILike(student.LastName ?? "", pattern) ||
                                             EF.Functions.ILike(student.MobileNo ?? "", pattern) ||
                                             EF.Functions.ILike(student.Standard ?? "", pattern) ||
                                             EF.Functions.ILike(student.CityName ?? "", pattern) ||
                                             EF.Functions.ILike(student.EmailId ?? "", pattern) ||
                                             EF.Functions.ILike(log.ActivityType ?? "", pattern))
                                      select student).Distinct(),

                "total-teachers" => _context.Set<ViewHfTeacherDetails>()
                    .AsNoTracking()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) &&
                           (!hasSearch ||
                            EF.Functions.ILike(x.FirstName ?? "", pattern) ||
                            EF.Functions.ILike(x.LastName ?? "", pattern) ||
                            EF.Functions.ILike(x.InstituteName ?? "", pattern) ||
                          //  EF.Functions.ILike(x.EmailId ?? "", pattern) ||
                            EF.Functions.ILike(x.MobileNo ?? "", pattern) ||
                            EF.Functions.ILike(x.Standard ?? "", pattern))),

                "active-teachers" => (from log in _context.TeacherActivityLogs.AsNoTracking()
                                      join teacher in _context.Set<ViewHfTeacherDetails>() on log.TeacherID equals teacher.TeacherID
                                      where log.InstituteID != null && instituteIds.Contains(log.InstituteID) &&
                                            (!hasSearch ||
                                             EF.Functions.ILike(teacher.FirstName ?? "", pattern) ||
                                             EF.Functions.ILike(teacher.LastName ?? "", pattern) ||
                                             EF.Functions.ILike(teacher.InstituteName ?? "", pattern) ||
                                          //   EF.Functions.ILike(teacher.EmailId ?? "", pattern) ||
                                             EF.Functions.ILike(teacher.MobileNo ?? "", pattern))
                                      select teacher).Distinct(),

                "sel-projects" => _context.creativityActivitiesSubmission2021s
                    .AsNoTracking()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) &&
                           (!hasSearch ||
                            EF.Functions.ILike(x.ShortDescription ?? "", pattern) 
                            //||
                            //EF.Functions.ILike(x.InstituteName ?? "", pattern) ||
                            //EF.Functions.ILike(x.CourseName ?? "", pattern)
                            )),

                "family-projects" => _context.StudentActivityLogs
                    .AsNoTracking()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Family PBL" &&
                           (!hasSearch ||
                            EF.Functions.ILike(x.StudentName ?? "", pattern) ||
                            EF.Functions.ILike(x.MainCourseName ?? "", pattern) ||
                            EF.Functions.ILike(x.InstituteName ?? "", pattern) ||
                            EF.Functions.ILike(x.CourseName ?? "", pattern))),

                "individual-projects" => _context.StudentActivityLogs
                    .AsNoTracking()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Individual PBL" &&
                           (!hasSearch ||
                            EF.Functions.ILike(x.StudentName ?? "", pattern) ||
                            EF.Functions.ILike(x.MainCourseName ?? "", pattern) ||
                            EF.Functions.ILike(x.InstituteName ?? "", pattern) ||
                            EF.Functions.ILike(x.CourseName ?? "", pattern))),

                "group-pbl" => _context.creativityActivitiesSubmission2021s
                    .AsNoTracking()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) &&
                           (!hasSearch ||
                            EF.Functions.ILike(x.ShortDescription ?? "", pattern)
                            //||
                            //EF.Functions.ILike(x.InstituteName ?? "", pattern) ||
                            //EF.Functions.ILike(x.CourseName ?? "", pattern)
                            )),

                "total-schools" => _context.SchoolProfileDetails
                    .AsNoTracking()
                    .Where(x => x.instituteid != null && instituteIds.Contains(x.instituteid) &&
                           (!hasSearch ||
                            EF.Functions.ILike(x.institute_name ?? "", pattern) ||
                            EF.Functions.ILike(x.firstname ?? "", pattern) ||
                            EF.Functions.ILike(x.lastname ?? "", pattern) ||
                            EF.Functions.ILike(x.cityname ?? "", pattern) ||
                            EF.Functions.ILike(x.referralcode ?? "", pattern) ||
                            EF.Functions.ILike(x.mobileno ?? "", pattern) ||
                            EF.Functions.ILike(x.emailid ?? "", pattern) ||
                            EF.Functions.ILike(x.username ?? "", pattern)))
                    .Select(x => (object)new
                    {
                        InstituteID = x.instituteid,
                        ReferralCode = x.referralcode,
                        Institute_Name = x.institute_name,
                        FirstName = x.firstname,
                        LastName = x.lastname,
                        MobileNo = x.mobileno,
                        CityName = x.cityname,
                        Pincode = x.pincode,
                        username=x.username,
                        password=x.password_,
                        AssociatedStudents = x.associatedstudents,
                        ActiveStudents = x.activestudents,
                        TotalTeachers = x.totalteachers,
                        ActiveTeachers = x.activeteachers
                    }),

                _ => Enumerable.Empty<object>().AsQueryable()
            };
        }
        
        private async Task<List<int?>> GetInstituteIdsForCategoryAsync(string category)
        {
            if (category == "performance")
            {
                var referralCodes = await _context.WJ_ReferralCodes
                    .AsNoTracking()
                    .Where(x => x.ReferralCode != null && x.ReferralCode != "")
                    .Select(x => x.ReferralCode)
                    .Distinct()
                    .ToListAsync();

                return await _context.SchoolProfileDetails
                    .AsNoTracking()
                   // .Where(x => x.referralcode != null && referralCodes.Contains(x.referralcode))
                    .Select(x => (int?)x.instituteid)
                    .Distinct()
                    .ToListAsync();
            }
            else if (category == "focused")
            {
                return await _context.HfTblAffliateFocusedLists
                    .AsNoTracking()
                    .Where(x => x.InstituteId != null)
                    .Select(x => (int?)x.InstituteId)
                    .Distinct()
                    .ToListAsync();
            }
            return new List<int?>();
        }

        private DataTable ConvertListToDataTable(List<object> items)
        {
            DataTable dataTable = new DataTable("ReportData");
            if (items == null || items.Count == 0 || items[0] == null)
            {
                return dataTable;
            }

            // Get the actual runtime type (handles anonymous types correctly)
            Type itemType = items[0].GetType();
            PropertyInfo[] props = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in props)
            {
                dataTable.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            }

            foreach (var item in items)
            {
                if (item == null) continue;
                var values = new object[props.Length];
                for (int i = 0; i < props.Length; i++)
                {
                    values[i] = props[i].GetValue(item, null) ?? DBNull.Value;
                }
                dataTable.Rows.Add(values);
            }

            return dataTable;
        }
        private async Task<List<int?>> GetInstituteIdsForCategoryAsyncwithaffiliateid(string category,Int32 userid,string role)
        {
            if (
                    role == "WJ_Affiliate" 
                )
            {


                if (category == "performance")
                {
                    var referralCodes = await _context.WJ_ReferralCodes
                        .AsNoTracking()
                        .Where(x => x.UserID==userid)
                        .Select(x => x.ReferralCode)
                        .Distinct()
                        .ToListAsync();

                    return await _context.WJ_Institutes
                        .AsNoTracking()
                        .Where(x => x.ReferralCode != null && referralCodes.Contains(x.ReferralCode))
                        .Select(x => (int?)x.InstituteID)
                        .Distinct()
                        .ToListAsync();
                }
                else if (category == "focused")
                {
                    return await _context.HfTblAffliateFocusedLists
                         .AsNoTracking()
                         .Where(f => f.InstituteId != null)
                         .Join(
                             _context.WJ_Institutes,
                             f => f.InstituteId,
                             i => i.InstituteID,
                             (f, i) => new { f, i }
                         )
                         .Join(
                             _context.WJ_ReferralCodes.Where(r => r.UserID == userid),
                             x => x.i.ReferralCode,
                             r => r.ReferralCode,
                             (x, r) => x.f.InstituteId
                        )
                     .Distinct()
                     .ToListAsync();
                                }
                     }
                    else
                    {
                        if (category == "performance")
                        {
                    

                            return await _context.VolunteerInstituteMappings
                                .AsNoTracking()
                                 .Where(x => x.VolunteerId == userid)
                                .Select(x => (int?)x.InstituteId)
                                .Distinct()
                                .ToListAsync();
                        }
                

                    }


               
            return new List<int?>();
        }

        [HttpGet("download-bulk-file")]
        public async Task<IActionResult> DownloadBulkFile([FromQuery] string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "File path is required."
                });
            }

            try
            {
                var fileResult = await _fileStorage.DownloadAsync(filePath);

                if (fileResult == null ||
                    fileResult.FileBytes == null ||
                    fileResult.FileBytes.Length == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "File not found."
                    });
                }

                return File(
                    fileResult.FileBytes,
                    fileResult.ContentType ??
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileResult.FileName
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to download file: " + ex.Message
                });
            }
        }
    }
}