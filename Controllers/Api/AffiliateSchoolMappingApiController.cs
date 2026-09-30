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

        [HttpGet("affiliates-list")]
        public async Task<IActionResult> GetAffiliatesList()
        {
            var list = await _context.View_HF_Affiliate
                .AsNoTracking()
                .Where(x => x.SchoolCount >= 5)
                .Select(x => new
                {
                    id = x.InstituteID,
                    name = (x.FirstName ?? "") + " " + (x.LastName ?? "")
                })
                .Distinct()
                .OrderBy(x => x.name)
                .ToListAsync();

            return Ok(new { success = true, data = list });
        }

        [HttpGet("get-mapping-data/{affiliateId}")]
        public async Task<IActionResult> GetMappingData(long affiliateId)
        {
            int? affIdStr = (int?)affiliateId;


            var referralCodeStr = await _context.WJ_ReferralCodes
               .Where(x => x.UserID == affIdStr)
               .Select(x => x.ReferralCode)
               .FirstOrDefaultAsync();

            var assignedSchools = await _context.WJ_Institutes
                .Where(i => i.IsActive && i.ReferralCode == referralCodeStr)
                .Select(i => new
                {
                    i.InstituteID,
                    i.Name
                   
                })
                .OrderBy(i => i.Name)
                .ToListAsync();


            var availableSchools = await _context.WJ_Institutes
                .Where(i => i.IsActive && i.ReferralCode != referralCodeStr)
                .Select(i => new
                {
                    i.InstituteID,
                    i.Name,
                    i.ReferralCode
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

        [HttpPost("assign-schools")]
        public async Task<IActionResult> AssignSchools([FromBody] AssignSchoolsModel model)
        {
            if (model.AffiliateId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid Affiliate selected." });
            }

            var targetAffiliate = await _context.Users.FirstOrDefaultAsync(a => a.UserID == model.AffiliateId);
            if (targetAffiliate == null)
            {
                return NotFound(new { success = false, message = "Selected Affiliate not found." });
            }
            var reffrelcode = await _context.WJ_ReferralCodes.FirstOrDefaultAsync(a => a.UserID == model.AffiliateId);
            if (reffrelcode == null)
            {
                return NotFound(new { success = false, message = "Selected referrel code not found." });
            }

            var newAffName = targetAffiliate.FirstName + " " + targetAffiliate.LastName;
            var targetAffIdStr = model.AffiliateId.ToString();
            var istNow = GetIstTime();

            // Get all schools currently assigned to this affiliate
            var currentAssignedInstitutes = await _context.WJ_Institutes
                .Where(i => i.ReferralCode == reffrelcode.ReferralCode)
                .ToListAsync();

            // Selected Institute IDs sent from UI
            var newSelectedIDs = model.InstituteIDs ?? new List<int>();

            // 1) Schools to be ADDED or RE-ASSIGNED
            var institutesToAdd = await _context.WJ_Institutes
                .Where(i => newSelectedIDs.Contains(i.InstituteID) && i.ReferralCode != reffrelcode.ReferralCode)
                .ToListAsync();

            foreach (var inst in institutesToAdd)
            {
                string? prevAffIdStr = inst.ReferralCode;
                string? prevAffName = "Unassigned";

                if (!string.IsNullOrEmpty(inst.ReferralCode) )
                {
                    var prevAff = await _context.WJ_ReferralCodes.FirstOrDefaultAsync(a => a.ReferralCode == inst.ReferralCode);
                    var prevAffdetails = await _context.Users.FirstOrDefaultAsync(a => a.UserID == prevAff.UserID);

                    prevAffName = prevAffdetails.FirstName + " " + prevAffdetails.LastName;
                    
                }

                // Update mapping
                inst.ReferralCode = reffrelcode.ReferralCode;

                // Log audit history
                _context.AffiliateSchoolMappingHistories.Add(new AffiliateSchoolMappingHistory
                {
                    InstituteID = inst.InstituteID,
                    //PreviousAffiliateId = !string.IsNullOrEmpty(prevAffIdStr) && long.TryParse(prevAffIdStr, out long pId) ? pId : null,
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
                inst.ReferralCode = null; // Unassigned

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

       
    }

    public class AssignSchoolsModel
    {
        public long AffiliateId { get; set; }
        public List<int> InstituteIDs { get; set; } = new();
    }
}