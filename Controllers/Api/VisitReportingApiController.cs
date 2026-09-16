using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Authorize]
    [Route("api/visit-reporting")]
    [ApiController]
    public class VisitReportingApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public VisitReportingApiController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private async Task<long> GetCurrentAffiliateId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var affiliate = await _context.Users
                .Where(x => x.AspNetUserID == userId)
                .Select(x => x.UserID)
                .FirstOrDefaultAsync();

            return affiliate;
        }

        [HttpGet("activity-types")]
        public async Task<IActionResult> GetActivityTypes()
        {
            var types = await _context.ActivityTypeMasters
                .FromSqlRaw(@"
            SELECT *
            FROM ""ActivityTypeMasters""
            WHERE ""IsActive"" = TRUE
              AND ""IsDeleted"" = FALSE")
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = types
            });
        }

        [HttpGet("list/{statusType}")]
        public async Task<IActionResult> GetVisits(string statusType)
        {
            long affId = await GetCurrentAffiliateId();

            var query = _context.AffiliateVisitPlans
                .Where(v => v.AffiliateId == affId && !v.IsDeleted);

            if (statusType.Equals("pending", StringComparison.OrdinalIgnoreCase))
                query = query.Where(v => v.Status == "Pending" || v.Status == "Active");
            else
                query = query.Where(v => v.Status == statusType);

            // Left join with SchoolProfileDetails & AffiliateVisitReports to get complete details
            var visits = await query
                .Join(
                    _context.SchoolProfileDetails,
                    v => v.InstituteID,
                    s => s.instituteid,
                    (v, s) => new { v, s })
                .GroupJoin(
                    _context.AffiliateVisitReports,
                    vs => vs.v.Id,
                    r => r.VisitPlanId,
                    (vs, reports) => new { vs.v, vs.s, report = reports.FirstOrDefault() })
                .Select(x => new
                {
                    x.v.Id,
                    x.v.InstituteID,
                    SchoolName = x.s.institute_name,
                    SchoolCity = x.s.cityname,
                    x.v.ScheduleDate,
                    x.v.Purpose,
                    x.v.Status,
                    Description = x.report != null ? x.report.Description : null,
                    Latitude = x.report != null ? x.report.Latitude : null,
                    Longitude = x.report != null ? x.report.Longitude : null,
                    PhotoUrl = x.report != null ? x.report.PhotoUrl : null
                })
                .OrderByDescending(v => v.ScheduleDate)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = visits
            });
        }

        [HttpPost("submit-report")]
        public async Task<IActionResult> SubmitReport([FromForm] VisitReportDto model)
        {
            long affId = await GetCurrentAffiliateId();

            var plan = await _context.AffiliateVisitPlans.FirstOrDefaultAsync(v => v.Id == model.VisitPlanId && v.AffiliateId == affId);
            if (plan == null) return NotFound(new { success = false, message = "Visit plan not found." });

            if (plan.Status == "Completed" || plan.Status == "Cancelled")
                return BadRequest(new { success = false, message = "This visit is already closed and cannot be updated." });

            string? photoPath = null;

            // Handle Photo Upload
            if (model.PhotoFile != null && model.PhotoFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "visits");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + model.PhotoFile.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.PhotoFile.CopyToAsync(fileStream);
                }
                photoPath = "/uploads/visits/" + uniqueFileName;
            }

            // Save Report
            var report = new AffiliateVisitReport
            {
                VisitPlanId = model.VisitPlanId,
                ActivityTypeId = model.ActivityTypeId,
                Description = model.Description,
                PhotoUrl = photoPath,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                ReportedDate = DateTime.UtcNow
            };
            _context.AffiliateVisitReports.Add(report);

            // Auto-update the Plan status to Completed
            plan.Status = "Completed";
            plan.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Visit reported and marked as completed successfully!" });
        }
    }

    public class VisitReportDto
    {
        public int VisitPlanId { get; set; }
        public int ActivityTypeId { get; set; }
        public string? Description { get; set; }
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        public IFormFile? PhotoFile { get; set; }
    }
}