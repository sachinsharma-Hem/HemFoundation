using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace HemFoundation.Services.Parent
{
    /// <summary>
    /// Parent portal on PostgreSQL: a parent links their children (found by the parent's registered
    /// mobile number) and sees each child's progress. Replaces the old SQL Server procedures
    /// HF_SP_GetUnmappedChildrenByMobile / ByUserID, HF_SP_MapChildToParent, HF_SP_GetStudentsDetailsByUserID
    /// (parent part), HF_SP_StudentsDashboardCount and HF_Proc_StudentsMainCoursewiseDetails.
    /// Table: Data/Scripts/RunOnce_Update_2026_11_27.sql (hf_tbl_parentchildassociation).
    /// </summary>
    public class ParentService
    {
        private readonly string _cs;
        private readonly ILogger<ParentService> _logger;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan DashboardCache = TimeSpan.FromMinutes(2);

        public ParentService(IConfiguration configuration, ILogger<ParentService> logger, IMemoryCache cache)
        {
            _cs = configuration.GetConnectionString("DefaultConnection") ?? "";
            _logger = logger;
            _cache = cache;
        }

        private NpgsqlConnection Open() => new(_cs);

        public class ParentException : Exception { public ParentException(string m) : base(m) { } }

        // =====================================================================
        // Records
        // =====================================================================
        public class Child
        {
            public int StudentId { get; set; }
            public string Name { get; set; } = "";
            public string? Standard { get; set; }
            public string? Division { get; set; }
            public string? RollNo { get; set; }
            public string? School { get; set; }
            public string? PhotoUrl { get; set; }
            public string? Username { get; set; }
            public string? Gender { get; set; }
            public string? City { get; set; }
            public DateTime? MappedOn { get; set; }
            // quick figures for the child cards
            public int Activities { get; set; }
            public decimal Points { get; set; }
            public int Certificates { get; set; }
            public DateTime? LastActive { get; set; }
        }

        public class CourseRow
        {
            public string CourseName { get; set; } = "";
            public int Mcq { get; set; }
            public int Practical { get; set; }
            public int FamilyProject { get; set; }
            public int GroupProject { get; set; }
            public int Survey { get; set; }
            public decimal McqPoints { get; set; }
            public decimal PracticalPoints { get; set; }
            public decimal PblPoints { get; set; }
            public int Certified { get; set; }
            public DateTime? LastActivity { get; set; }
            public decimal Marks => McqPoints + PracticalPoints + PblPoints;
            public const decimal MaxMarks = 100m;   // MCQ 50 + Practical 30 + PBL 20, as on the report card
            public string Status =>
                Certified > 0 || (FamilyProject > 0 && GroupProject > 0 && Survey > 0) ? "Completed"
                : Mcq + Practical + FamilyProject + GroupProject + Survey > 0 ? "In progress" : "Not started";
        }

        public class RecentActivity
        {
            public string? CourseName { get; set; }
            public string? Topic { get; set; }
            public string? Type { get; set; }
            public string? Points { get; set; }
            public DateTime? Date { get; set; }
        }

        public class Dashboard
        {
            public Child Child { get; set; } = new();
            public List<CourseRow> Courses { get; set; } = new();
            public List<RecentActivity> Recent { get; set; } = new();
            public int TotalCourses => Courses.Count;
            public int CoursesDone => Courses.Count(c => c.Status == "Completed");
            public int CoursesInProgress => Courses.Count(c => c.Status == "In progress");
            public int CoursesNotStarted => Courses.Count(c => c.Status == "Not started");
            public int Certificates { get; set; }
            public decimal TotalMarks => Courses.Count * CourseRow.MaxMarks;
            public decimal MarksGained => Courses.Sum(c => c.Marks);
            public int Mcq => Courses.Sum(c => c.Mcq);
            public int Practical => Courses.Sum(c => c.Practical);
            public int FamilyProjects => Courses.Sum(c => c.FamilyProject);
            public int GroupProjects => Courses.Sum(c => c.GroupProject);
            public int Surveys => Courses.Sum(c => c.Survey);
            public int? ClassRank { get; set; }
            public int ClassSize { get; set; }
            public DateTime GeneratedAt { get; set; } = DateTime.Now;
        }

        // =====================================================================
        // Setup check
        // =====================================================================
        public async Task<bool> TableExistsAsync()
        {
            try
            {
                await using var c = Open();
                return await c.ExecuteScalarAsync<bool>("SELECT to_regclass('public.hf_tbl_parentchildassociation') IS NOT NULL");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Parent: could not check the table");
                return false;
            }
        }

        // Student profile columns, used by every list below.
        private const string ChildColumns = @"
            s.studentid AS StudentId,
            btrim(coalesce(u.firstname, '') || ' ' || coalesce(u.lastname, '')) AS Name,
            btrim(s.standard) AS Standard, s.division AS Division, s.rollno AS RollNo,
            (SELECT i.""Name"" FROM wj_studentassociates a JOIN wj_institutes i ON i.instituteid = a.instituteid
             WHERE a.studentid = s.studentid ORDER BY (a.isprimaryassociate::text IN ('1', 'true')) DESC LIMIT 1) AS School,
            nullif(nullif(u.photourl, '-'), '') AS PhotoUrl, an.username AS Username,
            u.gender::text AS Gender, u.cityname AS City";

        // Digits only, last 10: '+91 98xxx', '098xxx' and '98xxx' all match.
        private const string Mobile = "right(regexp_replace(coalesce({0}, ''), '\\D', '', 'g'), 10)";

        // =====================================================================
        // Children linked to the parent
        // =====================================================================
        public async Task<List<Child>> ChildrenAsync(int parentId)
        {
            await using var c = Open();
            var list = (await c.QueryAsync<Child>(@"
                WITH kids AS (
                    SELECT childid, min(mappeddate) AS mappedon FROM hf_tbl_parentchildassociation
                    WHERE parentid = @parentId GROUP BY childid
                ),
                act AS (
                    SELECT l.studentid, count(*)::int AS activities, max(l.activity_date) AS lastactive,
                           sum(CASE WHEN l.points::text ~ '^\s*-?\d+(\.\d+)?\s*$' THEN l.points::text::numeric ELSE 0 END) AS points
                    FROM hf_view_studentactivitylog l WHERE l.studentid IN (SELECT childid FROM kids) GROUP BY l.studentid
                ),
                cert AS (
                    SELECT userid, count(*)::int AS certificates FROM wj_value_certification
                    WHERE userid IN (SELECT childid FROM kids) GROUP BY userid
                )
                SELECT " + ChildColumns + @", k.mappedon AS MappedOn,
                       coalesce(act.activities, 0) AS Activities, coalesce(act.points, 0) AS Points,
                       coalesce(cert.certificates, 0) AS Certificates, act.lastactive AS LastActive
                FROM kids k
                JOIN wj_students s ON s.studentid = k.childid
                LEFT JOIN users u ON u.userid = s.studentid
                LEFT JOIN aspnetusers an ON an.id = u.aspnetuserid
                LEFT JOIN act ON act.studentid = s.studentid
                LEFT JOIN cert ON cert.userid = s.studentid
                ORDER BY k.mappedon, s.studentid", new { parentId })).ToList();
            return list;
        }

        public async Task<bool> IsMappedAsync(int parentId, int studentId)
        {
            await using var c = Open();
            return await c.ExecuteScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM hf_tbl_parentchildassociation WHERE parentid = @parentId AND childid = @studentId)", new { parentId, studentId });
        }

        // =====================================================================
        // Children registered with the parent's mobile number (not linked yet)
        // =====================================================================
        public async Task<(string? Mobile, List<Child> Children)> FindChildrenAsync(int parentId)
        {
            await using var c = Open();
            string? mobile = await c.ExecuteScalarAsync<string?>("SELECT nullif(btrim(mobileno), '') FROM users WHERE userid = @parentId", new { parentId });
            string digits = new string((mobile ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length < 10) return (mobile, new List<Child>());
            digits = digits[^10..];

            // Same rule as the old procedure: the student's mobile number, or a login made from it
            // (brothers and sisters registered on one number get 98xxxxxxxx, 98xxxxxxxx_1, ...).
            var list = (await c.QueryAsync<Child>(@"
                SELECT " + ChildColumns + @"
                FROM wj_students s
                JOIN users u ON u.userid = s.studentid
                LEFT JOIN aspnetusers an ON an.id = u.aspnetuserid
                WHERE (" + string.Format(Mobile, "u.mobileno") + @" = @digits
                       OR an.username = @digits OR an.username LIKE @digits || '\_%')
                  AND u.userid <> @parentId
                  AND NOT EXISTS (SELECT 1 FROM hf_tbl_parentchildassociation p WHERE p.parentid = @parentId AND p.childid = s.studentid)
                ORDER BY s.studentid
                LIMIT 50", new { parentId, digits })).ToList();
            return (mobile, list);
        }

        /// <summary>Links a child. Only a child found by <see cref="FindChildrenAsync"/> can be linked.</summary>
        public async Task<Child> MapChildAsync(int parentId, int studentId)
        {
            var (_, found) = await FindChildrenAsync(parentId);
            var child = found.FirstOrDefault(x => x.StudentId == studentId);
            if (child == null)
            {
                if (await IsMappedAsync(parentId, studentId)) throw new ParentException("This child is already in your list.");
                throw new ParentException("This student is not registered with your mobile number, so they cannot be added.");
            }

            await using var c = Open();
            await c.OpenAsync();
            await using var tx = await c.BeginTransactionAsync();
            await c.ExecuteAsync("SELECT pg_advisory_xact_lock(881101, @parentId)", new { parentId }, tx);
            await c.ExecuteAsync(@"
                INSERT INTO hf_tbl_parentchildassociation (parentid, childid, mappeddate)
                SELECT @parentId, @studentId, now()
                WHERE NOT EXISTS (SELECT 1 FROM hf_tbl_parentchildassociation WHERE parentid = @parentId AND childid = @studentId)",
                new { parentId, studentId }, tx);
            await tx.CommitAsync();
            return child;
        }

        // =====================================================================
        // Dashboard of one child (one round trip, cached for two minutes)
        // =====================================================================
        public async Task<Dashboard?> DashboardAsync(int studentId, bool fresh = false)
        {
            string key = "parent-dash:" + studentId;
            if (!fresh && _cache.TryGetValue(key, out Dashboard? cached) && cached != null) return cached;

            await using var c = Open();
            var child = await c.QueryFirstOrDefaultAsync<Child>(@"
                SELECT " + ChildColumns + @"
                FROM wj_students s
                LEFT JOIN users u ON u.userid = s.studentid
                LEFT JOIN aspnetusers an ON an.id = u.aspnetuserid
                WHERE s.studentid = @studentId", new { studentId });
            if (child == null) return null;

            string standard = (child.Standard ?? "").Trim();
            const string pts = @"CASE WHEN l.points::text ~ '^\s*-?\d+(\.\d+)?\s*$' THEN l.points::text::numeric ELSE 0 END";

            using var multi = await c.QueryMultipleAsync(@"
                -- 1. course-wise activity of this standard (HF_Proc_StudentsMainCoursewiseDetails)
                SELECT l.maincoursename AS CourseName,
                       count(*) FILTER (WHERE l.activity_type = 'MCQ')::int AS Mcq,
                       count(*) FILTER (WHERE l.activity_type IN ('Pratical', 'Practical'))::int AS Practical,
                       count(*) FILTER (WHERE l.activity_type IN ('Individual PBL', 'Family PBL'))::int AS FamilyProject,
                       count(*) FILTER (WHERE l.activity_type = 'Group PBL')::int AS GroupProject,
                       count(*) FILTER (WHERE l.activity_type = 'Survey')::int AS Survey,
                       coalesce(sum(" + pts + @") FILTER (WHERE l.activity_type = 'MCQ'), 0) AS McqPoints,
                       coalesce(sum(" + pts + @") FILTER (WHERE l.activity_type IN ('Pratical', 'Practical', 'HEM Project')), 0) AS PracticalPoints,
                       coalesce(sum(" + pts + @") FILTER (WHERE l.activity_type ILIKE '%PBL%'), 0) AS PblPoints,
                       max(l.activity_date) AS LastActivity
                FROM hf_view_studentactivitylog l
                WHERE l.studentid = @studentId AND btrim(l.standard::text) = @standard
                  AND l.maincoursename IS NOT NULL AND l.maincoursename NOT ILIKE '%survey%'
                GROUP BY l.maincoursename;

                -- 2. certificates, with the course they belong to
                SELECT coalesce(v.coursename, '') FROM wj_value_certification v WHERE v.userid = @studentId;

                -- 3. which course each sub-course belongs to (from the child's own activity)
                SELECT DISTINCT l.coursename, l.maincoursename FROM hf_view_studentactivitylog l
                WHERE l.studentid = @studentId AND l.coursename IS NOT NULL AND l.maincoursename IS NOT NULL;

                -- 4. latest activity
                SELECT l.maincoursename AS CourseName, l.topicname AS Topic, l.activity_type AS Type, l.points::text AS Points, l.activity_date AS Date
                FROM hf_view_studentactivitylog l WHERE l.studentid = @studentId
                ORDER BY l.activity_date DESC NULLS LAST LIMIT 8;", new { studentId, standard });

            var courses = (await multi.ReadAsync<CourseRow>()).ToList();
            var certs = (await multi.ReadAsync<string>()).ToList();
            var courseOf = (await multi.ReadAsync<(string Course, string Main)>())
                .GroupBy(x => x.Course, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Main, StringComparer.OrdinalIgnoreCase);
            var recent = (await multi.ReadAsync<RecentActivity>()).ToList();

            // Courses of the standard with no activity yet (HF_SP_StudentsDashboardCount counted them in Total Courses).
            try
            {
                var syllabus = await c.QueryAsync<string>(@"
                    SELECT DISTINCT maincoursename FROM view_wj_syllabus
                    WHERE btrim(standard_::text) = @standard AND maincoursename IS NOT NULL AND maincoursename NOT ILIKE '%survey%'",
                    new { standard });
                var have = courses.Select(x => x.CourseName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                courses.AddRange(syllabus.Where(n => !have.Contains(n)).Select(n => new CourseRow { CourseName = n }));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Parent: syllabus not available");
            }

            foreach (var name in certs)
                if (courseOf.TryGetValue(name, out var main))
                {
                    var row = courses.FirstOrDefault(x => x.CourseName.Equals(main, StringComparison.OrdinalIgnoreCase));
                    if (row != null) row.Certified++;
                }

            var dash = new Dashboard
            {
                Child = child,
                Courses = courses.OrderBy(x => x.Status == "Not started").ThenBy(x => x.CourseName).ToList(),
                Recent = recent,
                Certificates = certs.Count
            };

            // Rank in the class: same school and standard, by points earned.
            try
            {
                var rank = await c.QueryFirstOrDefaultAsync<(int? Rank, int Size)>(@"
                    WITH inst AS (
                        SELECT instituteid FROM wj_studentassociates WHERE studentid = @studentId
                        ORDER BY (isprimaryassociate::text IN ('1', 'true')) DESC LIMIT 1
                    ),
                    totals AS (
                        SELECT l.studentid, sum(" + pts + @") AS total
                        FROM hf_view_studentactivitylog l
                        WHERE l.instituteid = (SELECT instituteid FROM inst) AND btrim(l.standard::text) = @standard
                        GROUP BY l.studentid
                    ),
                    ranked AS (SELECT studentid, dense_rank() OVER (ORDER BY total DESC)::int AS rnk FROM totals WHERE total > 0)
                    SELECT (SELECT rnk FROM ranked WHERE studentid = @studentId), (SELECT count(*)::int FROM ranked)", new { studentId, standard });
                dash.ClassRank = rank.Rank;
                dash.ClassSize = rank.Size;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Parent: class rank not available");
            }

            _cache.Set(key, dash, DashboardCache);
            return dash;
        }
    }
}
