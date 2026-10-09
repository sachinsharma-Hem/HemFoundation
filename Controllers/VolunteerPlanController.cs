using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers
{
    [Route("api/volunteer-plan")]
    [ApiController]
    public class VolunteerPlanController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public VolunteerPlanController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("/VolunteerPlan/plan")]
        public IActionResult plan()
        {
            var isLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            if (string.IsNullOrEmpty(isLoggedIn) || isLoggedIn != "true")
            {
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Volunteer/PlanVisit.cshtml");
        }

        private async Task<Volunteer?> GetCurrentVolunteerAsync()
        {
            // 1. Try finding via Claims (NameIdentifier or Identity Name)
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name;
            Volunteer? volunteer = null;

            if (!string.IsNullOrEmpty(userId))
            {
                volunteer = await _context.Volunteers
                    .FirstOrDefaultAsync(v => (v.AspNetUserId == userId || v.MobileNo == userId) && !v.IsDeleted);
            }

            // 2. Fallback to Session keys if claim is not present
            if (volunteer == null)
            {
                var sessVolId = HttpContext.Session.GetString("VolunteerId") ?? HttpContext.Session.GetString("UserId");
                if (!string.IsNullOrEmpty(sessVolId))
                {
                    if (int.TryParse(sessVolId, out int vId))
                    {
                        volunteer = await _context.Volunteers.FirstOrDefaultAsync(v => v.Id == vId && !v.IsDeleted);
                    }
                    else
                    {
                        volunteer = await _context.Volunteers.FirstOrDefaultAsync(v => (v.AspNetUserId == sessVolId || v.MobileNo == sessVolId) && !v.IsDeleted);
                    }
                }
            }

            // 3. Fallback for testing/development if single volunteer exists
            if (volunteer == null)
            {
                volunteer = await _context.Volunteers.FirstOrDefaultAsync(v => !v.IsDeleted);
            }

            return volunteer;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetPlans()
        {
            var volunteer = await GetCurrentVolunteerAsync();

            if (volunteer == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Unauthorized"
                });
            }

            var plans = await _context.VolunteerVisitPlans
                .AsNoTracking()
                .Where(p =>
                    p.VolunteerId == volunteer.Id &&
                    !p.IsDeleted
                )
                .OrderByDescending(p => p.ScheduleDate)
                .Select(p => new
                {
                    p.Id,
                    p.IsOtherActivity,

                    SchoolName = p.IsOtherActivity
                        ? p.SchoolName
                        : _context.SchoolProfileDetails
                            .Where(s => s.instituteid == p.InstituteId)
                            .Select(s => s.institute_name)
                            .FirstOrDefault(),

                    p.ScheduleDate,
                    VisitDate = p.visitdate,
                    p.Purpose,
                    p.Status,
                    p.Remarks,
                    p.ImagePath,
                    p.PlanLatitude,
                    p.PlanLongitude,
                    p.ReportLatitude,
                    p.ReportLongitude
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = plans
            });
        }

        // 4. Update Progress / Complete Visit
        [HttpPost("update-progress")]
        public async Task<IActionResult> UpdateProgress(
            [FromForm] int id,
            [FromForm] string remarks,
            [FromForm] string status,
            [FromForm] DateTime? visitDate,
            [FromForm] string? latitude,
            [FromForm] string? longitude,
            IFormFile? visitImage)
        {
            var plan = await _context.VolunteerVisitPlans.FindAsync(id);
            if (plan == null) return NotFound(new { success = false, message = "Plan not found." });

            if (plan.Status != null && (plan.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) || plan.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { success = false, message = "Ye visit pehle hi complete ya cancel ho chuki hai, ab dubara update nahi ho sakti." });
            }

            if (status == "Completed" && (visitImage == null || visitImage.Length == 0) && string.IsNullOrEmpty(plan.ImagePath))
            {
                return BadRequest(new { success = false, message = "Photo proof is mandatory when completing a visit." });
            }

            plan.Remarks = remarks;
            plan.Status = status;
            plan.visitdate = visitDate ?? DateTime.UtcNow;
            plan.ReportLatitude = latitude;
            plan.ReportLongitude = longitude;
            plan.ReportedDate = DateTime.UtcNow;
            plan.ModifiedDate = DateTime.UtcNow;

            if (visitImage != null && visitImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "volunteer_visits");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + visitImage.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await visitImage.CopyToAsync(stream);
                }

                plan.ImagePath = "/uploads/volunteer_visits/" + uniqueFileName;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Visit status updated successfully." });
        }

        [HttpGet("assigned-schools")]
        public async Task<IActionResult> GetAssignedSchools()
        {
            try
            {
                var volunteer = await GetCurrentVolunteerAsync();

                if (volunteer == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Unauthorized"
                    });
                }

                var instituteIds = await _context.VolunteerInstituteMappings
                    .Where(m =>
                        m.VolunteerId == volunteer.Id &&
                        m.IsActive &&
                        !m.IsDeleted)
                    .Select(m => m.InstituteId)
                    .Distinct()
                    .ToListAsync();

                if (!instituteIds.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        data = new List<object>()
                    });
                }

                var schools = await _context.SchoolProfileDetails
                    .AsNoTracking()
                    .Where(s => instituteIds.Contains(s.instituteid))
                    .Select(s => new
                    {
                        InstituteID = s.instituteid,
                        Name = s.institute_name
                    })
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = schools
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Error while loading assigned schools.",
                    error = ex.InnerException?.Message ?? ex.Message
                });
            }
        }

        // 3. Save / Create Visit Plan
        [HttpPost("save")]
        public async Task<IActionResult> SavePlan([FromBody] VolunteerVisitPlan model)
        {
            try
            {
                var volunteer = await GetCurrentVolunteerAsync();
                if (volunteer == null)
                    return Unauthorized(new { success = false, message = "Unauthorized volunteer session." });

                model.VolunteerId = volunteer.Id;
                model.CreatedDate = DateTime.Now;
                model.IsActive = true;
                model.Status = string.IsNullOrEmpty(model.Status) ? "Pending" : model.Status;

                _context.VolunteerVisitPlans.Add(model);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Visit plan scheduled successfully." });
            }
            catch (Exception ex)
            {
                var exactError = ex.InnerException?.Message ?? ex.Message;
                System.Diagnostics.Debug.WriteLine("DB SAVE ERROR: " + exactError);

                return BadRequest(new
                {
                    success = false,
                    message = "Error while saving visit plan.",
                    error = exactError
                });
            }
        }

        [HttpGet("dashboard-counts")]
        public async Task<IActionResult> GetDashboardCounts(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            var volunteer = await GetCurrentVolunteerAsync();

            if (volunteer == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Unauthorized"
                });
            }

            var query = _context.VolunteerVisitPlans
                .AsNoTracking()
                .Where(p =>
                    p.VolunteerId == volunteer.Id &&
                    !p.IsDeleted &&
                    p.IsActive);

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(p => p.ScheduleDate >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date.AddDays(1);
                query = query.Where(p => p.ScheduleDate < to);
            }

            var visitPlanCount = await query.CountAsync();

            var visitedCount = await query.CountAsync(p =>
                p.Status != null &&
                p.Status.ToLower() == "completed");

            var pendingCount = await query.CountAsync(p =>
                p.Status == null ||
                p.Status.ToLower() != "completed");

            var activityUpdatedCount = await query.CountAsync(p =>
                p.ReportedDate.HasValue ||
                !string.IsNullOrWhiteSpace(p.Remarks));

            var activityPendingCount = await query.CountAsync(p =>
                !p.ReportedDate.HasValue &&
                string.IsNullOrWhiteSpace(p.Remarks));

            return Ok(new
            {
                success = true,
                data = new
                {
                    visitPlan = visitPlanCount,
                    visited = visitedCount,
                    visitPending = pendingCount,
                    activityUpdated = activityUpdatedCount,
                    activityPending = activityPendingCount
                }
            });
        }
    }
}