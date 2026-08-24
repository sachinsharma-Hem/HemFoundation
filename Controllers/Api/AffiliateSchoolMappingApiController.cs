using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Authorize]
    [Route("api/affiliate-school-mapping")]
    [ApiController]
    public class AffiliateSchoolMappingApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AffiliateSchoolMappingApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static DateTime GetIstTime()
        {
            TimeZoneInfo istZone = OperatingSystem.IsWindows()
                ? TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
                : TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
        }

        // 1. Get Dropdown List of All Active Affiliates
        [HttpGet("affiliates-list")]
        public async Task<IActionResult> GetAffiliatesList()
        {
            var list = await _context.Affiliates
                .Where(a => a.IsActive)
                .Select(a => new
                {
                    a.UserID,
                    Name = a.FirstName + " " + a.LastName + " (" + (a.UserCode ?? "N/A") + ")"
                })
                .OrderBy(a => a.Name)
                .ToListAsync();

            return Ok(new { success = true, data = list });
        }

        // 2. Get Data for Selected Affiliate
        [HttpGet("get-mapping-data/{affiliateId}")]
        public async Task<IActionResult> GetMappingData(long affiliateId)
        {
            var affIdStr = affiliateId.ToString();

            // Currently Assigned Schools to THIS Affiliate
            var assignedSchools = await _context.Institutes
                .Where(i => i.IsActive && i.ReferredByAffiliateId == affIdStr)
                .Select(i => new
                {
                    i.InstituteID,
                    i.Name,
                    i.City,
                    i.State,
                    i.Address,
                    i.MobileNo,
                    i.EmailID
                })
                .OrderBy(i => i.Name)
                .ToListAsync();

            // Available Schools = All Active Schools EXCEPT those assigned to THIS Affiliate
            // (Isme Unassigned schools + Other Affiliates ke mapped schools ayenge)
            var availableSchools = await _context.Institutes
                .Where(i => i.IsActive && i.ReferredByAffiliateId != affIdStr)
                .Select(i => new
                {
                    i.InstituteID,
                    i.Name,
                    i.City,
                    i.ReferredByAffiliateId
                })
                .OrderBy(i => i.Name)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                assignedSchools,
                availableSchools
            });
        }

        // 3. Assign / Transfer / Remove Bulk Schools
        [HttpPost("assign-schools")]
        public async Task<IActionResult> AssignSchools([FromBody] AssignSchoolsModel model)
        {
            if (model.AffiliateId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid Affiliate selected." });
            }

            var targetAffiliate = await _context.Affiliates.FirstOrDefaultAsync(a => a.UserID == model.AffiliateId);
            if (targetAffiliate == null)
            {
                return NotFound(new { success = false, message = "Selected Affiliate not found." });
            }

            var newAffName = targetAffiliate.FirstName + " " + targetAffiliate.LastName;
            var targetAffIdStr = model.AffiliateId.ToString();
            var istNow = GetIstTime();

            // Get all schools currently assigned to this affiliate
            var currentAssignedInstitutes = await _context.Institutes
                .Where(i => i.ReferredByAffiliateId == targetAffIdStr)
                .ToListAsync();

            // Selected Institute IDs sent from UI
            var newSelectedIDs = model.InstituteIDs ?? new List<int>();

            // 1) Schools to be ADDED or RE-ASSIGNED
            var institutesToAdd = await _context.Institutes
                .Where(i => newSelectedIDs.Contains(i.InstituteID) && i.ReferredByAffiliateId != targetAffIdStr)
                .ToListAsync();

            foreach (var inst in institutesToAdd)
            {
                string? prevAffIdStr = inst.ReferredByAffiliateId;
                string? prevAffName = "Unassigned";

                if (!string.IsNullOrEmpty(prevAffIdStr) && long.TryParse(prevAffIdStr, out long prevId))
                {
                    var prevAff = await _context.Affiliates.FirstOrDefaultAsync(a => a.UserID == prevId);
                    if (prevAff != null)
                    {
                        prevAffName = prevAff.FirstName + " " + prevAff.LastName;
                    }
                }

                // Update mapping
                inst.ReferredByAffiliateId = targetAffIdStr;

                // Log audit history
                _context.AffiliateSchoolMappingHistories.Add(new AffiliateSchoolMappingHistory
                {
                    InstituteID = inst.InstituteID,
                    PreviousAffiliateId = !string.IsNullOrEmpty(prevAffIdStr) && long.TryParse(prevAffIdStr, out long pId) ? pId : null,
                    PreviousAffiliateName = prevAffName,
                    NewAffiliateId = model.AffiliateId,
                    NewAffiliateName = newAffName,
                    AssignedDate = istNow,
                    AssignedBy = "Admin"
                });
            }

            // 2) Schools REMOVED from this affiliate (if moved back to left box)
            var institutesToRemove = currentAssignedInstitutes
                .Where(i => !newSelectedIDs.Contains(i.InstituteID))
                .ToList();

            foreach (var inst in institutesToRemove)
            {
                inst.ReferredByAffiliateId = null; // Unassigned

                _context.AffiliateSchoolMappingHistories.Add(new AffiliateSchoolMappingHistory
                {
                    InstituteID = inst.InstituteID,
                    PreviousAffiliateId = model.AffiliateId,
                    PreviousAffiliateName = newAffName,
                    NewAffiliateId = 0,
                    NewAffiliateName = "Unassigned",
                    AssignedDate = istNow,
                    AssignedBy = "Admin"
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"School mappings updated successfully for {newAffName}." });
        }

        // 4. Get Mapping Lifecycle History Tree for a Specific School
        [HttpGet("school-history/{instituteId}")]
        public async Task<IActionResult> GetSchoolHistory(int instituteId)
        {
            var school = await _context.Institutes.FirstOrDefaultAsync(i => i.InstituteID == instituteId);
            if (school == null) return NotFound(new { success = false, message = "School not found." });

            var logs = await _context.AffiliateSchoolMappingHistories
                .Where(h => h.InstituteID == instituteId && !h.IsDeleted)
                .OrderByDescending(h => h.AssignedDate)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                schoolName = school.Name,
                schoolCity = school.City,
                historyTree = logs
            });
        }
    }

    public class AssignSchoolsModel
    {
        public long AffiliateId { get; set; }
        public List<int> InstituteIDs { get; set; } = new();
    }
}