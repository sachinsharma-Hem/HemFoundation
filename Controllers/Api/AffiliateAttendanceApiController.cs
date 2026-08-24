using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Authorize]
    [Route("api/affiliate-attendance")]
    [ApiController]
    public class AffiliateAttendanceApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AffiliateAttendanceApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper Method: Gets Current Indian Standard Time (IST / UTC+5:30)
        private static DateTime GetIstTime()
        {
            TimeZoneInfo istZone = OperatingSystem.IsWindows()
                ? TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
                : TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
        }

        private async Task<long> GetCurrentAffiliateIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return 0;

            var affiliate = await _context.Affiliates.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
            return affiliate?.UserID ?? 0;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Affiliate not found." });

            var istNow = GetIstTime();
            var todayIST = istNow.Date;

            var attendance = await _context.AffiliateAttendances
                .FirstOrDefaultAsync(x => x.AffiliateId == affId && x.AttendanceDate == todayIST && !x.IsDeleted);

            var lastRecord = await _context.AffiliateAttendances
                .Where(x => x.AffiliateId == affId && !x.IsDeleted)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                success = true,
                isCheckedIn = attendance != null && attendance.Status == "CheckedIn",
                todayRecord = attendance,
                lastRecord = lastRecord
            });
        }

        [HttpPost("punch")]
        public async Task<IActionResult> Punch([FromBody] PunchModel model)
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Affiliate not found." });

            var istNow = GetIstTime();
            var todayIST = istNow.Date;

            var attendance = await _context.AffiliateAttendances
                .FirstOrDefaultAsync(x => x.AffiliateId == affId && x.AttendanceDate == todayIST && !x.IsDeleted);

            // Strict Rule: Ek din me ek hi attendance cycle (In + Out) allow hogi
            if (attendance != null && attendance.Status == "CheckedOut" && attendance.PunchOutTime != null)
            {
                return BadRequest(new { success = false, message = "Aapki aaj ki attendance pehle hi complete ho chuki hai. Ab aap dobara punch in nahi kar sakte." });
            }

            if (model.Action == "In")
            {
                if (attendance != null && attendance.Status == "CheckedIn")
                {
                    return BadRequest(new { success = false, message = "Aap pehle se hi punched in hain." });
                }

                if (attendance == null)
                {
                    attendance = new AffiliateAttendance
                    {
                        AffiliateId = affId,
                        AttendanceDate = todayIST,
                        PunchInTime = istNow,
                        PunchInLocation = model.Location ?? "Current Location",
                        Status = "CheckedIn",
                        PunchOutMode = "User",
                        CreatedDate = istNow
                    };
                    _context.AffiliateAttendances.Add(attendance);
                }
            }
            else if (model.Action == "Out")
            {
                if (attendance == null || attendance.Status == "CheckedOut")
                {
                    return BadRequest(new { success = false, message = "Aap abhi punched in nahi hain." });
                }

                attendance.PunchOutTime = istNow;
                attendance.PunchOutLocation = model.Location ?? "Current Location";
                attendance.Status = "CheckedOut";
                attendance.PunchOutMode = "User";
                attendance.ModifiedDate = istNow;

                if (attendance.PunchInTime.HasValue)
                {
                    var diff = attendance.PunchOutTime.Value - attendance.PunchInTime.Value;
                    attendance.TotalHoursWorked = Math.Round(diff.TotalHours, 2);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, record = attendance });
        }

        // History API with Date-Wise, Month, and Year Filter
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] int? month, [FromQuery] int? year, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Affiliate not found." });

            var query = _context.AffiliateAttendances
                .Where(x => x.AffiliateId == affId && !x.IsDeleted);

            // Date Range Filter
            if (fromDate.HasValue)
                query = query.Where(x => x.AttendanceDate >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(x => x.AttendanceDate <= toDate.Value.Date);

            // Month and Year Filter
            if (month.HasValue && month > 0)
                query = query.Where(x => x.AttendanceDate.Month == month.Value);

            if (year.HasValue && year > 0)
                query = query.Where(x => x.AttendanceDate.Year == year.Value);

            var list = await query.OrderByDescending(x => x.AttendanceDate).ToListAsync();

            return Ok(new { success = true, data = list });
        }

        #region ADMIN ATTENDANCE ENDPOINTS

        // Helper to get IST
        private static DateTime GetIstTimeForAdmin()
        {
            TimeZoneInfo istZone = OperatingSystem.IsWindows()
                ? TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
                : TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
        }

        // 1. Get Dropdown List of Affiliates
        [HttpGet("admin/affiliate-list")]
        public async Task<IActionResult> GetAffiliateList()
        {
            var list = await _context.Affiliates
                .Where(a => a.IsActive)
                .Select(a => new
                {
                    a.UserID,
                    Name = a.FirstName + " " + a.LastName,
                    a.UserCode
                })
                .OrderBy(a => a.Name)
                .ToListAsync();

            return Ok(new { success = true, data = list });
        }

        // 2. Dashboard Top Stats
        [HttpGet("admin/dashboard-stats")]
        public async Task<IActionResult> GetAdminDashboardStats([FromQuery] DateTime? filterDate)
        {
            var targetDate = filterDate?.Date ?? GetIstTimeForAdmin().Date;

            var totalAffiliates = await _context.Affiliates.CountAsync(a => a.IsActive);

            var presentCount = await _context.AffiliateAttendances
                .Where(a => a.AttendanceDate == targetDate && !a.IsDeleted)
                .Select(a => a.AffiliateId)
                .Distinct()
                .CountAsync();

            var absentCount = totalAffiliates - presentCount;

            return Ok(new
            {
                success = true,
                targetDate = targetDate,
                totalAffiliates,
                present = presentCount,
                absent = absentCount < 0 ? 0 : absentCount
            });
        }

        // 3. Detailed Logs & Custom Summary
        [HttpGet("admin/logs")]
        public async Task<IActionResult> GetAdminAttendanceLogs([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] long? affiliateId)
        {
            var query = _context.AffiliateAttendances.Where(a => !a.IsDeleted);

            int totalDaysInRange = 0;

            if (fromDate.HasValue && toDate.HasValue)
            {
                query = query.Where(x => x.AttendanceDate >= fromDate.Value.Date && x.AttendanceDate <= toDate.Value.Date);
                totalDaysInRange = (int)(toDate.Value.Date - fromDate.Value.Date).TotalDays + 1;
            }

            if (affiliateId.HasValue && affiliateId > 0)
            {
                query = query.Where(x => x.AffiliateId == affiliateId.Value);
            }

            var logs = await query
                .Join(_context.Affiliates,
                    att => att.AffiliateId,
                    aff => aff.UserID,
                    (att, aff) => new
                    {
                        att.Id,
                        att.AffiliateId,
                        AffiliateName = aff.FirstName + " " + aff.LastName,
                        aff.UserCode,
                        att.AttendanceDate,
                        att.PunchInTime,
                        att.PunchOutTime,
                        att.PunchInLocation,
                        att.PunchOutLocation,
                        att.TotalHoursWorked,
                        att.Status,
                        att.PunchOutMode
                    })
                .OrderByDescending(x => x.AttendanceDate)
                .ThenByDescending(x => x.PunchInTime)
                .ToListAsync();

            // Grouping for Affiliate-wise Summary with Absent Calculation
            var affiliateWiseSummary = logs
                .GroupBy(x => new { x.AffiliateId, x.AffiliateName, x.UserCode })
                .Select(g =>
                {
                    int presentDays = g.Select(x => x.AttendanceDate).Distinct().Count();
                    int absentDays = totalDaysInRange > 0 ? (totalDaysInRange - presentDays) : 0;

                    return new
                    {
                        g.Key.AffiliateId,
                        g.Key.AffiliateName,
                        g.Key.UserCode,
                        TotalDaysPresent = presentDays,
                        TotalDaysAbsent = absentDays < 0 ? 0 : absentDays, // Failsafe
                        TotalHours = Math.Round(g.Sum(x => x.TotalHoursWorked), 2)
                    };
                })
                .OrderByDescending(x => x.TotalHours)
                .ToList();

            return Ok(new
            {
                success = true,
                detailedLogs = logs,
                summary = affiliateWiseSummary
            });
        }

        [HttpPost("leave/apply")]
        public async Task<IActionResult> ApplyLeave([FromBody] LeaveApplyModel model)
        {
            long affId = await GetCurrentAffiliateIdAsync();

            if (affId == 0)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Affiliate not found."
                });
            }

            if (model == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid leave request."
                });
            }

            if (string.IsNullOrWhiteSpace(model.Reason))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please enter reason for leave."
                });
            }

            var istNow = GetIstTime();

            var todayIST = istNow.Date;

            var fromDate = model.FromDate.Date;

            var toDate = model.ToDate.Date;


            // From Date cannot be before today
            if (fromDate < todayIST)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "From Date cannot be before today."
                });
            }


            // To Date cannot be before today
            if (toDate < todayIST)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "To Date cannot be before today."
                });
            }


            // From cannot be greater than To
            if (fromDate > toDate)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "From Date cannot be greater than To Date."
                });
            }


            /*
               Check existing leave overlap.

               Example:
               Existing: 20 Aug - 22 Aug
               New:      21 Aug - 23 Aug

               This should not be allowed.
            */

            var hasOverlappingLeave =
                await _context.AffiliateLeaves
                    .AnyAsync(x =>
                        x.AffiliateId == affId &&
                        !x.IsDeleted &&
                        x.Status != "Rejected" &&
                        fromDate <= x.ToDate.Date &&
                        toDate >= x.FromDate.Date
                    );


            if (hasOverlappingLeave)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "You already have a leave request for one or more selected dates."
                });
            }


            var leave = new AffiliateLeave
            {
                AffiliateId = affId,

                FromDate = fromDate,

                ToDate = toDate,

                Reason = model.Reason.Trim(),

                Status = "Pending",

                CreatedDate = istNow
            };


            _context.AffiliateLeaves.Add(leave);

            await _context.SaveChangesAsync();


            return Ok(new
            {
                success = true,
                message = "Leave application submitted successfully.",
                data = new
                {
                    leave.Id,
                    leave.FromDate,
                    leave.ToDate,
                    leave.Status
                }
            });
        }

        [HttpGet("leave/history")]
        public async Task<IActionResult> GetLeaveHistory()
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Affiliate not found." });

            var leaves = await _context.AffiliateLeaves
                .Where(x => x.AffiliateId == affId && !x.IsDeleted)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            return Ok(new { success = true, data = leaves });
        }
        #endregion
        // GET: api/affiliate-attendance/my-summary?fromDate=2026-08-01&toDate=2026-08-31
        [HttpGet("my-summary")]
        public async Task<IActionResult> GetMySummary([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Affiliate not found." });

            var start = fromDate?.Date ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var end = toDate?.Date ?? start.AddMonths(1).AddDays(-1);

            int totalRangeDays = (int)(end - start).TotalDays + 1;

            // Fetch Present Days (Unique attendance dates)
            var presentDays = await _context.AffiliateAttendances
                .Where(x => x.AffiliateId == affId && !x.IsDeleted && x.AttendanceDate >= start && x.AttendanceDate <= end)
                .Select(x => x.AttendanceDate.Date)
                .Distinct()
                .CountAsync();

            // Fetch Approved Leaves falling in range
            var approvedLeaves = await _context.AffiliateLeaves
                .Where(x => x.AffiliateId == affId && !x.IsDeleted && x.Status == "Approved" && x.FromDate <= end && x.ToDate >= start)
                .ToListAsync();

            int leaveDays = 0;
            foreach (var leave in approvedLeaves)
            {
                var lStart = leave.FromDate < start ? start : leave.FromDate;
                var lEnd = leave.ToDate > end ? end : leave.ToDate;
                if (lStart <= lEnd)
                {
                    leaveDays += (int)(lEnd - lStart).TotalDays + 1;
                }
            }

            int absentDays = totalRangeDays - presentDays - leaveDays;
            if (absentDays < 0) absentDays = 0;

            // Fetch Affiliate details for Name & ID
            var affiliate = await _context.Affiliates.FirstOrDefaultAsync(a => a.UserID == affId);
            string affName = affiliate != null ? $"{affiliate.FirstName} {affiliate.LastName}" : "Affiliate";
            string affCode = affiliate?.UserCode ?? affId.ToString();

            return Ok(new
            {
                success = true,
                totalDays = totalRangeDays,
                presentCount = presentDays,
                leaveCount = leaveDays,
                absentCount = absentDays,
                affiliateName = affName,
                affiliateCode = affCode
            });
        }

        #region ADMIN LEAVE MANAGEMENT ENDPOINTS

        // 1. Get All Leave Requests with Filters (Status, Affiliate, Date)
        [HttpGet("admin/leave/list")]
        public async Task<IActionResult> GetAdminLeaveList([FromQuery] string? status, [FromQuery] long? affiliateId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var query = _context.AffiliateLeaves
                .Where(x => !x.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(x => x.Status == status);
            }

            if (affiliateId.HasValue && affiliateId > 0)
            {
                query = query.Where(x => x.AffiliateId == affiliateId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(x => x.ToDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(x => x.FromDate <= toDate.Value.Date);
            }

            var list = await query
                .Join(_context.Affiliates,
                    leave => leave.AffiliateId,
                    aff => aff.UserID,
                    (leave, aff) => new
                    {
                        leave.Id,
                        leave.AffiliateId,
                        AffiliateName = aff.FirstName + " " + aff.LastName,
                        aff.UserCode,
                        leave.FromDate,
                        leave.ToDate,
                        leave.Reason,
                        leave.Status,
                        leave.AdminRemarks,
                        leave.CreatedDate,
                        leave.ActionDate
                    })
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            return Ok(new { success = true, data = list });
        }

        // 2. Admin Dashboard Stats for Leaves (Today's requests, Pending, Total Completed)
        [HttpGet("admin/leave/stats")]
        public async Task<IActionResult> GetAdminLeaveStats()
        {
            var today = GetIstTimeForAdmin().Date;

            var totalRequests = await _context.AffiliateLeaves.CountAsync(x => !x.IsDeleted);
            var todayRequests = await _context.AffiliateLeaves.CountAsync(x => !x.IsDeleted && x.CreatedDate.Date == today);
            var pendingRequests = await _context.AffiliateLeaves.CountAsync(x => !x.IsDeleted && x.Status == "Pending");
            var approvedRequests = await _context.AffiliateLeaves.CountAsync(x => !x.IsDeleted && x.Status == "Approved");
            var rejectedRequests = await _context.AffiliateLeaves.CountAsync(x => !x.IsDeleted && x.Status == "Rejected");

            return Ok(new
            {
                success = true,
                total = totalRequests,
                todayNew = todayRequests,
                pending = pendingRequests,
                approved = approvedRequests,
                rejected = rejectedRequests
            });
        }

        // 3. Update Leave Status (Approve / Reject with remarks)
        [HttpPost("admin/leave/update-status")]
        public async Task<IActionResult> UpdateLeaveStatus([FromBody] AdminLeaveUpdateModel model)
        {
            if (model == null || model.LeaveId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid request data." });
            }

            var leave = await _context.AffiliateLeaves.FirstOrDefaultAsync(x => x.Id == model.LeaveId && !x.IsDeleted);
            if (leave == null)
            {
                return NotFound(new { success = false, message = "Leave request not found." });
            }

            if (model.Status != "Approved" && model.Status != "Rejected")
            {
                return BadRequest(new { success = false, message = "Invalid status value." });
            }

            leave.Status = model.Status;
            leave.AdminRemarks = string.IsNullOrWhiteSpace(model.Remarks) ? null : model.Remarks.Trim();
            leave.ActionDate = GetIstTimeForAdmin();
            leave.ModifiedDate = leave.ActionDate;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Leave request has been {model.Status.ToLower()} successfully." });
        }

        #endregion
    }
    public class AdminLeaveUpdateModel
    {
        public long LeaveId { get; set; }
        public string Status { get; set; } = string.Empty; // "Approved" or "Rejected"
        public string? Remarks { get; set; }
    }
    public class PunchModel
    {
        public string Action { get; set; } = "In";
        public string? Location { get; set; }
    }
    public class LeaveApplyModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }


}