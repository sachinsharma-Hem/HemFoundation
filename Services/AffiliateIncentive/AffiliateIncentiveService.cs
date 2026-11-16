using HemFoundation.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;

namespace HemFoundation.Services.AffiliateIncentive
{
    public class AffiliateInfo
    {
        public int UserId { get; set; }
        public string AspNetUserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Code { get; set; }
        public string? Mobile { get; set; }
        public string? ReferralCode { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>One affiliate's incentive for one calendar month.</summary>
    public class MonthlyIncentive
    {
        public int Year { get; set; }
        public int Month { get; set; }

        // From public.get_incentive_dashboard_fast (unchanged calculation).
        public int ActiveStudents { get; set; }
        public decimal StudentIncentive { get; set; }
        public int ActiveSchools { get; set; }
        public decimal SchoolIncentive { get; set; }
        public int ActiveTeachers { get; set; }
        public decimal TeacherIncentive { get; set; }
        public int TotalCertificates { get; set; }
        public decimal CertificateIncentive { get; set; }
        public decimal BaseIncentive { get; set; }

        // Evaluated projects (same rule the incentive page has always used).
        public int Green { get; set; }
        public int Orange { get; set; }
        public int Blue { get; set; }
        public int Yellow { get; set; }
        public int Red { get; set; }
        public int EvaluatedProjects => Green + Orange + Blue + Yellow + Red;
        public decimal ProjectIncentive { get; set; }

        public decimal TotalIncentive => BaseIncentive + ProjectIncentive;
        public DateTime ComputedAt { get; set; }

        public int Key => Year * 100 + Month;
    }

    /// <summary>
    /// Month-wise affiliate incentives for the affiliate pages and the admin
    /// report.
    ///
    /// The calculation itself is NOT reimplemented here:
    ///   * students / schools / teachers / certificates come from the existing
    ///     database function public.get_incentive_dashboard_fast, which keeps
    ///     its results in public.incentive_dashboard_cache;
    ///   * evaluated projects use the rule the incentive page applied in the
    ///     browser (Green 20, Orange 10, Blue 10, Yellow 5, Red 0 per project
    ///     evaluated in the month), now counted by one aggregate query.
    ///
    /// What this class adds is efficiency. Months that are already stored and
    /// final are read from incentive_dashboard_cache in ONE query for any
    /// number of affiliates. Only the months that are missing, were stored
    /// before the month had ended, or are the current month go through the
    /// function, and those calls are batched instead of one round trip each.
    /// </summary>
    public class AffiliateIncentiveService
    {
        // Project incentive per evaluated project, by evaluation colour.
        public const decimal GreenRate = 20, OrangeRate = 10, BlueRate = 10, YellowRate = 5, RedRate = 0;

        private const int BatchSize = 40;        // (affiliate, month) pairs per SQL statement
        private const int MaxParallel = 4;       // concurrent database connections for a cold load

        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<AffiliateIncentiveService> _logger;

        public AffiliateIncentiveService(
            ApplicationDbContext db,
            IConfiguration config,
            ILogger<AffiliateIncentiveService> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }

        /// <summary>Number of statements sent to the database by this instance (diagnostics).</summary>
        public int DatabaseCalls { get; private set; }

        /// <summary>Business "now" (India time), matching the dates stored by the application.</summary>
        public static DateTime Now => DateTime.UtcNow.AddMinutes(330);

        private string ConnectionString =>
            _config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        // =====================================================================
        // Affiliates
        // =====================================================================

        public async Task<AffiliateInfo?> FindByAspNetUserIdAsync(string aspNetUserId)
        {
            DatabaseCalls++;
            var user = await _db.Users.AsNoTracking()
                .Where(u => u.AspNetUserID == aspNetUserId)
                .Select(u => new { u.UserID, u.AspNetUserID, u.FirstName, u.LastName, u.UserCode, u.MobileNo, u.CreatedDate })
                .FirstOrDefaultAsync();

            if (user == null) return null;

            DatabaseCalls++;
            var referralCode = await _db.WJ_ReferralCodes.AsNoTracking()
                .Where(r => r.UserID == user.UserID)
                .OrderBy(r => r.ReferralID)
                .Select(r => r.ReferralCode)
                .FirstOrDefaultAsync();

            return new AffiliateInfo
            {
                UserId = user.UserID,
                AspNetUserId = user.AspNetUserID,
                Name = $"{user.FirstName} {user.LastName}".Trim(),
                Code = user.UserCode,
                Mobile = user.MobileNo,
                ReferralCode = referralCode,
                CreatedDate = user.CreatedDate
            };
        }

        /// <summary>Every affiliate (the same source the other admin pages use), in two queries.</summary>
        public async Task<List<AffiliateInfo>> ListAffiliatesAsync()
        {
            DatabaseCalls++;
            var ids = await _db.View_HF_Affiliate.AsNoTracking()
                .Select(v => v.InstituteID)
                .Distinct()
                .ToListAsync();

            if (ids.Count == 0) return new List<AffiliateInfo>();

            DatabaseCalls++;
            var users = await _db.Users.AsNoTracking()
                .Where(u => ids.Contains(u.UserID))
                .Select(u => new { u.UserID, u.AspNetUserID, u.FirstName, u.LastName, u.UserCode, u.MobileNo, u.CreatedDate })
                .ToListAsync();

            DatabaseCalls++;
            var codes = (await _db.WJ_ReferralCodes.AsNoTracking()
                    .Where(r => r.UserID != null && ids.Contains(r.UserID.Value))
                    .Select(r => new { r.UserID, r.ReferralID, r.ReferralCode })
                    .ToListAsync())
                .GroupBy(r => r.UserID!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.ReferralID).First().ReferralCode);

            return users
                .Where(u => !string.IsNullOrEmpty(u.AspNetUserID))
                .Select(u => new AffiliateInfo
                {
                    UserId = u.UserID,
                    AspNetUserId = u.AspNetUserID,
                    Name = $"{u.FirstName} {u.LastName}".Trim(),
                    Code = u.UserCode,
                    Mobile = u.MobileNo,
                    ReferralCode = codes.GetValueOrDefault(u.UserID),
                    CreatedDate = u.CreatedDate
                })
                .OrderBy(a => a.Name)
                .ToList();
        }

        // =====================================================================
        // Month range
        // =====================================================================

        /// <summary>
        /// First month to report for an affiliate: the month the account was
        /// created, limited by AffiliateIncentive:StartMonth (yyyy-MM) and
        /// AffiliateIncentive:MaxMonths (default 36).
        /// </summary>
        private DateTime FirstMonth(AffiliateInfo affiliate, DateTime currentMonth)
        {
            var start = new DateTime(affiliate.CreatedDate.Year, affiliate.CreatedDate.Month, 1);

            if (DateTime.TryParseExact(_config["AffiliateIncentive:StartMonth"], "yyyy-MM",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var floor) && start < floor)
                start = floor;

            int maxMonths = Math.Clamp(_config.GetValue<int?>("AffiliateIncentive:MaxMonths") ?? 36, 2, 120);
            var oldest = currentMonth.AddMonths(-(maxMonths - 1));
            if (start < oldest) start = oldest;

            // Always include at least the current and the previous month.
            var previous = currentMonth.AddMonths(-1);
            return start > previous ? previous : start;
        }

        // =====================================================================
        // History: all months for a set of affiliates
        // =====================================================================

        /// <summary>
        /// Month-wise incentives (oldest first) for each affiliate.
        /// refresh = true sends every month through the database function
        /// (which recalculates anything older than its own 10 minute limit).
        /// </summary>
        public async Task<Dictionary<int, List<MonthlyIncentive>>> GetHistoryAsync(
            IReadOnlyCollection<AffiliateInfo> affiliates, bool refresh = false)
        {
            var now = Now;
            var currentMonth = new DateTime(now.Year, now.Month, 1);

            // 1. Stored months that are final (calculated after the month ended).
            var stored = refresh
                ? new Dictionary<(string, int, int), string>()
                : await ReadStoredMonthsAsync(affiliates);

            // 1b. Months that are not stored, not final or not fresh (or
            //     everything, on a refresh): one pass per affiliate through
            //     refresh_incentive_dashboard_range, when that function is
            //     installed. It gives the same figures as the per-month
            //     function without rescanning the activity logs for each month.
            if (await BackfillClosedMonthsAsync(affiliates, stored, currentMonth, refresh))
                stored = await ReadStoredMonthsAsync(affiliates);

            var result = new Dictionary<int, List<MonthlyIncentive>>();
            var toCompute = new List<(AffiliateInfo Affiliate, int Year, int Month)>();

            foreach (var affiliate in affiliates)
            {
                var months = new List<MonthlyIncentive>();
                result[affiliate.UserId] = months;

                for (var m = FirstMonth(affiliate, currentMonth); m <= currentMonth; m = m.AddMonths(1))
                {
                    var item = new MonthlyIncentive { Year = m.Year, Month = m.Month };
                    months.Add(item);

                    if (stored.TryGetValue((affiliate.AspNetUserId, m.Year, m.Month), out var json))
                        ApplyFunctionResult(item, json);
                    else
                        toCompute.Add((affiliate, m.Year, m.Month));
                }
            }

            // 2. Everything else (always including the current month) through
            //    the existing function, in batches. The function stores what it
            //    calculates, so closed months are computed only once.
            if (toCompute.Count > 0)
            {
                var computed = await ComputeBaseAsync(toCompute);

                foreach (var (affiliate, year, month) in toCompute)
                {
                    if (computed.TryGetValue((affiliate.AspNetUserId, year, month), out var json))
                        ApplyFunctionResult(
                            result[affiliate.UserId].First(x => x.Year == year && x.Month == month), json);
                }
            }

            FunctionCalls = toCompute.Count;

            // 3. Evaluated projects: one aggregate query for everyone.
            var projects = await GetProjectCountsAsync(affiliates);

            foreach (var affiliate in affiliates)
            {
                if (string.IsNullOrEmpty(affiliate.ReferralCode)) continue;

                foreach (var month in result[affiliate.UserId])
                {
                    if (!projects.TryGetValue((affiliate.ReferralCode, month.Key), out var counts)) continue;

                    month.Green = counts.GetValueOrDefault("green");
                    month.Orange = counts.GetValueOrDefault("orange");
                    month.Blue = counts.GetValueOrDefault("blue");
                    month.Yellow = counts.GetValueOrDefault("yellow");
                    month.Red = counts.GetValueOrDefault("red");
                    month.ProjectIncentive =
                        month.Green * GreenRate + month.Orange * OrangeRate + month.Blue * BlueRate +
                        month.Yellow * YellowRate + month.Red * RedRate;
                }
            }

            return result;
        }

        /// <summary>(affiliate, month) pairs that had to go through the database function.</summary>
        public int FunctionCalls { get; private set; }

        /// <summary>Affiliates whose missing months were calculated in one pass.</summary>
        public int RangeCalls { get; private set; }

        // Whether refresh_incentive_dashboard_range exists in this database.
        // Remembered once found; re-checked occasionally while it is absent.
        private static bool _rangeFunctionAvailable;
        private static DateTime _rangeFunctionCheckedAt = DateTime.MinValue;

        private async Task<bool> RangeFunctionAvailableAsync()
        {
            if (_rangeFunctionAvailable) return true;
            if (DateTime.UtcNow - _rangeFunctionCheckedAt < TimeSpan.FromMinutes(5)) return false;

            _rangeFunctionCheckedAt = DateTime.UtcNow;

            try
            {
                await using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                await using var cmd = new NpgsqlCommand(
                    "SELECT to_regprocedure('public.refresh_incentive_dashboard_range(text,date,date,boolean)') IS NOT NULL",
                    conn);

                DatabaseCalls++;
                _rangeFunctionAvailable = (bool)(await cmd.ExecuteScalarAsync() ?? false);
            }
            catch (PostgresException ex)
            {
                _logger.LogWarning(ex, "Affiliate incentive: could not check for the range function");
            }

            return _rangeFunctionAvailable;
        }

        /// <summary>
        /// Calculates each affiliate's missing or stale months in a single pass.
        /// Returns true when anything was written (the caller then re-reads
        /// the stored months). Does nothing when the optional database
        /// function is not installed: the per-month function is used instead.
        /// </summary>
        private async Task<bool> BackfillClosedMonthsAsync(
            IReadOnlyCollection<AffiliateInfo> affiliates,
            Dictionary<(string, int, int), string> stored,
            DateTime currentMonth,
            bool overwrite)
        {
            var ranges = new List<(string Uid, DateTime From, DateTime To)>();

            foreach (var affiliate in affiliates)
            {
                DateTime? from = null, to = null;

                // Includes the current month when its stored row is not fresh.
                for (var m = FirstMonth(affiliate, currentMonth); m <= currentMonth; m = m.AddMonths(1))
                {
                    if (!overwrite && stored.ContainsKey((affiliate.AspNetUserId, m.Year, m.Month))) continue;
                    from ??= m;
                    to = m;
                }

                if (from != null) ranges.Add((affiliate.AspNetUserId, from.Value, to!.Value));
            }

            if (ranges.Count == 0 || !await RangeFunctionAvailableAsync()) return false;

            const string sql = """
                SELECT public.refresh_incentive_dashboard_range(t.uid, t.f, t.t, @overwrite)
                FROM unnest(@uids, @froms, @tos) AS t(uid, f, t)
                """;

            var batches = ranges.Chunk(10).ToList();
            int timeout = Math.Clamp(_config.GetValue<int?>("AffiliateIncentive:CommandTimeoutSeconds") ?? 180, 30, 900);

            await Parallel.ForEachAsync(
                batches,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallel },
                async (batch, cancel) =>
                {
                    await using var conn = new NpgsqlConnection(ConnectionString);
                    await conn.OpenAsync(cancel);

                    await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = timeout };
                    cmd.Parameters.AddWithValue("overwrite", overwrite);
                    cmd.Parameters.AddWithValue("uids", batch.Select(r => r.Uid).ToArray());
                    cmd.Parameters.Add(new NpgsqlParameter("froms", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Date)
                        { Value = batch.Select(r => DateOnly.FromDateTime(r.From)).ToArray() });
                    cmd.Parameters.Add(new NpgsqlParameter("tos", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Date)
                        { Value = batch.Select(r => DateOnly.FromDateTime(r.To)).ToArray() });

                    await cmd.ExecuteNonQueryAsync(cancel);
                });

