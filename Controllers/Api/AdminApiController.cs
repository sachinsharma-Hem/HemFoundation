using HemFoundation.Data;
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

        public AdminApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Example API endpoint to fetch dashboard statistics or authenticated user info securely
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

        // 1. Fetch all bulk upload logs with School Name
        [HttpGet("bulk-logs")]
        public async Task<IActionResult> GetBulkLogs()
        {
            var logs = await (from l in _context.BulkUploadLogs
                              join i in _context.Institutes on l.InstituteID equals i.InstituteID
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

        // 2. Update status of a specific log
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
    }
}