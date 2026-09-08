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
                // 1. Add a worksheet with the desired sheet name
                var ws = wb.Worksheets.Add("PBL Report");

                // 2. Insert the list of DTOs directly starting from cell A1 (automatically handles headers!)
                ws.Cell(1, 1).InsertData(reportData);

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
                // Add worksheet and automatically insert strongly-typed list data starting at A1
                var ws = wb.Worksheets.Add("Certified Students");
                ws.Cell(1, 1).InsertData(reportData);

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