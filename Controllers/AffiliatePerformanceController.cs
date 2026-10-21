using ClosedXML.Excel;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace HemFoundation.Controllers
{
    public class AffiliatePerformanceController : Controller
    {
        private readonly string _connectionString;

        public AffiliatePerformanceController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' was not configured.");

            QuestPDF.Settings.License = LicenseType.Community;
        }

        // Explicit projection: Password is NEVER selected.
        private const string BaseSql = @"
            SELECT
                ""Afiiliate_ID""::bigint AS ""AffiliateId"",
                COALESCE(""Name"", '')::text AS ""Name"",
                COALESCE(""Referral COde"", '')::text AS ""ReferralCode"",
                COALESCE(""School Count"", 0)::bigint AS ""TotalSchools"",
                COALESCE(""Total Students"", 0)::bigint AS ""TotalStudents"",
                COALESCE(""Active Students"", 0)::bigint AS ""ActiveStudents"",
                COALESCE(""Total Teachers"", 0)::bigint AS ""TotalTeachers"",
                COALESCE(""Active Teachers"", 0)::bigint AS ""ActiveTeachers"",
                COALESCE(""Total Sel Project"", 0)::bigint AS ""TotalSelectedProjects"",
                COALESCE(""Family Project"", 0)::bigint AS ""FamilyProjects"",
                COALESCE(""Individual Project"", 0)::bigint AS ""IndividualProjects"",
                COALESCE(""Group Project"", 0)::bigint AS ""GroupPbl""
            FROM public.hf_view_Affiliate_PerformanceReport";

        // Only these hardcoded SQL expressions can be used for sorting.
        private static readonly Dictionary<string, string> SortColumns =
             new(StringComparer.OrdinalIgnoreCase)
             {
                 ["AffiliateId"] = @"""Afiiliate_ID""",
                 ["Name"] = @"""Name""",
                 ["ReferralCode"] = @"""ReferralCOde""", // Note exact casing from your DB view
                 ["TotalSchools"] = @"""School Count""",
                 ["TotalStudents"] = @"""Total Students""",
                 ["ActiveStudents"] = @"""Active Students""",
                 ["TotalTeachers"] = @"""Total Teachers""",
                 ["ActiveTeachers"] = @"""Active Teachers""",
                 ["TotalSelectedProjects"] = @"""Total Sel Project""",
                 ["FamilyProjects"] = @"""Family Project""",
                 ["IndividualProjects"] = @"""Individual Project""",
                 ["GroupPbl"] = @"""Group Project"""
             };

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            int page = 1,
            int pageSize = 10,
            string sortBy = "Name",
            string sortDir = "asc",
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 10, 100);

            if (!SortColumns.ContainsKey(sortBy))
                sortBy = "Name";

            sortDir = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? "DESC"
                : "ASC";

            search = search?.Trim();
            var parameters = new DynamicParameters();
            parameters.Add("Search", search);

            string whereSql = @"
                WHERE (
                    @Search::text IS NULL OR @Search::text = ''
                    OR COALESCE(""Name"", '') ILIKE '%' || @Search::text || '%'
                    OR COALESCE(""Referral COde"", '') ILIKE '%' || @Search::text || '%'
                    OR CAST(""Afiiliate_ID"" AS text) ILIKE '%' || @Search::text || '%'
                )";
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var totalCount = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM public.hf_view_Affiliate_PerformanceReport " + whereSql,
                    parameters,
                    cancellationToken: cancellationToken));

            var rowsSql = $@"
                {BaseSql}
                {whereSql}
                ORDER BY {SortColumns[sortBy]} {sortDir} NULLS LAST
                LIMIT @Limit OFFSET @Offset";

            parameters.Add("Limit", pageSize);
            parameters.Add("Offset", (page - 1) * pageSize);

            var rows = (await connection.QueryAsync<AffiliatePerformanceRow>(
                new CommandDefinition(
                    rowsSql,
                    parameters,
                    cancellationToken: cancellationToken))).ToList();

            // KPI cards use the full filtered result set, not just the current page.
            var summarySql = $@"
                SELECT
                    COUNT(*)::bigint AS ""TotalAffiliates"",
                    COALESCE(SUM(COALESCE(""School Count"", 0)), 0)::bigint AS ""TotalSchools"",
                    COALESCE(SUM(COALESCE(""Total Students"", 0)), 0)::bigint AS ""TotalStudents"",
                    COALESCE(SUM(COALESCE(""Active Students"", 0)), 0)::bigint AS ""ActiveStudents"",
                    COALESCE(SUM(COALESCE(""Total Teachers"", 0)), 0)::bigint AS ""TotalTeachers"",
                    COALESCE(SUM(COALESCE(""Total Sel Project"", 0)), 0)::bigint AS ""TotalProjects""
                FROM public.hf_view_Affiliate_PerformanceReport
                {whereSql}";

            var summary = await connection.QuerySingleAsync<AffiliatePerformanceSummary>(
                new CommandDefinition(
                    summarySql,
                    new { Search = search },
                    cancellationToken: cancellationToken));

            // Chart data: top 8 affiliates in the filtered result.
            var chartSql = $@"
                {BaseSql}
                {whereSql}
                ORDER BY ""Total Students"" DESC NULLS LAST
                LIMIT 8";

            var chartRows = (await connection.QueryAsync<AffiliatePerformanceRow>(
                new CommandDefinition(
                    chartSql,
                    new { Search = search },
                    cancellationToken: cancellationToken))).ToList();

            var model = new AffiliatePerformanceDashboardViewModel
            {
                Search = search,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                SortBy = sortBy,
                SortDir = sortDir.ToLowerInvariant(),
                Rows = rows,
                Summary = summary,
                ChartLabelsJson = JsonSerializer.Serialize(
                    chartRows.Select(x => x.Name)),
                ChartStudentsJson = JsonSerializer.Serialize(
                    chartRows.Select(x => x.TotalStudents)),
                ChartProjectsJson = JsonSerializer.Serialize(
                    chartRows.Select(x => x.TotalSelectedProjects)),
                ChartProjectTypesJson = JsonSerializer.Serialize(new[]
                {
                    summary.TotalProjects >= 0 ? chartRows.Sum(x => x.FamilyProjects) : 0,
                    chartRows.Sum(x => x.IndividualProjects),
                    chartRows.Sum(x => x.GroupPbl)
                })
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Export(
            string format,
            string? search,
            CancellationToken cancellationToken = default)
        {
            format = format?.Trim().ToLowerInvariant() ?? "";

            if (format is not ("xlsx" or "csv" or "pdf"))
                return BadRequest("Supported formats are xlsx, csv and pdf.");

            search = search?.Trim();

            var sql = $@"
                {BaseSql}
                WHERE (
                    @Search::text IS NULL OR @Search::text = ''
                    OR COALESCE(""Name"", '') ILIKE '%' || @Search::text || '%'
                    OR COALESCE(""Referral COde"", '') ILIKE '%' || @Search::text || '%'
                    OR CAST(""Afiiliate_ID"" AS text) ILIKE '%' || @Search::text || '%'
                )
                ORDER BY ""Name"" ASC NULLS LAST
                LIMIT 10000";

            await using var connection = new NpgsqlConnection(_connectionString);
            var rows = (await connection.QueryAsync<AffiliatePerformanceRow>(
                new CommandDefinition(
                    sql,
                    new { Search = search },
                    cancellationToken: cancellationToken))).ToList();

            // Do not include Password or other unapproved fields in this export model.
            if (format == "csv")
            {
                var csv = BuildCsv(rows);
                return File(
                    new UTF8Encoding(true).GetBytes(csv),
                    "text/csv; charset=utf-8",
                    $"affiliate-performance-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }

            if (format == "xlsx")
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Affiliate Performance");

                worksheet.Cell(1, 1).InsertTable(rows.Select(x => new
                {
                    x.AffiliateId,
                    x.Name,
                    x.ReferralCode,
                    x.TotalSchools,
                    x.TotalStudents,
                    x.ActiveStudents,
                    x.TotalTeachers,
                    x.ActiveTeachers,
                    x.TotalSelectedProjects,
                    x.FamilyProjects,
                    x.IndividualProjects,
                    x.GroupPbl
                }));

                worksheet.Columns().AdjustToContents();
                worksheet.SheetView.FreezeRows(1);

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"affiliate-performance-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
            }

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Header()
                        .Text("Affiliate Performance Report")
                        .FontSize(18)
                        .Bold();

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            for (int i = 0; i < 12; i++)
                                columns.RelativeColumn();
                        });

                        string[] headers =
                        {
                            "ID", "Affiliate", "Referral Code", "Schools",
                            "Students", "Active Students", "Teachers",
                            "Active Teachers", "Projects", "Family",
                            "Individual", "Group PBL"
                            };

                        table.Header(header =>
                        {
                            foreach (var heading in headers)
                            {
                                header.Cell()
                                    .Background(Colors.Grey.Lighten2)
                                    .Padding(3)
                                    .Text(heading).Bold();
                            }
                        });

                        foreach (var row in rows)
                        {
                            object[] values =
                            {
                                row.AffiliateId, row.Name, row.ReferralCode,
                                row.TotalSchools, row.TotalStudents, row.ActiveStudents,
                                row.TotalTeachers, row.ActiveTeachers,
                                row.TotalSelectedProjects, row.FamilyProjects,
                                row.IndividualProjects, row.GroupPbl
                                };

                            foreach (var value in values)
                            {
                                table.Cell().Padding(3)
                                    .Text(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                            }
                        }
                    });

                    page.Footer().AlignRight()
                        .Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                });
            }).GeneratePdf();

            return File(
                pdf,
                "application/pdf",
                $"affiliate-performance-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
        }

        private static string BuildCsv(IEnumerable<AffiliatePerformanceRow> rows)
        {
            var builder = new StringBuilder();
            var headers = new[]
            {
                "Affiliate ID", "Name", "Referral Code", "Total Schools",
                "Total Students", "Active Students", "Total Teachers",
                "Active Teachers", "Total Selected Projects", "Family Projects",
                "Individual Projects", "Group PBL"
            };

            builder.AppendLine(string.Join(",", headers.Select(CsvEscape)));

            foreach (var row in rows)
            {
                var values = new[]
                {
                    row.AffiliateId.ToString(CultureInfo.InvariantCulture),
                    row.Name,
                    row.ReferralCode,
                    row.TotalSchools.ToString(CultureInfo.InvariantCulture),
                    row.TotalStudents.ToString(CultureInfo.InvariantCulture),
                    row.ActiveStudents.ToString(CultureInfo.InvariantCulture),
                    row.TotalTeachers.ToString(CultureInfo.InvariantCulture),
                    row.ActiveTeachers.ToString(CultureInfo.InvariantCulture),
                    row.TotalSelectedProjects.ToString(CultureInfo.InvariantCulture),
                    row.FamilyProjects.ToString(CultureInfo.InvariantCulture),
                    row.IndividualProjects.ToString(CultureInfo.InvariantCulture),
                    row.GroupPbl.ToString(CultureInfo.InvariantCulture)
                };

                builder.AppendLine(string.Join(",", values.Select(CsvEscape)));
            }

            return builder.ToString();
        }

        private static string CsvEscape(string? value)
        {
            value ??= "";

            // Prevent spreadsheet formula injection in user-controlled text cells.
            if (value.Length > 0 && "=+-@".Contains(value[0]))
                value = "'" + value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }

    public sealed class AffiliatePerformanceRow
    {
        public long AffiliateId { get; set; }
        public string Name { get; set; } = "";
        public string ReferralCode { get; set; } = "";
        public long TotalSchools { get; set; }
        public long TotalStudents { get; set; }
        public long ActiveStudents { get; set; }
        public long TotalTeachers { get; set; }
        public long ActiveTeachers { get; set; }
        public long TotalSelectedProjects { get; set; }
        public long FamilyProjects { get; set; }
        public long IndividualProjects { get; set; }
        public long GroupPbl { get; set; }
    }

    public sealed class AffiliatePerformanceSummary
    {
        public long TotalAffiliates { get; set; }
        public long TotalSchools { get; set; }
        public long TotalStudents { get; set; }
        public long ActiveStudents { get; set; }
        public long TotalTeachers { get; set; }
        public long TotalProjects { get; set; }
    }

    public sealed class AffiliatePerformanceDashboardViewModel
    {
        public string? Search { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }
        public string SortBy { get; set; } = "Name";
        public string SortDir { get; set; } = "asc";
        public List<AffiliatePerformanceRow> Rows { get; set; } = new();
        public AffiliatePerformanceSummary Summary { get; set; } = new();
        public string ChartLabelsJson { get; set; } = "[]";
        public string ChartStudentsJson { get; set; } = "[]";
        public string ChartProjectsJson { get; set; } = "[]";
        public string ChartProjectTypesJson { get; set; } = "[]";

        public int TotalPages => (int)Math.Max(
            1, Math.Ceiling((double)TotalCount / Math.Max(PageSize, 1)));
    }

}