            DatabaseCalls += batches.Count;
            RangeCalls = ranges.Count;
            return true;
        }

        /// <summary>
        /// Reads incentive_dashboard_cache for all the affiliates in one query
        /// and returns the rows that can be used as they are: closed months
        /// whose row was written after the month ended (final), and any row
        /// written in the last 10 minutes (the same freshness rule
        /// get_incentive_dashboard_fast applies). An older row written during
        /// its own month only holds part of the month, so it is recalculated.
        /// The row is returned in the same JSON shape as the function.
        /// </summary>
        private async Task<Dictionary<(string, int, int), string>> ReadStoredMonthsAsync(
            IReadOnlyCollection<AffiliateInfo> affiliates)
        {
            var result = new Dictionary<(string, int, int), string>();
            if (affiliates.Count == 0) return result;

            const string sql = """
                SELECT c.aspnet_user_id, c.year, c.month,
                       jsonb_build_object(
                           'success', true,
                           'data', jsonb_build_object(
                               'activeStudents', c.active_students,
                               'studentIncentive', c.student_incentive,
                               'activeTeachers', c.active_teachers,
                               'teacherIncentive', c.teacher_incentive,
                               'activeSchools', c.active_schools,
                               'schoolIncentive', c.school_incentive,
                               'totalCertificates', c.total_certificates,
                               'certificateIncentive', c.certificate_incentive,
                               'totalIncentive', c.total_incentive,
                               'history', c.history,
                               'updatedAt', c.updated_at))::text
                FROM public.incentive_dashboard_cache c
                WHERE c.aspnet_user_id = ANY(@uids)
                  AND c.month BETWEEN 1 AND 12
                  AND (c.updated_at >= (make_date(c.year, c.month, 1) + interval '1 month')
                       OR c.updated_at >= now() - interval '10 minutes')
                """;

            try
            {
                await using var conn = new NpgsqlConnection(ConnectionString);
                await conn.OpenAsync();

                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("uids", affiliates.Select(a => a.AspNetUserId).Distinct().ToArray());

                DatabaseCalls++;
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                    result[(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2))] = reader.GetString(3);
            }
            catch (PostgresException ex)
            {
                // Table not readable: fall back to the function for every month.
                _logger.LogWarning(ex, "Affiliate incentive: incentive_dashboard_cache could not be read");
                result.Clear();
            }

