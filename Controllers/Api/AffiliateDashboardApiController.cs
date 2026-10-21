using HemFoundation.Data;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Humanizer.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Configuration;
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

        private readonly IConfiguration _configuration;

        public AffiliateDashboardApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;

            _configuration = configuration;
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

        //[HttpGet("associated-schools-details")]
        //public async Task<IActionResult> GetAssociatedSchoolsDetails()
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    var affiliate = await _context.Users
        //        .AsNoTracking()
        //        .FirstOrDefaultAsync(x => x.AspNetUserID == userId);

        //    if (affiliate == null)
        //        return Unauthorized(new
        //        {
        //            success = false,
        //            message = "Affiliate not found."
        //        });

        //    var referralCode = await _context.WJ_ReferralCodes
        //        .AsNoTracking()
        //        .Where(x => x.UserID == affiliate.UserID)
        //        .Select(x => x.ReferralCode)
        //        .FirstOrDefaultAsync();

        //    if (string.IsNullOrWhiteSpace(referralCode))
        //    {
        //        return Ok(new
        //        {
        //            success = true,
        //            data = new List<object>()
        //        });
        //    }

        //    var assignedSchools = await _context.SchoolProfileDetails
        //        .AsNoTracking()
        //        .Where(x => x.referralcode == referralCode)
        //        .OrderBy(x => x.institute_name)
        //        .Select(x => new
        //        {
        //            instituteID = x.instituteid,
        //            instituteType = x.institutetype,
        //            name = x.institute_name,
        //            profileUrl = x.profileurl,
        //            instituteIsActive = x.institute_isactive,
        //            referralCode = x.referralcode,
        //            isVerify = x.isverify,

        //            firstName = x.firstname,
        //            lastName = x.lastname,
        //            mobileNo = x.mobileno,
        //            emailId = x.emailid,

        //            addressLine1 = x.addressline1,
        //            addressLine2 = x.addressline2,
        //            addressLine3 = x.addressline3,
        //            city = x.cityname,
        //            pincode = x.pincode,

        //            photoUrl = x.photourl,

        //            userIsActive = x.user_isactive,

        //            createdBy = x.createdby,
        //            createdDate = x.createddate,
        //            modifiedBy = x.modifiedby,
        //            modifiedDate = x.modifieddate,

        //            aspNetUserId = x.aspnetuserid,

        //            associatedStudents = x.associatedstudents,
        //            activeStudents = x.activestudents,
        //            totalTeachers = x.totalteachers,
        //            activeTeachers = x.activeteachers
        //        })
        //        .ToListAsync();

        //    return Ok(new
        //    {
        //        success = true,
        //        data = assignedSchools
        //    });
        //}

        [HttpGet("associated-schools-details")]
        public async Task<IActionResult> GetAssociatedSchoolsDetails()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var affiliate = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AspNetUserID == userId);

            if (affiliate == null)
                return Unauthorized(new
                {
                    success = false,
                    message = "Affiliate not found."
                });

            var referralCode = await _context.WJ_ReferralCodes
                .AsNoTracking()
                .Where(x => x.UserID == affiliate.UserID)
                .Select(x => x.ReferralCode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(referralCode))
            {
                return Ok(new
                {
                    success = true,
                    data = new List<object>()
                });
            }

            var assignedSchools = await _context.SchoolProfileDetails
                .AsNoTracking()
                .Where(x => x.referralcode == referralCode)
                .OrderBy(x => x.institute_name)
                .Select(x => new
                {
                    instituteID = x.instituteid,
                    instituteType = x.institutetype,
                    name = x.institute_name,
                    profileUrl = x.profileurl,
                    instituteIsActive = x.institute_isactive,
                    referralCode = x.referralcode,
                    isVerify = x.isverify,

                    firstName = x.firstname,
                    lastName = x.lastname,
                    mobileNo = x.mobileno,
                    emailId = x.emailid,

                    addressLine1 = x.addressline1,
                    addressLine2 = x.addressline2,
                    addressLine3 = x.addressline3,
                    city = x.cityname,
                    pincode = x.pincode,

                    photoUrl = x.photourl,

                    userIsActive = x.user_isactive,

                    createdBy = x.createdby,
                    createdDate = x.createddate,
                    modifiedBy = x.modifiedby,
                    modifiedDate = x.modifieddate,

                    aspNetUserId = x.aspnetuserid,

                    associatedStudents = x.associatedstudents,
                    activeStudents = x.activestudents,
                    totalTeachers = x.totalteachers,
                    activeTeachers = x.activeteachers
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
            try
            {
                Int32 reportaffiliateid = Convert.ToInt32(affiliateUserId);

                if (reportaffiliateid == 0)
                {
                    int loggedInUserID = HttpContext.Session.GetInt32("UserID") ?? 0;
                    reportaffiliateid = loggedInUserID;
                }

                string connString = _configuration.GetConnectionString("DefaultConnection");

                int totalinstitute = 0;
                int totalStudents = 0;
                int activeStudents = 0;
                int totalTeachers = 0;
                int activeTeachers = 0;
                int totalSelProjects = 0;
                int familyProjects = 0;
                int individualProjects = 0;
                int groupPbl = 0;

                using (var conn = new Npgsql.NpgsqlConnection(connString))
                {
                    await conn.OpenAsync();

                    string summarySql = "SELECT schoolcount, totalstudents, activestudents, totalteachers, activeteachers, totalselprojects, familyprojects, individualprojects, grouppbl FROM public.get_affiliate_dashboardupdated_summary(@AffiliateId)";

                    using (var cmd = new Npgsql.NpgsqlCommand(summarySql, conn))
                    {
                        cmd.Parameters.AddWithValue("AffiliateId", reportaffiliateid);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            // Read the single returned row from the stored procedure
                            if (await reader.ReadAsync())
                            {
                                totalinstitute = reader.GetInt32(0);
                                totalStudents = reader.GetInt32(1);
                                activeStudents = reader.GetInt32(2);
                                totalTeachers = reader.GetInt32(3);
                                activeTeachers = reader.GetInt32(4);
                                totalSelProjects = reader.GetInt32(5);
                                familyProjects = reader.GetInt32(6);
                                individualProjects = reader.GetInt32(7);
                                groupPbl = reader.GetInt32(8);
                            }
                            else
                            {
                                // If no row returned (e.g. invalid affiliate ID), return zeroes
                                return Ok(new { success = true, data = new { totalinstitute = 0, totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
                            }
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalinstitute,
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
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading dashboard summary counts.",
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        [HttpGet("dashboard-summary-counts1")]
        public async Task<IActionResult> GetDashboardSummaryCounts1([FromQuery] string? affiliateUserId)
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
            var totalinstitute = instituteIds.Count;
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
                    totalinstitute,
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

        [HttpGet("dashboard-focused_summary-counts1")]
        public async Task<IActionResult> GetFocusedDashboardSummaryCounts1([FromQuery] string? affiliateUserId)
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
        [HttpGet("dashboard-focused_summary-counts")]
        public async Task<IActionResult> GetFocusedDashboardSummaryCounts([FromQuery] string? affiliateUserId)
        {
            try
            {
                Int32 reportaffiliateid = Convert.ToInt32(affiliateUserId);

                if (reportaffiliateid == 0)
                {
                    int loggedInUserID = HttpContext.Session.GetInt32("UserID") ?? 0;
                    reportaffiliateid = loggedInUserID;
                }

                string connString = _configuration.GetConnectionString("DefaultConnection");

                int schoolCount = 0;
                int totalStudents = 0;
                int activeStudents = 0;
                int totalTeachers = 0;
                int activeTeachers = 0;
                int totalSelProjects = 0;
                int familyProjects = 0;
                int individualProjects = 0;
                int groupPbl = 0;

                using (var conn = new Npgsql.NpgsqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // Call the optimized PostgreSQL stored procedure
                    string summarySql = "SELECT schoolcount, totalstudents, activestudents, totalteachers, activeteachers, totalselprojects, familyprojects, individualprojects, grouppbl FROM public.get_affiliate_foccuseddashboardupdated_summary(@AffiliateId)";

                    using (var cmd = new Npgsql.NpgsqlCommand(summarySql, conn))
                    {
                        cmd.Parameters.AddWithValue("AffiliateId", reportaffiliateid);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                schoolCount = reader.GetInt32(0);
                                totalStudents = reader.GetInt32(1);
                                activeStudents = reader.GetInt32(2);
                                totalTeachers = reader.GetInt32(3);
                                activeTeachers = reader.GetInt32(4);
                                totalSelProjects = reader.GetInt32(5);
                                familyProjects = reader.GetInt32(6);
                                individualProjects = reader.GetInt32(7);
                                groupPbl = reader.GetInt32(8);
                            }
                            else
                            {
                                // If affiliate/user has no data or doesn't exist
                                return Ok(new { success = true, data = new { schoolcount = 0, totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
                            }
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        schoolcount = schoolCount,
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
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading focused summary counts.",
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
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
        public async Task<IActionResult> GetIncentiveDashboardData(
       [FromQuery] int year,
       [FromQuery] int month)
        {
            try
            {
                if (year < 2000 || month < 1 || month > 12)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid year or month."
                    });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User is not authenticated."
                    });
                }

                var result = await _context.Database
                    .SqlQueryRaw<string>(
                        """
                SELECT public.get_incentive_dashboard_fast(
                    {0},
                    {1},
                    {2}
                )::text AS "Value"
                """,
                        userId,
                        year,
                        month
                    )
                    .SingleAsync();

                return Content(
                    result,
                    "application/json"
                );
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