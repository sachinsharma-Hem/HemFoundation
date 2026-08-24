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
            var affiliate = await _context.Affiliates.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
            return affiliate?.UserID ?? 0;
        }

        // 1. Get ONLY assigned schools for the Dropdown
        [HttpGet("my-schools")]
        public async Task<IActionResult> GetMySchools()
        {
            long affId = await GetCurrentAffiliateId();
            string affIdStr = affId.ToString();

            var schools = await _context.Institutes
                .Where(i => i.IsActive && i.ReferredByAffiliateId == affIdStr)
                .Select(i => new { i.InstituteID, i.Name, i.City })
                .OrderBy(i => i.Name)
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
                .Join(_context.Institutes,
                      v => v.InstituteID,
                      i => i.InstituteID,
                      (v, i) => new
                      {
                          v.Id,
                          v.InstituteID,
                          SchoolName = i.Name,
                          SchoolCity = i.City,
                          v.ScheduleDate,
                          v.Purpose,
                          v.Status,
                          v.Remarks
                      })
                .OrderByDescending(v => v.ScheduleDate)
                .ToListAsync();

            return Ok(new { success = true, data = visits });
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
                .Where(v => v.AffiliateId == affId && v.InstituteID == instituteId && !v.IsDeleted)
                .OrderByDescending(v => v.ScheduleDate)
                .Select(v => new { v.ScheduleDate, v.Purpose, v.Status, v.Remarks })
                .ToListAsync();

            var school = await _context.Institutes.FirstOrDefaultAsync(i => i.InstituteID == instituteId);

            return Ok(new { success = true, schoolName = school?.Name, data = history });
        }
    }

    public class StatusUpdateModel
    {
        public string Status { get; set; } = string.Empty;
        public string? Remarks { get; set; }
    }
}