            return result;
        }

        // =====================================================================
        // The existing database function, called in batches
        // =====================================================================

        /// <summary>
        /// Runs public.get_incentive_dashboard_fast for many (affiliate, month)
        /// pairs. Each SQL statement handles a batch of pairs, and a few
        /// batches run in parallel, instead of one round trip per pair.
        /// </summary>
        private async Task<Dictionary<(string, int, int), string>> ComputeBaseAsync(
            List<(AffiliateInfo Affiliate, int Year, int Month)> pairs)
        {
            const string sql = """
                SELECT t.uid, t.y, t.m,
                       public.get_incentive_dashboard_fast(t.uid, t.y, t.m)::text
                FROM unnest(@uids, @years, @months) AS t(uid, y, m)
                """;

            var output = new ConcurrentDictionary<(string, int, int), string>();
            var batches = pairs.Chunk(BatchSize).ToList();
            int timeout = Math.Clamp(_config.GetValue<int?>("AffiliateIncentive:CommandTimeoutSeconds") ?? 180, 30, 900);

            await Parallel.ForEachAsync(
                batches,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallel },
                async (batch, cancel) =>
                {
                    await using var conn = new NpgsqlConnection(ConnectionString);
                    await conn.OpenAsync(cancel);

                    await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = timeout };
                    cmd.Parameters.AddWithValue("uids", batch.Select(p => p.Affiliate.AspNetUserId).ToArray());
                    cmd.Parameters.AddWithValue("years", batch.Select(p => p.Year).ToArray());
                    cmd.Parameters.AddWithValue("months", batch.Select(p => p.Month).ToArray());

                    await using var reader = await cmd.ExecuteReaderAsync(cancel);
                    while (await reader.ReadAsync(cancel))
                    {
                        if (reader.IsDBNull(3)) continue;
                        output[(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2))] = reader.GetString(3);
                    }
                });

            DatabaseCalls += batches.Count;
            return new Dictionary<(string, int, int), string>(output);
        }

        private static decimal Number(JsonElement parent, string name)
        {
            if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value)) return 0;

            return value.ValueKind switch
            {
                JsonValueKind.Number => value.TryGetDecimal(out var d) ? d : 0,
                JsonValueKind.String => decimal.TryParse(value.GetString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 0,
                _ => 0
            };
        }

        /// <summary>
        /// Reads the function's JSON the way the incentive page does: the four
        /// component figures, and the month's total from the last entry of
        /// "history" when present (otherwise the sum of the components).
        /// </summary>
        private void ApplyFunctionResult(MonthlyIncentive target, string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True ||
                    !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                    return;

                target.ActiveStudents = (int)Number(data, "activeStudents");
                target.StudentIncentive = Number(data, "studentIncentive");
                target.ActiveSchools = (int)Number(data, "activeSchools");
                target.SchoolIncentive = Number(data, "schoolIncentive");
                target.ActiveTeachers = (int)Number(data, "activeTeachers");
                target.TeacherIncentive = Number(data, "teacherIncentive");
                target.TotalCertificates = (int)Number(data, "totalCertificates");
                target.CertificateIncentive = Number(data, "certificateIncentive");

                decimal total = target.StudentIncentive + target.SchoolIncentive +
                                target.TeacherIncentive + target.CertificateIncentive;

                // The database function reports two different totals for a
                // month: "totalIncentive" includes the school incentive, the
                // last "history" entry does not. The incentive page has always
                // shown the history figure, so that stays the default; set
                // AffiliateIncentive:IncludeSchoolIncentiveInTotal = true to
                // count the school incentive in monthly and lifetime totals.
                bool includeSchools = _config.GetValue<bool>("AffiliateIncentive:IncludeSchoolIncentiveInTotal");

                if (!includeSchools &&
                    data.TryGetProperty("history", out var history) &&
                    history.ValueKind == JsonValueKind.Array && history.GetArrayLength() > 0)
                {
                    var last = history[history.GetArrayLength() - 1];
                    if (last.ValueKind == JsonValueKind.Object && last.TryGetProperty("totalIncentive", out _))
                        total = Number(last, "totalIncentive");
                }

                target.BaseIncentive = total;

                if (data.TryGetProperty("updatedAt", out var updated) && updated.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(updated.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var updatedAt))
                    target.ComputedAt = updatedAt;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Affiliate incentive: unreadable result for {Year}-{Month}", target.Year, target.Month);
            }
        }

        // =====================================================================
        // Evaluated projects: one aggregate query
        // =====================================================================

        /// <summary>
        /// Evaluated projects per affiliate referral code, month and colour.
        /// A project counts for an affiliate when it belongs to one of the
        /// affiliate's schools, or to a student of one of those schools (the
        /// same rule as /api/project-evaluations/affiliate-history), and is
        /// counted once per affiliate.
        /// </summary>
        private async Task<Dictionary<(string Code, int MonthKey), Dictionary<string, int>>> GetProjectCountsAsync(
            IReadOnlyCollection<AffiliateInfo> affiliates)
        {
            var result = new Dictionary<(string, int), Dictionary<string, int>>();

            var codes = affiliates
                .Select(a => a.ReferralCode)
                .Where(c => !string.IsNullOrEmpty(c))
                .Select(c => c!)
                .Distinct()
                .ToArray();

            if (codes.Length == 0) return result;

            const string sql = """
                SELECT x.code,
                       (date_part('year', x.evaluated)::int * 100 + date_part('month', x.evaluated)::int) AS month_key,
                       lower(btrim(coalesce(x.color, ''))) AS color,
                       count(*)::int AS projects
                FROM (
                    SELECT i.referralcode AS code, e."EvaluationID" AS id,
                           e."EvaluatedDate" AS evaluated, e."StatusColor" AS color
                    FROM "ProjectEvaluations" e
                    JOIN wj_institutes i ON i.instituteid = e."InstituteID"
                    WHERE i.referralcode = ANY(@codes)
                    UNION
                    SELECT i.referralcode, e."EvaluationID", e."EvaluatedDate", e."StatusColor"
                    FROM "ProjectEvaluations" e
                    JOIN wj_studentassociates sa ON sa.studentid = e."StudentID"
                    JOIN wj_institutes i ON i.instituteid = sa.instituteid
                    WHERE i.referralcode = ANY(@codes)
                ) x
                GROUP BY 1, 2, 3
                """;

            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("codes", codes);

            DatabaseCalls++;
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var key = (reader.GetString(0), reader.GetInt32(1));
                if (!result.TryGetValue(key, out var colours))
                    result[key] = colours = new Dictionary<string, int>();

                colours[reader.GetString(2)] = reader.GetInt32(3);
            }

            return result;
        }
    }

    /// <summary>Who may open the admin incentive report.</summary>
    public static class AffiliateIncentiveAccess
    {
        private static readonly string[] AdminRoles = { "Admin", "WJ_Admin" };

        /// <summary>
        /// Admin accounts, the WJ_Admin role, and any role that was given the
        /// "Admin Incentive Report" menu through the existing role-menu rights.
        /// </summary>
        public static async Task<bool> IsAdminAsync(HttpContext http, ApplicationDbContext db)
        {
            var roles = http.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            var sessionRole = http.Session.GetString("UserRole");
            if (http.Session.GetString("IsAdminLoggedIn") == "true" && !string.IsNullOrEmpty(sessionRole))
                roles.Add(sessionRole);

            if (roles.Count == 0) return false;
            if (roles.Any(r => AdminRoles.Contains(r))) return true;

            var roleIds = await db.Roles
                .Where(r => roles.Contains(r.Name!))
                .Select(r => r.Id)
                .ToListAsync();

            return await (
                from right in db.RoleMenuRights
                join menu in db.Menus on right.MenuId equals menu.Id
                where roleIds.Contains(right.RoleId)
                      && right.CanView && right.IsActive && !right.IsDeleted
                      && menu.IsActive && !menu.IsDeleted
                      && menu.RouteUrl != null
                      && menu.RouteUrl.ToLower().Contains("affiliate/adminincentivereport")
                select right.Id).AnyAsync();
        }
    }
}
