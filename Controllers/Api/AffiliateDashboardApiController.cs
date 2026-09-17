using HemFoundation.Data;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Authorize] 
    [Route("api/affiliate-dashboard")]
    [ApiController]
    public class AffiliateDashboardApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

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


        [HttpGet("activity-stats")]
        public async Task<IActionResult> GetActivityDashboardStats([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not found." });

            var affiliate = await _context.Users.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
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

        //[HttpGet("dashboard-summary-counts")]
        //public async Task<IActionResult> GetDashboardSummaryCounts()
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    var affiliate = await _context.Users
        //        .FirstOrDefaultAsync(x => x.AspNetUserID == userId);

        //    var referralCode = await _context.WJ_ReferralCodes
        //        .Where(x => x.UserID == affiliate.UserID)
        //        .Select(x => x.ReferralCode)
        //        .FirstOrDefaultAsync();

        //    var instituteIds = await _context.SchoolProfileDetails
        //        .Where(m => m.referralcode == referralCode)
        //        .Select(m => (int?)m.instituteid)
        //        .Distinct()
        //        .ToListAsync();


        //    if (!instituteIds.Any())
        //    {
        //        return Ok(new { success = true, data = new { totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
        //    }

        //    var totalStudents = await _context.Set<ViewWjStudentsDetailsInfo>()
        //        .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

        //    var activeStudents = await _context.StudentActivityLogs
        //        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
        //        .Select(x => x.StudentID)
        //        .Distinct()
        //        .CountAsync();

        //    var totalTeachers = await _context.Set<ViewHfTeacherDetails>()
        //        .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

        //    var activeTeachers = await _context.TeacherActivityLogs
        //        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
        //        .Select(x => x.TeacherID)
        //        .Distinct()
        //        .CountAsync();

        //    var totalSelProjects = await _context.creativityActivitiesSubmission2021s
        //        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
        //        .Select(x => x.InstituteID)
        //        .Distinct()
        //        .CountAsync();

        //    var familyProjects = await _context.StudentActivityLogs
        //        .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Family PBL");

        //    var individualProjects = await _context.StudentActivityLogs
        //        .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Individual PBL");

        //    var groupPbl = await _context.creativityActivitiesSubmission2021s
        //        .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

        //    return Ok(new
        //    {
        //        success = true,
        //        data = new
        //        {
        //            totalStudents,
        //            activeStudents,
        //            totalTeachers,
        //            activeTeachers,
        //            totalSelProjects,
        //            familyProjects,
        //            individualProjects,
        //            groupPbl
        //        }
        //    });
        //}

        [HttpGet("dashboard-summary-counts")]
        public async Task<IActionResult> GetDashboardSummaryCounts([FromQuery] string? affiliateUserId)
        {

            Int32 reportaffiliateid = Convert.ToInt32(affiliateUserId);

            if(reportaffiliateid==0)
            {
              //  var userId = !string.IsNullOrEmpty(affiliateUserId) ? affiliateUserId : User.FindFirstValue(ClaimTypes.NameIdentifier);

                int loggedInUserID = HttpContext.Session.GetInt32("UserID") ?? 0;
                reportaffiliateid = loggedInUserID;

            }





            var affiliate = await _context.Users
                .FirstOrDefaultAsync(x => x.UserID == reportaffiliateid);

            if (affiliate == null)
            {
                return Ok(new { success = true, data = new { totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
            }

            var referralCode = await _context.WJ_ReferralCodes
                .Where(x => x.UserID == affiliate.UserID)
                .Select(x => x.ReferralCode)
                .FirstOrDefaultAsync();

            var instituteIds = await _context.SchoolProfileDetails
                .Where(m => m.referralcode == referralCode)
                .Select(m => (int?)m.instituteid)
                .Distinct()
                .ToListAsync();

            if (!instituteIds.Any())
            {
                return Ok(new { success = true, data = new { totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
            }

            var totalStudents = await _context.Set<ViewWjStudentsDetailsInfo>()
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

            var activeStudents = await _context.StudentActivityLogs
                .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                .Select(x => x.StudentID)
                .Distinct()
                .CountAsync();

            var totalTeachers = await _context.Set<ViewHfTeacherDetails>()
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

            var activeTeachers = await _context.TeacherActivityLogs
                .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                .Select(x => x.TeacherID)
                .Distinct()
                .CountAsync();

            var totalSelProjects = await _context.creativityActivitiesSubmission2021s
                .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                .Select(x => x.InstituteID)
                .Distinct()
                .CountAsync();

            var familyProjects = await _context.StudentActivityLogs
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Family PBL");

            var individualProjects = await _context.StudentActivityLogs
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Individual PBL");

            var groupPbl = await _context.creativityActivitiesSubmission2021s
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

            return Ok(new
            {
                success = true,
                data = new
                {
                    totalStudents,
                    activeStudents,
                    totalTeachers,
                    activeTeachers,
                    totalSelProjects,
                    familyProjects,
                    individualProjects,
                    groupPbl
                }
            });
        }
        [HttpGet("details-list/{type}")]
        public async Task<IActionResult> GetDetailsList(string type)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var volunteer = await _context.Volunteers.FirstOrDefaultAsync(v => v.AspNetUserId == userId && v.IsActive && !v.IsDeleted);

            if (volunteer == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var instituteIds = await _context.VolunteerInstituteMappings
                .Where(m => m.VolunteerId == volunteer.Id && m.IsActive && !m.IsDeleted)
                .Select(m => (int?)m.InstituteId)
                .Distinct()
                .ToListAsync();

            if (!instituteIds.Any())
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            object result = type switch
            {
                "total-students" => await _context.Set<ViewWjStudentsDetailsInfo>()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                "active-students" => await (from log in _context.StudentActivityLogs
                                            join student in _context.Set<ViewWjStudentsDetailsInfo>()
                                            on log.StudentID equals student.StudentId
                                            where log.InstituteID != null && instituteIds.Contains(log.InstituteID)
                                            select student)
                                    .Distinct()
                                    .ToListAsync(),

                "total-teachers" => await _context.Set<ViewHfTeacherDetails>()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                "active-teachers" => await (from log in _context.TeacherActivityLogs
                                            join teacher in _context.Set<ViewHfTeacherDetails>()
                                            on log.TeacherID equals teacher.TeacherID
                                            where log.InstituteID != null && instituteIds.Contains(log.InstituteID)
                                            select teacher)
                                    .Distinct()
                                    .ToListAsync(),

                "sel-projects" => await _context.creativityActivitiesSubmission2021s
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                "family-projects" => await _context.StudentActivityLogs
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Family PBL")
                    .ToListAsync(),

                "individual-projects" => await _context.StudentActivityLogs
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Individual PBL")
                    .ToListAsync(),

                "group-pbl" => await _context.creativityActivitiesSubmission2021s
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                _ => new List<object>()
            };

            return Ok(new { success = true, data = result });
        }

        [HttpGet("dashboard-focused_summary-counts")]
        public async Task<IActionResult> GetFocusedDashboardSummaryCounts([FromQuery] string? affiliateUserId)
        {
            // Agar admin ne dropdown se select kiya hai toh woh use hoga, warna logged-in user
            Int32 reportaffiliateid = Convert.ToInt32(affiliateUserId);

            if (reportaffiliateid == 0)
            {
                //  var userId = !string.IsNullOrEmpty(affiliateUserId) ? affiliateUserId : User.FindFirstValue(ClaimTypes.NameIdentifier);

                int loggedInUserID = HttpContext.Session.GetInt32("UserID") ?? 0;
                reportaffiliateid = loggedInUserID;

            }





            var affiliate = await _context.Users
                .FirstOrDefaultAsync(x => x.UserID == reportaffiliateid);
            if (affiliate == null)
            {
                return Ok(new { success = true, data = new { totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
            }

            var instituteIds = await _context.HfTblAffliateFocusedLists
                .Where(m => m.AffliateId == affiliate.UserID)
                .Select(m => (int?)m.InstituteId)
                .Distinct()
                .ToListAsync();

            if (!instituteIds.Any())
            {
                return Ok(new { success = true, data = new { totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
            }

            var totalStudents = await _context.Set<ViewWjStudentsDetailsInfo>()
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

            var activeStudents = await _context.StudentActivityLogs
                .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                .Select(x => x.StudentID)
                .Distinct()
                .CountAsync();

            var totalTeachers = await _context.Set<ViewHfTeacherDetails>()
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

            var activeTeachers = await _context.TeacherActivityLogs
                .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                .Select(x => x.TeacherID)
                .Distinct()
                .CountAsync();

            var totalSelProjects = await _context.creativityActivitiesSubmission2021s
                .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                .Select(x => x.InstituteID)
                .Distinct()
                .CountAsync();

            var familyProjects = await _context.StudentActivityLogs
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Family PBL");

            var individualProjects = await _context.StudentActivityLogs
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Individual PBL");

            var groupPbl = await _context.creativityActivitiesSubmission2021s
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

            return Ok(new
            {
                success = true,
                data = new
                {
                    totalStudents,
                    activeStudents,
                    totalTeachers,
                    activeTeachers,
                    totalSelProjects,
                    familyProjects,
                    individualProjects,
                    groupPbl
                }
            });
        }

        //[HttpGet("dashboard-focused_summary-counts")]
        //public async Task<IActionResult> GetFocusedDashboardSummaryCounts()
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    var affiliate = await _context.Users
        //        .FirstOrDefaultAsync(x => x.AspNetUserID == userId);

        //    if (affiliate == null)
        //    {
        //        return BadRequest(new { success = false, message = "Affiliate user not found." });
        //    }

        //    var referralCode = await _context.WJ_ReferralCodes
        //        .Where(x => x.UserID == affiliate.UserID)
        //        .Select(x => x.ReferralCode)
        //        .FirstOrDefaultAsync();

        //    if (string.IsNullOrEmpty(referralCode))
        //    {
        //        return Ok(new { success = true, data = new { totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
        //    }

        //    // Match with string type AffliateId
        //    var instituteIds = await _context.HfTblAffliateFocusedLists
        //        .Where(m => m.AffliateId == affiliate.UserID)
        //        .Select(m => (int?)m.InstituteId)
        //        .Distinct()
        //        .ToListAsync();

        //    if (!instituteIds.Any())
        //    {
        //        return Ok(new { success = true, data = new { totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
        //    }

        //    var totalStudents = await _context.Set<ViewWjStudentsDetailsInfo>()
        //        .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

        //    var activeStudents = await _context.StudentActivityLogs
        //        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
        //        .Select(x => x.StudentID)
        //        .Distinct()
        //        .CountAsync();

        //    var totalTeachers = await _context.Set<ViewHfTeacherDetails>()
        //        .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

        //    var activeTeachers = await _context.TeacherActivityLogs
        //        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
        //        .Select(x => x.TeacherID)
        //        .Distinct()
        //        .CountAsync();

        //    var totalSelProjects = await _context.creativityActivitiesSubmission2021s
        //        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
        //        .Select(x => x.InstituteID)
        //        .Distinct()
        //        .CountAsync();

        //    var familyProjects = await _context.StudentActivityLogs
        //        .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Family PBL");

        //    var individualProjects = await _context.StudentActivityLogs
        //        .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Individual PBL");

        //    var groupPbl = await _context.creativityActivitiesSubmission2021s
        //        .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

        //    return Ok(new
        //    {
        //        success = true,
        //        data = new
        //        {
        //            totalStudents,
        //            activeStudents,
        //            totalTeachers,
        //            activeTeachers,
        //            totalSelProjects,
        //            familyProjects,
        //            individualProjects,
        //            groupPbl
        //        }
        //    });
        //}

        [HttpGet("details-list2/{type}")]
        public async Task<IActionResult> GetDetailsList2(string type)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var affiliate = await _context.Users
                .FirstOrDefaultAsync(x => x.AspNetUserID == userId);

            if (affiliate == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var referralCodeStr = await _context.WJ_ReferralCodes
                .Where(x => x.UserID == affiliate.UserID)
                .Select(x => x.ReferralCode)
                .FirstOrDefaultAsync();

            int affiliateIdInt = 0;
            if (!int.TryParse(referralCodeStr, out affiliateIdInt))
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var instituteIds = await _context.HfTblAffliateFocusedLists
                .Where(m => m.AffliateId == affiliateIdInt)
                .Select(m => (int?)m.InstituteId)
                .Distinct()
                .ToListAsync();

            if (!instituteIds.Any())
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            object result = type switch
            {
                "total-students" => await _context.Set<ViewWjStudentsDetailsInfo>()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                "active-students" => await (from log in _context.StudentActivityLogs
                                            join student in _context.Set<ViewWjStudentsDetailsInfo>()
                                            on log.StudentID equals student.StudentId
                                            where log.InstituteID != null && instituteIds.Contains(log.InstituteID)
                                            select student)
                                    .Distinct()
                                    .ToListAsync(),

                "total-teachers" => await _context.Set<ViewHfTeacherDetails>()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                "active-teachers" => await (from log in _context.TeacherActivityLogs
                                            join teacher in _context.Set<ViewHfTeacherDetails>()
                                            on log.TeacherID equals teacher.TeacherID
                                            where log.InstituteID != null && instituteIds.Contains(log.InstituteID)
                                            select teacher)
                                    .Distinct()
                                    .ToListAsync(),

                "sel-projects" => await _context.creativityActivitiesSubmission2021s
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                "family-projects" => await _context.StudentActivityLogs
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Family PBL")
                    .ToListAsync(),

                "individual-projects" => await _context.StudentActivityLogs
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Individual PBL")
                    .ToListAsync(),

                "group-pbl" => await _context.creativityActivitiesSubmission2021s
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                _ => new List<object>()
            };

            return Ok(new { success = true, data = result });
        }

    }
}