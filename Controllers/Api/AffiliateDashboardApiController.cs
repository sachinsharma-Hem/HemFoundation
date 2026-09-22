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

       

        [HttpGet("dashboard-summary-counts")]
        public async Task<IActionResult> GetDashboardSummaryCounts([FromQuery] string? affiliateUserId)
        {

            Int32 reportaffiliateid = Convert.ToInt32(affiliateUserId);

            if(reportaffiliateid==0)
            {

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
                .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType!="Survey")
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
                                            where log.InstituteID != null && instituteIds.Contains(log.InstituteID) && log.ActivityType!="Survey"
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


            int schoolcount = instituteIds.Count;

            var totalStudents = await _context.Set<ViewWjStudentsDetailsInfo>()
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

            var activeStudents = await _context.StudentActivityLogs
                .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType != "Survey") 
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
                    schoolcount,
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

        [HttpGet("incentive-dashboard")]
        public async Task<IActionResult> GetIncentiveDashboardData([FromQuery] int year,[FromQuery] int month)
        {
            try
            {
                if (year < 2000 || month < 1 || month > 12)
                {
                    return BadRequest(new { success = false, message = "Invalid year or month." });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new { success = false, message = "User is not authenticated." });
                }

                var affiliate = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.AspNetUserID == userId);

                if (affiliate == null)
                {
                    return Unauthorized(new { success = false, message = "Affiliate not found." });
                }

                var referralCode = await _context.WJ_ReferralCodes
                    .AsNoTracking()
                    .Where(x => x.UserID == affiliate.UserID)
                    .Select(x => x.ReferralCode)
                    .FirstOrDefaultAsync();

                var schoolsQuery = await _context.SchoolProfileDetails
                    .AsNoTracking()
                    .Where(x => x.referralcode == referralCode && x.instituteid != null)
                    .Select(m => new {
                        InstituteId = (int?)m.instituteid,
                        TotalTeachers = m.totalteachers ?? 0
                    })
                    .ToListAsync();

                var instituteIds = schoolsQuery.Select(x => x.InstituteId).Where(x => x != null).Distinct().ToList();

                if (!instituteIds.Any())
                {
                    return Ok(new { success = true, data = new { activeStudents = 0, studentIncentive = 0m, activeTeachers = 0, teacherIncentive = 0m, activeSchools = 0, schoolIncentive = 0m, totalCertificates = 0, certificateIncentive = 0m, greenProjects = 0, orangeProjects = 0, totalIncentive = 0m, history = new List<object>() } });
                }

                var startDate = new DateTime(year, month, 1, 0, 0, 0);
                var nextMonthDate = startDate.AddMonths(1);

                // 1. Active Students (First Activity in Selected Month)
                var activeStudentCount = await _context.StudentActivityLogs
                    .AsNoTracking()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType != "Survey")
                    .GroupBy(x => x.StudentID)
                    .Select(g => new { StudentID = g.Key, FirstActivityDate = g.Min(x => x.ActivityDate) })
                    .CountAsync(x => x.FirstActivityDate >= startDate && x.FirstActivityDate < nextMonthDate);

                // 2. Active Teachers (First Activity in Selected Month)
                var activeTeacherCount = await _context.TeacherActivityLogs
                    .AsNoTracking()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .GroupBy(x => x.TeacherID)
                    .Select(g => new { TeacherID = g.Key, FirstActivityDate = g.Min(x => x.ActivityDate) })
                    .CountAsync(x => x.FirstActivityDate >= startDate && x.FirstActivityDate < nextMonthDate);

                // 3. Value Certifications Count (Month-wise based on CreatedOn)
                var certificateCount = await _context.ValueCertifications
                    .AsNoTracking()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.CreatedOn >= startDate && x.CreatedOn < nextMonthDate)
                    .CountAsync();

                // 4. Active Schools Calculation
                int activeSchoolCount = 0;
                var students = await _context.Set<ViewWjStudentsDetailsInfo>().AsNoTracking().Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID)).Select(x => new { x.InstituteID, x.StudentId }).ToListAsync();
                var studentLogCounts = await _context.StudentActivityLogs.AsNoTracking().Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID)).GroupBy(x => new { x.InstituteID, x.StudentID }).Select(g => new { g.Key.InstituteID, g.Key.StudentID, Count = g.Count() }).ToListAsync();
                var teacherTotals = await _context.Set<ViewHfTeacherDetails>().AsNoTracking().Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID)).GroupBy(x => x.InstituteID).Select(g => new { InstituteID = g.Key, Count = g.Count() }).ToListAsync();
                var activeTeacherTotals = await _context.TeacherActivityLogs.AsNoTracking().Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID)).Select(x => new { x.InstituteID, x.TeacherID }).Distinct().GroupBy(x => x.InstituteID).Select(g => new { InstituteID = g.Key, Count = g.Count() }).ToListAsync();

                foreach (var instituteIdNullable in instituteIds)
                {
                    if (!instituteIdNullable.HasValue) continue;
                    int schoolId = instituteIdNullable.Value;

                    var schoolStudents = students.Where(x => x.InstituteID == schoolId).Select(x => x.StudentId).ToList();
                    if (!schoolStudents.Any()) continue;

                    var schoolStudentLogs = studentLogCounts.Where(x => x.InstituteID == schoolId).ToList();
                    bool allStudentsCompletedPbl = schoolStudentLogs.Count == schoolStudents.Count && schoolStudentLogs.All(x => x.Count >= 15);

                    var totalTeachersInSchool = teacherTotals.FirstOrDefault(x => x.InstituteID == schoolId)?.Count ?? 0;
                    var activeTeachersInSchool = activeTeacherTotals.FirstOrDefault(x => x.InstituteID == schoolId)?.Count ?? 0;
                    bool allTeachersAreActive = totalTeachersInSchool > 0 && activeTeachersInSchool >= totalTeachersInSchool;

                    if (allStudentsCompletedPbl && allTeachersAreActive)
                    {
                        activeSchoolCount++;
                    }
                }

                // Green & Orange Projects (Placeholder logic, can be adjusted according to your exact table structure)
                int greenProjects = 200;
                int orangeProjects = 300;

                decimal studentIncentive = activeStudentCount * 5m;
                decimal teacherIncentive = activeTeacherCount * 2m;
                decimal schoolIncentive = activeSchoolCount * 100m;
                decimal certificateIncentive = certificateCount * 10m;

                decimal totalIncentive = studentIncentive + teacherIncentive + schoolIncentive + certificateIncentive;

                // Last 3 Months History for Trend Chart
                var historyList = new List<object>();
                for (int i = 2; i >= 0; i--)
                {
                    var hStart = startDate.AddMonths(-i);
                    var hEnd = hStart.AddMonths(1);

                    var hStudents = await _context.StudentActivityLogs
                        .AsNoTracking()
                        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType != "Survey")
                        .GroupBy(x => x.StudentID)
                        .Select(g => new { FirstActivityDate = g.Min(x => x.ActivityDate) })
                        .CountAsync(x => x.FirstActivityDate >= hStart && x.FirstActivityDate < hEnd);

                    var hTeachers = await _context.TeacherActivityLogs
                        .AsNoTracking()
                        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                        .GroupBy(x => x.TeacherID)
                        .Select(g => new { FirstActivityDate = g.Min(x => x.ActivityDate) })
                        .CountAsync(x => x.FirstActivityDate >= hStart && x.FirstActivityDate < hEnd);

                    var hCerts = await _context.ValueCertifications
                        .AsNoTracking()
                        .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.CreatedOn >= hStart && x.CreatedOn < hEnd)
                        .CountAsync();

                    decimal hTotal = (hStudents * 5m) + (hTeachers * 2m) + (hCerts * 10m);

                    historyList.Add(new
                    {
                        monthName = hStart.ToString("MMM yyyy"),
                        activeStudents = hStudents,
                        activeTeachers = hTeachers,
                        totalCertificates = hCerts,
                        totalIncentive = hTotal
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        activeStudents = activeStudentCount,
                        studentIncentive,
                        activeTeachers = activeTeacherCount,
                        teacherIncentive,
                        activeSchools = activeSchoolCount,
                        schoolIncentive,
                        totalCertificates = certificateCount,
                        certificateIncentive,
                        greenProjects,
                        orangeProjects,
                        totalIncentive,
                        history = historyList
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error while loading incentive dashboard.",
                    error = ex.Message
                });
            }
        }
        

    }
}