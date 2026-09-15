using ClosedXML.Excel;
using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.Entities; // Adjust based on your DTO location
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HemFoundation.Controllers // Changed namespace to Controllers convention
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("pbl-report-teacher")]
        public async Task<IActionResult> PBLReportForTeacher()
        {
            // 1. Get Teacher ID from your current session/claims setup

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
            int? teacherId = userId;

            // 2. Execute the PostgreSQL function and fetch results into a list
            var reportData = await _context.PblReportTeacherDtos
                .FromSqlRaw("SELECT * FROM pbl_report_teacher({0})", teacherId)
                .ToListAsync();

            using (XLWorkbook wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("PBL Report");

                // 1. Define your custom column headers manually
                ws.Cell(1, 1).Value = "User ID";
                ws.Cell(1, 2).Value = "First Name";
                ws.Cell(1, 3).Value = "Last Name";
                ws.Cell(1, 4).Value = "Standard";
                ws.Cell(1, 5).Value = "School Name";
                ws.Cell(1, 6).Value = "Topic";
               // ws.Cell(1, 7).Value = "PBL Type";
                ws.Cell(1, 8).Value = "File Name";
                ws.Cell(1, 9).Value = "Extension";
                ws.Cell(1, 10).Value = "File URL";
                ws.Cell(1, 11).Value = "Submission Date"; // Adjust labels to match your DTO properties

                // Style the header row (optional: make it bold & give it a light background)
                var headerRow = ws.Row(1);
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

                // 2. Insert the data starting from row 2 horizontally
                int rowIdx = 2;
                foreach (var item in reportData)
                {
                    ws.Cell(rowIdx, 1).Value = item.StudentID;     // Replace with your actual DTO property names
                    ws.Cell(rowIdx, 2).Value = item.FirstName;
                    ws.Cell(rowIdx, 3).Value = item.LastName;
                    ws.Cell(rowIdx, 4).Value = item.Standard;
                    ws.Cell(rowIdx, 5).Value = item.InstituteName;
                    ws.Cell(rowIdx, 6).Value = item.Title;
                    ws.Cell(rowIdx, 7).Value = item.Title;
                    ws.Cell(rowIdx, 8).Value = item.FileName;
                    ws.Cell(rowIdx, 9).Value = item.FileExtension;
                    ws.Cell(rowIdx, 10).Value = item.FileLink;
                    ws.Cell(rowIdx, 11).Value = item.SubmissionDate;

                    rowIdx++;
                }

                // Auto-fit columns for readability
                ws.Columns().AdjustToContents();

                using (MemoryStream stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    var fileName = "PBLReport.xlsx";

                    return File(stream.ToArray(), contentType, fileName);
                }
            }


        }

        [HttpGet("certified-students-report")]
        public async Task<IActionResult> CertifiedStudentsReportForTeacher()
        {
            // 1. Get Teacher ID from current session
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
            int? teacherId = userId;

            // 2. Execute the PostgreSQL function via EF Core
            var reportData = await _context.CertifiedStudentsDtos
                .FromSqlRaw("SELECT * FROM certified_students_details_from_teacher({0})", teacherId)
                .ToListAsync();

            using (XLWorkbook wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Certified Students");

                // 1. Define your custom column headers manually in Row 1
                ws.Cell(1, 1).Value = "Student ID";
                ws.Cell(1, 2).Value = "Student Name";
                ws.Cell(1, 3).Value = "Standard";
                ws.Cell(1, 4).Value = "School Name";
                ws.Cell(1, 5).Value = "Email";
                ws.Cell(1, 6).Value = "Phone";
                ws.Cell(1, 7).Value = "Course ID";
                ws.Cell(1, 8).Value = "Course Name"; // Adjust headers to match your certified students DTO properties

                // Style the header row
                var headerRow = ws.Row(1);
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

                // 2. Insert data row-by-row horizontally
                int rowIdx = 2;
                foreach (var item in reportData)
                {
                    ws.Cell(rowIdx, 1).Value = item.Id;   // Replace with your actual DTO properties
                    ws.Cell(rowIdx, 2).Value = item.StudentName;
                    ws.Cell(rowIdx, 3).Value = item.Standard;
                    ws.Cell(rowIdx, 4).Value = item.InstituteName;
                    ws.Cell(rowIdx, 5).Value = item.EmailID;
                    ws.Cell(rowIdx, 6).Value = item.MobileNo;
                    ws.Cell(rowIdx, 7).Value = item.CourseID;
                    ws.Cell(rowIdx, 8).Value = item.CourseName;

                    rowIdx++;
                }

                // Auto-fit column widths for clear readability
                ws.Columns().AdjustToContents();

                using (MemoryStream stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    var fileName = "CertifiedStudentsReport.xlsx";

                    return File(stream.ToArray(), contentType, fileName);
                }
            }
        }
    }
}