using HemFoundation.Data;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<ApplicationUser> _userManager;

        // Constructor for Dependency Injection
        public AffiliateDashboardApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetAffiliateDashboardStats()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var affiliate = await _context.Users
                .FirstOrDefaultAsync(x => x.AspNetUserID == userId);

            if (affiliate == null)
                return Unauthorized(new { success = false, message = "Affiliate not found." });

            var referralCode = await _context.WJ_ReferralCodes
                .Where(x => x.UserID == affiliate.UserID)
                .Select(x => x.ReferralCode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(referralCode))
                return Ok(new { success = true, data = new { } });

            var data = await _context.SchoolProfileDetails
                .Where(x => x.referralcode == referralCode)
                .GroupBy(x => 1)
                .Select(g => new
                {
                    associatedSchools = g.Count(),
                    associatedStudents = g.Sum(x => x.associatedstudents ?? 0),
                    activeStudents = g.Sum(x => x.activestudents ?? 0),
                    associatedTeachers = g.Sum(x => x.totalteachers ?? 0),
                    activeTeachers = g.Sum(x => x.activeteachers ?? 0)
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                success = true,
                data = data ?? new
                {
                    associatedSchools = 0,
                    associatedStudents = 0,
                    activeStudents = 0,
                    associatedTeachers = 0,
                    activeTeachers = 0
                }
            });
        }

        [HttpGet("associated-schools-details")]
        public async Task<IActionResult> GetAssociatedSchoolsDetails()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var affiliate = await _context.Users
                .FirstOrDefaultAsync(x => x.AspNetUserID == userId);

            if (affiliate == null)
                return Unauthorized(new { success = false, message = "Affiliate not found." });

            var referralCode = await _context.WJ_ReferralCodes
                .Where(x => x.UserID == affiliate.UserID)
                .Select(x => x.ReferralCode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(referralCode))
                return Ok(new { success = true, data = new List<object>() });

            var assignedSchools = await _context.SchoolProfileDetails
                .Where(x => x.referralcode == referralCode)
                .OrderBy(x => x.institute_name)
                .Select(x => new
                {
                    instituteID = x.instituteid,
                    name = x.institute_name,
                    city = x.cityname,
                    mobileNo = x.mobileno
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = assignedSchools
            });
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