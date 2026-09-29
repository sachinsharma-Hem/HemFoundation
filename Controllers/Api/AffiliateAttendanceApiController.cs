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

            var affiliate = await _context.Users.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
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

        [HttpGet("leave/history")]
        public async Task<IActionResult> GetLeaveHistory()
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

            var leaves = await _context.AffiliateLeaves
                .AsNoTracking()
                .Where(x =>
                    x.AffiliateId == affId &&
                    !x.IsDeleted)
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new
                {
                    id = x.Id,
                    fromDate = x.FromDate,
                    toDate = x.ToDate,
                    reason = x.Reason,
                    status = x.Status,
                    adminRemarks = x.AdminRemarks,
                    createdDate = x.CreatedDate,
                    actionDate = x.ActionDate
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = leaves
            });
        }
        

        [HttpPost("leave/apply")]
        public async Task<IActionResult> ApplyLeave(
            [FromBody] LeaveApplyModel model)
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
                    message = "Invalid request data."
                });
            }

            if (model.FromDate.Date < DateTime.Today)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "From Date cannot be before today."
                });
            }

            if (model.ToDate.Date < model.FromDate.Date)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "To Date cannot be before From Date."
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


            // ------------------------------------------------------------
            // Check overlapping leave
            // ------------------------------------------------------------

            var overlappingLeave =
                await _context.AffiliateLeaves
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.AffiliateId == affId &&
                        !x.IsDeleted &&
                        x.Status != "Rejected" &&
                        x.FromDate.Date <= model.ToDate.Date &&
                        x.ToDate.Date >= model.FromDate.Date);

            if (overlappingLeave)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "A leave request already exists for the selected date range."
                });
            }


            // ------------------------------------------------------------
            // Create Leave
            // ------------------------------------------------------------

            var now = GetIstTime();

            var leave = new AffiliateLeave
            {
                AffiliateId = affId,
                FromDate = model.FromDate.Date,
                ToDate = model.ToDate.Date,
                Reason = model.Reason.Trim(),
                Status = "Pending",
                AdminRemarks = null,
                CreatedDate = now,
                ModifiedDate = now,
                ActionDate = null,
                IsDeleted = false
            };


            _context.AffiliateLeaves.Add(leave);

            await _context.SaveChangesAsync();


            return Ok(new
            {
                success = true,
                message = "Leave application submitted successfully.",
                data = new
                {
                    id = leave.Id,
                    fromDate = leave.FromDate,
                    toDate = leave.ToDate,
                    reason = leave.Reason,
                    status = leave.Status,
                    createdDate = leave.CreatedDate
                }
            });
        }
        #region ADMIN ATTENDANCE ENDPOINTS

        private static DateTime GetIstTimeForAdmin()
        {
            TimeZoneInfo istZone = OperatingSystem.IsWindows()
                ? TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
                : TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
        }


        // ============================================================
        // ADMIN - AFFILIATE DROPDOWN
        // ============================================================
        [HttpGet("admin/affiliates-list")]
        public async Task<IActionResult> GetAffiliatesList()
        {
            var list = await _context.View_HF_Affiliate
                .AsNoTracking()
                .Where(x => x.SchoolCount >= 5)
                .Select(x => new
                {
                    AffiliateId = (long)x.InstituteID,
                    AffiliateName =
                        ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim()
                })
                .Where(x => x.AffiliateId > 0)
                .Distinct()
                .OrderBy(x => x.AffiliateName)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = list
            });
        }


        // ============================================================
        // ADMIN - DASHBOARD STATS
        // ============================================================
        [HttpGet("admin/dashboard-stats")]
        public async Task<IActionResult> GetAdminDashboardStats(
            [FromQuery] DateTime? filterDate)
        {
            var targetDate = filterDate?.Date ?? GetIstTimeForAdmin().Date;

            var affiliateIds = await _context.View_HF_Affiliate
                .AsNoTracking()
                .Where(x => x.SchoolCount >= 5)
                .Select(x => (long)x.InstituteID)
                .Distinct()
                .ToListAsync();

            var totalAffiliates = affiliateIds.Count;

            var presentCount = await _context.AffiliateAttendances
                .AsNoTracking()
                .Where(a =>
                    a.AttendanceDate == targetDate &&
                    !a.IsDeleted &&
                    affiliateIds.Contains(a.AffiliateId))
                .Select(a => a.AffiliateId)
                .Distinct()
                .CountAsync();

            var absentCount = totalAffiliates - presentCount;

            return Ok(new
            {
                success = true,
                targetDate = targetDate.ToString("yyyy-MM-dd"),
                totalAffiliates = totalAffiliates,
                present = presentCount,
                absent = absentCount < 0 ? 0 : absentCount
            });
        }


        // ============================================================
        // ADMIN - ATTENDANCE LOGS
        // ============================================================
        [HttpGet("admin/logs")]
        public async Task<IActionResult> GetAdminAttendanceLogs(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] long? affiliateId)
        {
            // --------------------------------------------------------
            // 1. Affiliate master list
            // --------------------------------------------------------
            var affiliateQuery = _context.View_HF_Affiliate
                .AsNoTracking()
                .Where(x => x.SchoolCount >= 5);

            if (affiliateId.HasValue && affiliateId.Value > 0)
            {
                affiliateQuery = affiliateQuery
                    .Where(x => (long)x.InstituteID == affiliateId.Value);
            }

            var affiliates = await affiliateQuery
                .Select(x => new
                {
                    AffiliateId = (long)x.InstituteID,

                    AffiliateName =
                        ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim()
                })
                .Where(x => x.AffiliateId > 0)
                .Distinct()
                .OrderBy(x => x.AffiliateName)
                .ToListAsync();


            // --------------------------------------------------------
            // 2. Attendance query
            // --------------------------------------------------------
            var query = _context.AffiliateAttendances
                .AsNoTracking()
                .Where(a => !a.IsDeleted);


            int totalDaysInRange = 0;

            if (fromDate.HasValue && toDate.HasValue)
            {
                var from = fromDate.Value.Date;
                var to = toDate.Value.Date;

                query = query.Where(x =>
                    x.AttendanceDate >= from &&
                    x.AttendanceDate <= to);

                totalDaysInRange =
                    (int)(to - from).TotalDays + 1;
            }
            else if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;

                query = query.Where(x =>
                    x.AttendanceDate >= from);
            }
            else if (toDate.HasValue)
            {
                var to = toDate.Value.Date;

                query = query.Where(x =>
                    x.AttendanceDate <= to);
            }


            if (affiliateId.HasValue && affiliateId.Value > 0)
            {
                query = query.Where(x =>
                    x.AffiliateId == affiliateId.Value);
            }


            // --------------------------------------------------------
            // 3. Attendance logs
            // --------------------------------------------------------
            var attendanceLogs = await query
                .Select(att => new
                {
                    att.Id,
                    att.AffiliateId,
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


            // --------------------------------------------------------
            // 4. Attach affiliate name + code
            // --------------------------------------------------------
            var userIds = attendanceLogs
     .Select(x => x.AffiliateId)
     .Distinct()
     .Where(x => x <= int.MaxValue)
     .Select(x => (int)x)
     .ToList();

            var users = await _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.UserID))
                .Select(u => new
                {
                    u.UserID,
                    u.FirstName,
                    u.LastName,
                    u.UserCode
                })
                .ToListAsync();


            var userDictionary = users.ToDictionary(
     x => (long)x.UserID,
     x => new
     {
         Name = $"{x.FirstName} {x.LastName}".Trim(),
         Code = x.UserCode
     });


            // --------------------------------------------------------
            // 5. Detailed logs with names
            // --------------------------------------------------------
            var logs = attendanceLogs
                .Select(att =>
                {
                    userDictionary.TryGetValue(
                        att.AffiliateId,
                        out var user);

                    return new
                    {
                        att.Id,
                        att.AffiliateId,

                        AffiliateName =
                            user?.Name ?? "Unknown Affiliate",

                        UserCode =
                            user?.Code ?? "N/A",

                        att.AttendanceDate,
                        att.PunchInTime,
                        att.PunchOutTime,
                        att.PunchInLocation,
                        att.PunchOutLocation,
                        att.TotalHoursWorked,
                        att.Status,
                        att.PunchOutMode
                    };
                })
                .ToList();


            // --------------------------------------------------------
            // 6. Summary
            // --------------------------------------------------------
            var affiliateWiseSummary = affiliates
                .Select(affiliate =>
                {
                    var userLogs = logs
                        .Where(l =>
                            l.AffiliateId == affiliate.AffiliateId)
                        .ToList();


                    int presentDays = userLogs
                        .Select(x => x.AttendanceDate.Date)
                        .Distinct()
                        .Count();


                    int absentDays =
                        totalDaysInRange > 0
                            ? totalDaysInRange - presentDays
                            : 0;


                    var matchedUser = users
                        .FirstOrDefault(u =>
                            u.UserID == affiliate.AffiliateId);


                    return new
                    {
                        AffiliateId = affiliate.AffiliateId,

                        AffiliateName =
                            !string.IsNullOrWhiteSpace(affiliate.AffiliateName)
                                ? affiliate.AffiliateName
                                : "Unknown Affiliate",

                        UserCode =
                            matchedUser?.UserCode ?? "N/A",

                        TotalDaysPresent = presentDays,

                        TotalDaysAbsent =
                            absentDays < 0 ? 0 : absentDays,

                        TotalHours = Math.Round(
                            userLogs.Sum(x =>
                                x.TotalHoursWorked),
                            2)
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

        #endregion

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
            var affiliate = await _context.Users.FirstOrDefaultAsync(a => a.UserID == affId);
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
                .Join(_context.Users,
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
        public string Status { get; set; } = string.Empty; 
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