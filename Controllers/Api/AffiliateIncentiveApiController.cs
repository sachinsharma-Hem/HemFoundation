using HemFoundation.Data;
using HemFoundation.Services.AffiliateIncentive;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace HemFoundation.Controllers.Api
{
    /// <summary>
    /// Affiliate incentive summaries: an affiliate's own lifetime / month-wise
    /// figures, and the admin report across all affiliates. Uses the same
    /// login (JWT bearer) as the existing affiliate dashboard API.
    /// </summary>
    [Authorize]
    [Route("api/affiliate-incentive")]
    [ApiController]
    public class AffiliateIncentiveApiController : ControllerBase
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly AffiliateIncentiveService _incentives;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AffiliateIncentiveApiController> _logger;

        public AffiliateIncentiveApiController(
            AffiliateIncentiveService incentives,
            ApplicationDbContext db,
            ILogger<AffiliateIncentiveApiController> logger)
        {
            _incentives = incentives;
            _db = db;
            _logger = logger;
        }

        private IActionResult Fail(int status, string message) =>
            StatusCode(status, new { success = false, message });

        private static string MonthLabel(int year, int month) =>
            new DateTime(year, month, 1).ToString("MMMM yyyy", Inv);

        private static string Status(MonthlyIncentive m, int currentKey) =>
            m.Key == currentKey ? "In progress" : "Closed";

        private static object MonthDto(MonthlyIncentive m, int currentKey) => new
        {
            year = m.Year,
            month = m.Month,
            key = $"{m.Year:0000}-{m.Month:00}",
            label = MonthLabel(m.Year, m.Month),
            activeStudents = m.ActiveStudents,
            studentIncentive = m.StudentIncentive,
            activeSchools = m.ActiveSchools,
            schoolIncentive = m.SchoolIncentive,
            activeTeachers = m.ActiveTeachers,
            teacherIncentive = m.TeacherIncentive,
            totalCertificates = m.TotalCertificates,
            certificateIncentive = m.CertificateIncentive,
            evaluatedProjects = m.EvaluatedProjects,
            projectCounts = new { green = m.Green, orange = m.Orange, blue = m.Blue, yellow = m.Yellow, red = m.Red },
            projectIncentive = m.ProjectIncentive,
            totalIncentive = m.TotalIncentive,
            status = Status(m, currentKey),
            isCurrent = m.Key == currentKey
        };

        /// <summary>Drops the empty months before an affiliate's first earning.</summary>
        private static List<MonthlyIncentive> TrimLeadingEmpty(List<MonthlyIncentive> months)
        {
            int first = months.FindIndex(m => m.TotalIncentive != 0);
            if (first < 0) first = Math.Max(0, months.Count - 2);       // nothing earned yet: keep last two
            first = Math.Min(first, Math.Max(0, months.Count - 2));     // always keep current + previous
            return months.Skip(first).ToList();
        }

        private static object SummaryDto(List<MonthlyIncentive> months, DateTime now)
        {
            int currentKey = now.Year * 100 + now.Month;
            var previousDate = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            int previousKey = previousDate.Year * 100 + previousDate.Month;

            var current = months.FirstOrDefault(m => m.Key == currentKey);
            var previous = months.FirstOrDefault(m => m.Key == previousKey);
            var earning = months.Where(m => m.TotalIncentive != 0).ToList();
            var best = earning.OrderByDescending(m => m.TotalIncentive).FirstOrDefault();

            return new
            {
                lifetimeIncentive = months.Sum(m => m.TotalIncentive),
                currentMonthIncentive = current?.TotalIncentive ?? 0,
                currentMonthLabel = MonthLabel(now.Year, now.Month),
                previousMonthIncentive = previous?.TotalIncentive ?? 0,
                previousMonthLabel = MonthLabel(previousDate.Year, previousDate.Month),
                thisYearIncentive = months.Where(m => m.Year == now.Year).Sum(m => m.TotalIncentive),
                thisYear = now.Year,
                monthsCounted = months.Count,
                earningMonths = earning.Count,
                firstMonthLabel = months.Count > 0 ? MonthLabel(months[0].Year, months[0].Month) : null,
                bestMonthLabel = best == null ? null : MonthLabel(best.Year, best.Month),
                bestMonthIncentive = best?.TotalIncentive ?? 0
            };
        }

        // =====================================================================
        // AFFILIATE: my incentives (one request feeds the whole page)
        // =====================================================================

        [HttpGet("my")]
        public async Task<IActionResult> GetMine([FromQuery] bool refresh = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Fail(401, "User is not authenticated.");

            try
            {
                var timer = Stopwatch.StartNew();

                var affiliate = await _incentives.FindByAspNetUserIdAsync(userId);
                if (affiliate == null) return Fail(404, "Affiliate profile not found.");

                var history = await _incentives.GetHistoryAsync(new[] { affiliate }, refresh);
                var months = TrimLeadingEmpty(history[affiliate.UserId]);

                var now = AffiliateIncentiveService.Now;
                int currentKey = now.Year * 100 + now.Month;

                return Ok(new
                {
                    success = true,
                    affiliate = new { affiliate.UserId, affiliate.Name, affiliate.Code },
                    summary = SummaryDto(months, now),
                    months = months.OrderByDescending(m => m.Key).Select(m => MonthDto(m, currentKey)),
                    rates = new
                    {
                        student = 5,
                        teacher = 2,
                        certificate = 10,
                        school = await _incentives.GetSchoolRateAsync(),
                        green = AffiliateIncentiveService.GreenRate,
                        orange = AffiliateIncentiveService.OrangeRate,
                        blue = AffiliateIncentiveService.BlueRate,
                        yellow = AffiliateIncentiveService.YellowRate,
                        red = AffiliateIncentiveService.RedRate
                    },
                    diagnostics = new { databaseCalls = _incentives.DatabaseCalls, functionCalls = _incentives.FunctionCalls, rangeCalls = _incentives.RangeCalls, elapsedMs = timer.ElapsedMilliseconds }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Affiliate incentive: loading own summary failed");
                return Fail(500, "Error while loading your incentive details. Please try again.");
            }
        }

        // =====================================================================
        // ADMIN: all affiliates
        // =====================================================================

        private class ReportData
        {
            public List<AffiliateInfo> Affiliates = new();
            public Dictionary<int, List<MonthlyIncentive>> History = new();
            public DateTime Now;
            public int CurrentKey;
            public int PreviousKey;
        }

        private async Task<ReportData> LoadAllAsync(bool refresh)
        {
            var affiliates = await _incentives.ListAffiliatesAsync();
            var history = await _incentives.GetHistoryAsync(affiliates, refresh);

            foreach (var affiliate in affiliates)
                history[affiliate.UserId] = TrimLeadingEmpty(history[affiliate.UserId]);

            var now = AffiliateIncentiveService.Now;
            var previous = new DateTime(now.Year, now.Month, 1).AddMonths(-1);

            return new ReportData
            {
                Affiliates = affiliates,
                History = history,
                Now = now,
                CurrentKey = now.Year * 100 + now.Month,
                PreviousKey = previous.Year * 100 + previous.Month
            };
        }

        private class ReportFilter
        {
            public string? Search { get; set; }
            public int? Year { get; set; }
            public int? Month { get; set; }
            public string? From { get; set; }   // yyyy-MM
            public string? To { get; set; }     // yyyy-MM
            public string? Status { get; set; } // "In progress" | "Closed"
            public bool IncludeZero { get; set; }
        }

        private static int? ParseMonthKey(string? value) =>
            DateTime.TryParseExact(value, "yyyy-MM", Inv, DateTimeStyles.None, out var d)
                ? d.Year * 100 + d.Month
                : null;

        private static bool MatchesSearch(AffiliateInfo a, string? search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            search = search.Trim();

            return a.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                   || a.UserId.ToString() == search
                   || (a.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (a.Mobile?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (a.ReferralCode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private static bool MatchesPeriod(MonthlyIncentive m, ReportFilter f, int currentKey)
        {
            if (f.Year is > 0 && m.Year != f.Year) return false;
            if (f.Month is >= 1 and <= 12 && m.Month != f.Month) return false;
            if (ParseMonthKey(f.From) is int from && m.Key < from) return false;
            if (ParseMonthKey(f.To) is int to && m.Key > to) return false;
            if (!string.IsNullOrWhiteSpace(f.Status) &&
                !Status(m, currentKey).Equals(f.Status.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static bool HasPeriodFilter(ReportFilter f) =>
            f.Year is > 0 || f.Month is >= 1 and <= 12 || ParseMonthKey(f.From) != null ||
            ParseMonthKey(f.To) != null || !string.IsNullOrWhiteSpace(f.Status);

        private class AffiliateRow
        {
            public AffiliateInfo Affiliate = new();
            public decimal Current, Previous, Lifetime, Period;
            public int EarningMonths, Students, Schools, Teachers, Certificates, Projects;
            public DateTime? LastUpdated;
        }

        private static List<AffiliateRow> BuildAffiliateRows(ReportData data, ReportFilter filter)
        {
            return data.Affiliates
                .Where(a => MatchesSearch(a, filter.Search))
                .Select(a =>
                {
                    var months = data.History[a.UserId];
                    var inPeriod = months.Where(m => MatchesPeriod(m, filter, data.CurrentKey)).ToList();

                    return new AffiliateRow
                    {
                        Affiliate = a,
                        Current = months.FirstOrDefault(m => m.Key == data.CurrentKey)?.TotalIncentive ?? 0,
                        Previous = months.FirstOrDefault(m => m.Key == data.PreviousKey)?.TotalIncentive ?? 0,
                        Lifetime = months.Sum(m => m.TotalIncentive),
                        Period = inPeriod.Sum(m => m.TotalIncentive),
                        EarningMonths = months.Count(m => m.TotalIncentive != 0),
                        // Eligible figures for the selected period (unique people
                        // are not summed across months: the highest month is shown).
                        Students = inPeriod.Count == 0 ? 0 : inPeriod.Max(m => m.ActiveStudents),
                        Schools = inPeriod.Count == 0 ? 0 : inPeriod.Max(m => m.ActiveSchools),
                        Teachers = inPeriod.Count == 0 ? 0 : inPeriod.Max(m => m.ActiveTeachers),
                        Certificates = inPeriod.Sum(m => m.TotalCertificates),
                        Projects = inPeriod.Sum(m => m.EvaluatedProjects),
                        LastUpdated = months.Where(m => m.ComputedAt != default)
                            .Select(m => (DateTime?)m.ComputedAt).Max()
                    };
                })
                .ToList();
        }

        private static List<(AffiliateInfo Affiliate, MonthlyIncentive Month)> BuildMonthlyRows(
            ReportData data, ReportFilter filter)
        {
            return data.Affiliates
                .Where(a => MatchesSearch(a, filter.Search))
                .SelectMany(a => data.History[a.UserId]
                    .Where(m => MatchesPeriod(m, filter, data.CurrentKey))
                    .Where(m => filter.IncludeZero || m.TotalIncentive != 0)
                    .Select(m => (a, m)))
                .ToList();
        }

        private static IEnumerable<AffiliateRow> Sort(List<AffiliateRow> rows, string? sort, bool descending)
        {
            Func<AffiliateRow, object> key = (sort ?? "").ToLowerInvariant() switch
            {
                "name" => r => r.Affiliate.Name,
                "id" => r => r.Affiliate.UserId,
                "current" => r => r.Current,
                "previous" => r => r.Previous,
                "period" => r => r.Period,
                "months" => r => r.EarningMonths,
                _ => r => r.Lifetime
            };

            if (string.IsNullOrEmpty(sort)) descending = true;   // default: highest lifetime first

            return descending
                ? rows.OrderByDescending(key).ThenBy(r => r.Affiliate.Name)
                : rows.OrderBy(key).ThenBy(r => r.Affiliate.Name);
        }

        private static IEnumerable<(AffiliateInfo Affiliate, MonthlyIncentive Month)> Sort(
            List<(AffiliateInfo Affiliate, MonthlyIncentive Month)> rows, string? sort, bool descending)
        {
            Func<(AffiliateInfo Affiliate, MonthlyIncentive Month), object> key = (sort ?? "").ToLowerInvariant() switch
            {
                "name" => r => r.Affiliate.Name,
                "id" => r => r.Affiliate.UserId,
                "incentive" => r => r.Month.TotalIncentive,
                "students" => r => r.Month.ActiveStudents,
                _ => r => r.Month.Key
            };

            if (string.IsNullOrEmpty(sort)) descending = true;   // default: newest month first

            return descending
                ? rows.OrderByDescending(key).ThenBy(r => r.Affiliate.Name).ThenByDescending(r => r.Month.Key)
                : rows.OrderBy(key).ThenBy(r => r.Affiliate.Name).ThenByDescending(r => r.Month.Key);
        }

        private static object AffiliateRowDto(AffiliateRow r) => new
        {
            userId = r.Affiliate.UserId,
            name = string.IsNullOrWhiteSpace(r.Affiliate.Name) ? $"Affiliate {r.Affiliate.UserId}" : r.Affiliate.Name,
            code = r.Affiliate.Code,
            mobile = r.Affiliate.Mobile,
            referralCode = r.Affiliate.ReferralCode,
            currentMonthIncentive = r.Current,
            previousMonthIncentive = r.Previous,
            lifetimeIncentive = r.Lifetime,
            periodIncentive = r.Period,
            earningMonths = r.EarningMonths,
            students = r.Students,
            schools = r.Schools,
            teachers = r.Teachers,
            certificates = r.Certificates,
            projects = r.Projects,
            lastUpdated = r.LastUpdated?.ToString("dd MMM yyyy, hh:mm tt", Inv)
        };

        private static object MonthlyRowDto((AffiliateInfo Affiliate, MonthlyIncentive Month) r, int currentKey) => new
        {
            userId = r.Affiliate.UserId,
            name = string.IsNullOrWhiteSpace(r.Affiliate.Name) ? $"Affiliate {r.Affiliate.UserId}" : r.Affiliate.Name,
            code = r.Affiliate.Code,
            month = new DateTime(r.Month.Year, r.Month.Month, 1).ToString("MMMM", Inv),
            year = r.Month.Year,
            students = r.Month.ActiveStudents,
            schools = r.Month.ActiveSchools,
            teachers = r.Month.ActiveTeachers,
            certificates = r.Month.TotalCertificates,
            projects = r.Month.EvaluatedProjects,
            incentive = r.Month.TotalIncentive,
            status = Status(r.Month, currentKey),
            lastUpdated = r.Month.ComputedAt == default ? null : r.Month.ComputedAt.ToString("dd MMM yyyy, hh:mm tt", Inv)
        };

        /// <summary>
        /// Summary cards plus one page of the report, in a single request.
        /// view = "affiliates" (one row per affiliate) or "monthly"
        /// (one row per affiliate and month).
        /// </summary>
        [HttpGet("admin/report")]
        public async Task<IActionResult> GetReport(
            [FromQuery] string? view,
            [FromQuery] string? search,
            [FromQuery] int? year,
            [FromQuery] int? month,
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] string? status,
            [FromQuery] bool includeZero = false,
            [FromQuery] string? sort = null,
            [FromQuery] string? dir = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool refresh = false)
        {
            if (!await AffiliateIncentiveAccess.IsAdminAsync(HttpContext, _db))
                return Fail(403, "You do not have permission to view the incentive report.");

            try
            {
                var timer = Stopwatch.StartNew();
                var data = await LoadAllAsync(refresh);

                var filter = new ReportFilter
                {
                    Search = search, Year = year, Month = month, From = from, To = to,
                    Status = status, IncludeZero = includeZero
                };

                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 5, 100);
                bool descending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
                bool monthly = string.Equals(view, "monthly", StringComparison.OrdinalIgnoreCase);

                // ---- Summary cards: always across all affiliates ----
                var all = data.History.Values.SelectMany(m => m).ToList();
                var thisMonth = all.Where(m => m.Key == data.CurrentKey).ToList();

                var overview = new
                {
                    totalAffiliates = data.Affiliates.Count,
                    earningAffiliates = data.History.Count(h => h.Value.Any(m => m.TotalIncentive != 0)),
                    lifetimeIncentive = all.Sum(m => m.TotalIncentive),
                    currentMonthIncentive = thisMonth.Sum(m => m.TotalIncentive),
                    currentMonthLabel = MonthLabel(data.Now.Year, data.Now.Month),
                    previousMonthIncentive = all.Where(m => m.Key == data.PreviousKey).Sum(m => m.TotalIncentive),
                    previousMonthLabel = MonthLabel(data.PreviousKey / 100, data.PreviousKey % 100),
                    eligible = new
                    {
                        students = thisMonth.Sum(m => m.ActiveStudents),
                        schools = thisMonth.Sum(m => m.ActiveSchools),
                        teachers = thisMonth.Sum(m => m.ActiveTeachers),
                        certificates = thisMonth.Sum(m => m.TotalCertificates),
                        projects = thisMonth.Sum(m => m.EvaluatedProjects)
                    },
                    years = all.Select(m => m.Year).Distinct().OrderByDescending(y => y)
                };

                int total;
                decimal filteredIncentive;
                IEnumerable<object> items;

                if (monthly)
                {
                    var rows = BuildMonthlyRows(data, filter);
                    total = rows.Count;
                    filteredIncentive = rows.Sum(r => r.Month.TotalIncentive);
                    items = Sort(rows, sort, descending)
                        .Skip((page - 1) * pageSize).Take(pageSize)
                        .Select(r => MonthlyRowDto(r, data.CurrentKey));
                }
                else
                {
                    var rows = BuildAffiliateRows(data, filter);
                    total = rows.Count;
                    filteredIncentive = rows.Sum(r => HasPeriodFilter(filter) ? r.Period : r.Lifetime);
                    items = Sort(rows, sort, descending)
                        .Skip((page - 1) * pageSize).Take(pageSize)
                        .Select(AffiliateRowDto);
                }

                return Ok(new
                {
                    success = true,
                    overview,
                    view = monthly ? "monthly" : "affiliates",
                    periodFiltered = HasPeriodFilter(filter),
                    filteredIncentive,
                    total,
                    page,
                    pageSize,
                    items = items.ToList(),
                    diagnostics = new { databaseCalls = _incentives.DatabaseCalls, functionCalls = _incentives.FunctionCalls, rangeCalls = _incentives.RangeCalls, elapsedMs = timer.ElapsedMilliseconds }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Affiliate incentive: admin report failed");
                return Fail(500, "Error while loading the incentive report. Please try again.");
            }
        }

        [HttpGet("admin/affiliates/{userId:int}")]
        public async Task<IActionResult> GetAffiliateDetail(int userId)
        {
            if (!await AffiliateIncentiveAccess.IsAdminAsync(HttpContext, _db))
                return Fail(403, "You do not have permission to view the incentive report.");

            try
            {
                var affiliate = (await _incentives.ListAffiliatesAsync()).FirstOrDefault(a => a.UserId == userId);
                if (affiliate == null) return Fail(404, "Affiliate not found.");

                var history = await _incentives.GetHistoryAsync(new[] { affiliate });
                var months = TrimLeadingEmpty(history[affiliate.UserId]);

                var now = AffiliateIncentiveService.Now;
                int currentKey = now.Year * 100 + now.Month;

                return Ok(new
                {
                    success = true,
                    affiliate = new
                    {
                        userId = affiliate.UserId,
                        name = affiliate.Name,
                        code = affiliate.Code,
                        mobile = affiliate.Mobile,
                        referralCode = affiliate.ReferralCode,
                        joined = affiliate.CreatedDate.ToString("dd MMM yyyy", Inv)
                    },
                    summary = SummaryDto(months, now),
                    months = months.OrderByDescending(m => m.Key).Select(m => MonthDto(m, currentKey))
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Affiliate incentive: detail for {UserId} failed", userId);
                return Fail(500, "Error while loading the affiliate's incentive history.");
            }
        }

        // =====================================================================
        // ADMIN: incentive settings
        // =====================================================================

        public class IncentiveSettingsDto
        {
            public decimal? SchoolRate { get; set; }
        }

        [HttpGet("admin/settings")]
        public async Task<IActionResult> GetSettings()
        {
            if (!await AffiliateIncentiveAccess.IsAdminAsync(HttpContext, _db))
                return Fail(403, "You do not have permission to change incentive settings.");

            return Ok(new
            {
                success = true,
                schoolRate = await _incentives.GetSchoolRateAsync(),
                fixedRates = new { student = 5, teacher = 2, certificate = 10 }
            });
        }

        [HttpPut("admin/settings")]
        public async Task<IActionResult> SaveSettings([FromBody] IncentiveSettingsDto model)
        {
            if (!await AffiliateIncentiveAccess.IsAdminAsync(HttpContext, _db))
                return Fail(403, "You do not have permission to change incentive settings.");

            if (model?.SchoolRate is not decimal rate || rate < 0 || rate > 100000 || decimal.Round(rate, 2) != rate)
                return Fail(400, "Enter a school rate between ₹0 and ₹1,00,000 (up to 2 decimal places).");

            string? by = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!await _incentives.SetSchoolRateAsync(rate, by))
                return Fail(409, "The settings table does not exist yet. Please run Data/Scripts/RunOnce_Update_2026_11_20.sql first.");

            return Ok(new
            {
                success = true,
                schoolRate = rate,
                message = $"School rate saved: ₹{rate.ToString("#,##0.##", new CultureInfo("en-IN"))} per school, applied to all months."
            });
        }

        [HttpGet("admin/export")]
        public async Task<IActionResult> Export(
            [FromQuery] string? view,
            [FromQuery] string? search,
            [FromQuery] int? year,
            [FromQuery] int? month,
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] string? status,
            [FromQuery] bool includeZero = false,
            [FromQuery] string? sort = null,
            [FromQuery] string? dir = null)
        {
            if (!await AffiliateIncentiveAccess.IsAdminAsync(HttpContext, _db))
                return Fail(403, "You do not have permission to export the incentive report.");

            var data = await LoadAllAsync(false);

            var filter = new ReportFilter
            {
                Search = search, Year = year, Month = month, From = from, To = to,
                Status = status, IncludeZero = includeZero
            };

            bool descending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
            bool monthly = string.Equals(view, "monthly", StringComparison.OrdinalIgnoreCase);

            // Text cells are quoted; a leading = + - @ is neutralised so a
            // spreadsheet never treats a name as a formula.
            static string Text(string? value)
            {
                value ??= "";
                if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            static string Num(decimal value) => value.ToString("0.##", Inv);

            var csv = new StringBuilder();

            if (monthly)
            {
                csv.AppendLine("Affiliate Name,Affiliate ID,Affiliate Code,Month,Year,Active Students,Active Schools," +
                               "Active Teachers,Certificates,Evaluated Projects,Incentive Amount,Status,Last Updated");

                foreach (var r in Sort(BuildMonthlyRows(data, filter), sort, descending))
                {
                    csv.AppendLine(string.Join(",",
                        Text(r.Affiliate.Name), r.Affiliate.UserId, Text(r.Affiliate.Code),
                        Text(new DateTime(r.Month.Year, r.Month.Month, 1).ToString("MMMM", Inv)), r.Month.Year,
                        r.Month.ActiveStudents, r.Month.ActiveSchools, r.Month.ActiveTeachers,
                        r.Month.TotalCertificates, r.Month.EvaluatedProjects, Num(r.Month.TotalIncentive),
                        Text(Status(r.Month, data.CurrentKey)),
                        Text(r.Month.ComputedAt.ToString("dd MMM yyyy HH:mm", Inv))));
                }
            }
            else
            {
                csv.AppendLine("Affiliate Name,Affiliate ID,Affiliate Code,Mobile,Current Month Incentive," +
                               "Previous Month Incentive,Selected Period Incentive,Lifetime Incentive,Months With Incentive");

                foreach (var r in Sort(BuildAffiliateRows(data, filter), sort, descending))
                {
                    csv.AppendLine(string.Join(",",
                        Text(r.Affiliate.Name), r.Affiliate.UserId, Text(r.Affiliate.Code), Text(r.Affiliate.Mobile),
                        Num(r.Current), Num(r.Previous), Num(r.Period), Num(r.Lifetime), r.EarningMonths));
                }
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

            return File(bytes, "text/csv",
                $"Affiliate-Incentive-{(monthly ? "Monthly" : "Summary")}-{AffiliateIncentiveService.Now:yyyyMMdd-HHmm}.csv");
        }
    }
}
