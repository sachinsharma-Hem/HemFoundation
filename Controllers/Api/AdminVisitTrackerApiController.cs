using HemFoundation.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Authorize]
    [Route("api/admin-visit-tracker")]
    [ApiController]
    public class AdminVisitTrackerApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminVisitTrackerApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("affiliates")]
        public async Task<IActionResult> GetAffiliates()
        {
            var affiliates = await _context.View_HF_Affiliate
                .AsNoTracking()
                .Select(x => new
                {
                    userID = x.InstituteID,
                    name = ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim()
                })
                .Distinct()
                .OrderBy(x => x.name)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = affiliates
            });
        }

        [HttpGet("track")]
        public async Task<IActionResult> TrackVisits(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] long? affiliateId)
        {
            // ADMIN: Start with complete table
            var planQuery = _context.AffiliateVisitPlans
                .AsQueryable();

            // Optional filters
            if (fromDate.HasValue)
            {
                planQuery = planQuery.Where(p =>
                    p.ScheduleDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endOfDay = toDate.Value.Date
                    .AddDays(1)
                    .AddTicks(-1);

                planQuery = planQuery.Where(p =>
                    p.ScheduleDate <= endOfDay);
            }

            if (affiliateId.HasValue && affiliateId.Value > 0)
            {
                planQuery = planQuery.Where(p =>
                    p.AffiliateId == affiliateId.Value);
            }

            // STATS
            var totalScheduled = await planQuery.CountAsync();

            var totalCompleted = await planQuery
                .CountAsync(p => p.Status == "Completed");

            var totalPending = await planQuery
                .CountAsync(p => p.Status == "Pending" || p.Status == "Active");

            var totalCancelled = await planQuery
                .CountAsync(p => p.Status == "Cancelled");

            // VISIT DATA
            var visitList = await (
                from p in planQuery

                join a in _context.Users
                    on p.AffiliateId equals a.UserID into affGroup
                from aff in affGroup.DefaultIfEmpty()

                join i in _context.WJ_Institutes
                    on p.InstituteID equals i.InstituteID into instGroup
                from inst in instGroup.DefaultIfEmpty()

                join r in _context.AffiliateVisitReports
                    on p.Id equals r.VisitPlanId into reportGroup

                from rep in reportGroup
                    .OrderByDescending(x => x.ReportedDate)
                    .Take(1)
                    .DefaultIfEmpty()

                orderby p.ScheduleDate descending

                select new
                {
                    VisitId = p.Id,

                    AffiliateName = aff != null
                        ? aff.FirstName + " " + aff.LastName
                        : "Unknown Affiliate",

                    SchoolName = inst != null
                        ? inst.Name
                        : "Unknown School",

                    City = inst != null
                        ? null
                        : "-",

                    p.ScheduleDate,
                    p.Purpose,
                    p.Status,
                    p.Remarks,

                    PhotoUrl = rep != null
                        ? rep.PhotoUrl
                        : null,

                    Latitude = rep != null
                        ? rep.Latitude
                        : null,

                    Longitude = rep != null
                        ? rep.Longitude
                        : null,

                    ReportedDate = rep != null
                        ? (DateTime?)rep.ReportedDate
                        : null,

                    ReportDescription = rep != null
                        ? rep.Description
                        : null
                }
            ).ToListAsync();

            return Ok(new
            {
                success = true,

                stats = new
                {
                    totalScheduled,
                    totalCompleted,
                    totalPending,
                    totalCancelled
                },

                data = visitList
            });
        }
    }
}