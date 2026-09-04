using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Authorize]
    [Route("api/plan-visit")]
    [ApiController]
    public class PlanVisitApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PlanVisitApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<long> GetCurrentAffiliateId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var affiliate = await _context.Users.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
            return affiliate?.UserID ?? 0;
        }

        // 1. Get ONLY assigned schools for the Dropdown
        [HttpGet("my-schools")]
        public async Task<IActionResult> GetMySchools()
        {
            long affId = await GetCurrentAffiliateId();

            var referralCode = await _context.WJ_ReferralCodes
                .Where(x => x.UserID == affId)
                .Select(x => x.ReferralCode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(referralCode))
                return Ok(new { success = true, data = new List<object>() });

            var schools = await _context.SchoolProfileDetails
                .Where(x => x.referralcode == referralCode)
                .Select(x => new
                {
                    instituteID = x.instituteid,
                    name = x.institute_name,
                    city = x.cityname
                })
                .OrderBy(x => x.name)
                .ToListAsync();

            return Ok(new { success = true, data = schools });
        }

        // 2. Create New Visit
        [HttpPost("create")]
        public async Task<IActionResult> CreateVisit([FromBody] AffiliateVisitPlan model)
        {
            long affId = await GetCurrentAffiliateId();
            if (affId == 0) return Unauthorized();

            model.AffiliateId = affId;
            model.Status = "Pending";
            model.CreatedDate = DateTime.UtcNow;

            _context.AffiliateVisitPlans.Add(model);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Visit scheduled successfully!" });
        }

        // 3. Get All Planned Visits
        [HttpGet("list")]
        public async Task<IActionResult> GetMyVisits()
        {
            long affId = await GetCurrentAffiliateId();

            var visits = await _context.AffiliateVisitPlans
                .Where(v => v.AffiliateId == affId && !v.IsDeleted)
                .Join(
                    _context.SchoolProfileDetails,
                    v => v.InstituteID,
                    s => s.instituteid,
                    (v, s) => new
                    {
                        v.Id,
                        v.InstituteID,
                        SchoolName = s.institute_name,
                        SchoolCity = s.cityname,
                        v.ScheduleDate,
                        v.Purpose,
                        v.Status,
                        v.Remarks
                    })
                .OrderByDescending(v => v.ScheduleDate)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = visits
            });
        }

        // 4. Update Status (Pending -> Active -> Complete/Cancel)
        [HttpPost("update-status/{id}")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusUpdateModel model)
        {
            var visit = await _context.AffiliateVisitPlans.FirstOrDefaultAsync(v => v.Id == id);
            if (visit == null) return NotFound("Visit not found");

            visit.Status = model.Status;
            visit.Remarks = model.Remarks;
            visit.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Status updated successfully!" });
        }

        // 5. Get Visit History of a specific School
        [HttpGet("school-history/{instituteId}")]
        public async Task<IActionResult> GetSchoolHistory(int instituteId)
        {
            long affId = await GetCurrentAffiliateId();

            var history = await _context.AffiliateVisitPlans
                .Where(v =>
                    v.AffiliateId == affId &&
                    v.InstituteID == instituteId &&
                    !v.IsDeleted)
                .OrderByDescending(v => v.ScheduleDate)
                .Select(v => new
                {
                    v.ScheduleDate,
                    v.Purpose,
                    v.Status,
                    v.Remarks
                })
                .ToListAsync();

            var schoolName = await _context.SchoolProfileDetails
                .Where(s => s.instituteid == instituteId)
                .Select(s => s.institute_name)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                success = true,
                schoolName,
                data = history
            });
        }
    }

    public class StatusUpdateModel
    {
        public string Status { get; set; } = string.Empty;
        public string? Remarks { get; set; }
    }
}