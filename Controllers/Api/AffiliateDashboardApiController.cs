using HemFoundation.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Authorize] // Login required for these APIs
    [Route("api/affiliate-dashboard")] // Clean specific route
    [ApiController]
    public class AffiliateDashboardApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        // Constructor for Dependency Injection
        public AffiliateDashboardApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetAffiliateDashboardStats()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not found." });

            var affiliate = await _context.Affiliates.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
            if (affiliate == null)
                return Unauthorized(new { success = false, message = "Affiliate profile not found." });

            var affIdStr = affiliate.UserID.ToString();

            // Calculate Counts
            int associatedSchoolsCount = await _context.Institutes
                .Where(i => i.IsActive && i.ReferredByAffiliateId == affIdStr)
                .CountAsync();

            // Calculate other counts here as needed in the future
            // Example: int associatedStudentsCount = await _context.Students.Where(s => s.AffiliateId == affiliate.UserID).CountAsync();

            // Return stats
            return Ok(new
            {
                success = true,
                data = new
                {
                    associatedSchools = associatedSchoolsCount,
                    associatedStudents = 70, // Placeholder
                    activeStudents = 40,     // Placeholder
                    associatedTeachers = 33, // Placeholder
                    activeTeachers = 1,      // Placeholder
                    selSubmissions = 1,      // Placeholder
                    groupProjects = 1,       // Placeholder
                    familyProjects = 13,     // Placeholder
                    individualProjects = 36, // Placeholder
                    newSchools = 6           // Placeholder
                }
            });
        }

        [HttpGet("associated-schools-details")]
        public async Task<IActionResult> GetAssociatedSchoolsDetails()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not found." });

            var affiliate = await _context.Affiliates.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
            if (affiliate == null)
                return Unauthorized(new { success = false, message = "Affiliate profile not found." });

            var affIdStr = affiliate.UserID.ToString();

            var assignedSchools = await _context.Institutes
                .Where(i => i.IsActive && i.ReferredByAffiliateId == affIdStr)
                .Select(i => new
                {
                    i.InstituteID,
                    i.Name,
                    i.City,
                    i.State,
                    i.MobileNo
                })
                .OrderBy(i => i.Name)
                .ToListAsync();

            return Ok(new { success = true, data = assignedSchools });
        }

        // Yeh endpoint AffiliateDashboardApiController ke andar add karein

        [HttpGet("activity-stats")]
        public async Task<IActionResult> GetActivityDashboardStats([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not found." });

            var affiliate = await _context.Affiliates.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
            if (affiliate == null)
                return Unauthorized(new { success = false, message = "Affiliate profile not found." });

            // Base query for Plans
            var planQuery = _context.AffiliateVisitPlans
                .Where(v => v.AffiliateId == affiliate.UserID && !v.IsDeleted);

            // Base query for Reports (Activities)
            var reportQuery = _context.AffiliateVisitReports
                .Where(r => _context.AffiliateVisitPlans.Any(p => p.Id == r.VisitPlanId && p.AffiliateId == affiliate.UserID));

            // Apply Date Filters
            if (fromDate.HasValue)
            {
                planQuery = planQuery.Where(v => v.ScheduleDate >= fromDate.Value.Date);
                reportQuery = reportQuery.Where(r => r.ReportedDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1); // Up to 23:59:59 of selected day
                planQuery = planQuery.Where(v => v.ScheduleDate <= endOfDay);
                reportQuery = reportQuery.Where(r => r.ReportedDate <= endOfDay);
            }

            // 1. Visit Schedule: Total visits planned in this date range
            int visitSchedule = await planQuery.CountAsync();

            // 2. Visited: Visits marked as Completed
            int visited = await planQuery.CountAsync(v => v.Status == "Completed");

            // 3. Visit Pending: Visits marked as Pending or Active
            int visitPending = await planQuery.CountAsync(v => v.Status == "Pending" || v.Status == "Active");

            // 4. Activity Updated: Total visit reports/executions submitted in this date range
            int activityUpdated = await reportQuery.CountAsync();

            // 5. Activity Pending: Same as Visit Pending (Needs execution)
            int activityPending = visitPending;

            return Ok(new
            {
                success = true,
                data = new
                {
                    visitSchedule,
                    visited,
                    visitPending,
                    activityUpdated,
                    activityPending
                }
            });
        }
    }
}