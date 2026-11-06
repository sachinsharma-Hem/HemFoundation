
using ClosedXML.Excel;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Npgsql;
using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HemFoundation.Controllers
{
    public class AffiliatePerformanceController : Controller
    {
        private readonly string _connectionString;

        public AffiliatePerformanceController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' was not configured.");

            QuestPDF.Settings.License = LicenseType.Community;
        }

        // =========================================================
        // CONFIGURATION
        // =========================================================

        private const string UserIdSessionKey = "UserID";

        // Actual Super Affiliate role
        private const string SuperAffiliateRole = "WJ_Affiliate";

        // Keep these aligned with your application's Admin roles.
        private static readonly string[] AdminRoles =
        {
            "WJ_Admin",
            "Admin",
            "Management"
        };

        // =========================================================
        // BASE SQL
        // Password is intentionally retained as requested.
        // =========================================================

        private const string BaseSql = @"
            SELECT
                ""Afiiliate_ID""::bigint AS ""AffiliateId"",
                COALESCE(""Name"", '')::text AS ""Name"",
                COALESCE(""Referral COde"", '')::text AS ""ReferralCode"",
                COALESCE(""Mobile No"", '')::text AS ""MobileNo"",
                COALESCE(""User Name"", '')::text AS ""UserName"",
                COALESCE(""Password"", '')::text AS ""Password"",
                COALESCE(""Email Id"", '')::text AS ""EmailId"",
                COALESCE(""City Name"", '')::text AS ""CityName"",
                COALESCE(""School Count"", 0)::bigint AS ""TotalSchools"",
                COALESCE(""Total Students"", 0)::bigint AS ""TotalStudents"",
                COALESCE(""Active Students"", 0)::bigint AS ""ActiveStudents"",
                COALESCE(""Total Teachers"", 0)::bigint AS ""TotalTeachers"",
                COALESCE(""Active Teachers"", 0)::bigint AS ""ActiveTeachers"",
                COALESCE(""Total Sel Project"", 0)::bigint AS ""TotalSelectedProjects"",
                COALESCE(""Family Project"", 0)::bigint AS ""FamilyProjects"",
                COALESCE(""Individual Project"", 0)::bigint AS ""IndividualProjects"",
                COALESCE(""Group Project"", 0)::bigint AS ""GroupPbl""
            FROM public.hf_view_affiliate_performancereport";

        // =========================================================
        // SESSION
        // =========================================================

        private long? GetLoggedInInstituteId()
        {
            // Session.SetInt32("UserID", instituteId)
            int? id = HttpContext.Session.GetInt32(UserIdSessionKey);

            if (id.HasValue && id.Value > 0)
                return id.Value;

            // Session.SetString("UserID", instituteId.ToString())
            string? stringId =
                HttpContext.Session.GetString(UserIdSessionKey);

            if (long.TryParse(stringId, out long instituteId)
                && instituteId > 0)
            {
                return instituteId;
            }

            return null;
        }

        private string GetLoggedInRole()
        {
            // Supports the application's Role session key.
            // UserRole is retained as a fallback.
            return
                HttpContext.Session.GetString("Role")
                ?? HttpContext.Session.GetString("UserRole")
                ?? "";
        }

        private bool IsAdmin()
        {
            string role = GetLoggedInRole().Trim();

            return AdminRoles.Contains(
                role,
                StringComparer.OrdinalIgnoreCase);
        }

        private bool IsSuperAffiliate()
        {
            return string.Equals(
                GetLoggedInRole().Trim(),
                SuperAffiliateRole,
                StringComparison.OrdinalIgnoreCase);
        }

        private bool HasReportAccess()
        {
            return IsAdmin() || IsSuperAffiliate();
        }

        // =========================================================
        // GET LOGGED-IN SUPER AFFILIATE REFERRAL CODE
        // =========================================================

        private async Task<string?> GetSuperAffiliateReferralCode(
            NpgsqlConnection connection,
            long instituteId,
            CancellationToken cancellationToken)
        {
            const string sql = @"
                SELECT NULLIF(TRIM(referralcode), '')
                FROM public.view_hf_affiliate
                WHERE instituteid = @InstituteId
                LIMIT 1;";

            return await connection.QueryFirstOrDefaultAsync<string>(
                new CommandDefinition(
                    sql,
                    new { InstituteId = instituteId },
                    cancellationToken: cancellationToken));
        }

        // =========================================================
        // GET FILTERED AFFILIATE DATA
        // =========================================================

        private async Task<List<AffiliatePerformanceRow>> GetAffiliateRows(
            NpgsqlConnection connection,
            CancellationToken cancellationToken)
        {
            // ADMIN: All affiliates
            if (IsAdmin())
            {
                var adminRows =
                    await connection.QueryAsync<AffiliatePerformanceRow>(
                        new CommandDefinition(
                            BaseSql + @" ORDER BY ""Name"" ASC",
                            cancellationToken: cancellationToken));

                return adminRows.ToList();
            }

            // SUPER AFFILIATE: Only own sub-affiliates
            if (IsSuperAffiliate())
            {
                long? instituteId = GetLoggedInInstituteId();

                if (!instituteId.HasValue)
                {
                    throw new UnauthorizedAccessException(
                        "Affiliate session is missing or expired.");
                }

                string? referralCode =
                    await GetSuperAffiliateReferralCode(
                        connection,
                        instituteId.Value,
                        cancellationToken);

                if (string.IsNullOrWhiteSpace(referralCode))
                    return new List<AffiliatePerformanceRow>();

                const string filterSql = @"
                    WHERE LOWER(TRIM(COALESCE(r.""ReferredBy"", '')))
                        = LOWER(TRIM(@ReferralCode))
                    ORDER BY r.""Name"" ASC;";

                var rows =
                    await connection.QueryAsync<AffiliatePerformanceRow>(
                        new CommandDefinition(
                            BaseSql.Replace(
                                "FROM public.hf_view_affiliate_performancereport",
                                "FROM public.hf_view_affiliate_performancereport r")
                            + filterSql,
                            new { ReferralCode = referralCode },
                            cancellationToken: cancellationToken));

                return rows.ToList();
            }

            throw new UnauthorizedAccessException(
                "You are not authorized to access this report.");
        }

        // =========================================================
        // SUMMARY
        // =========================================================

        private static AffiliatePerformanceSummary BuildSummary(
            List<AffiliatePerformanceRow> rows)
        {
            return new AffiliatePerformanceSummary
            {
                TotalAffiliates = rows.Count,
                TotalSchools = rows.Sum(x => x.TotalSchools),
                TotalStudents = rows.Sum(x => x.TotalStudents),
                ActiveStudents = rows.Sum(x => x.ActiveStudents),
                TotalTeachers = rows.Sum(x => x.TotalTeachers),
                ActiveTeachers = rows.Sum(x => x.ActiveTeachers),
                TotalProjects = rows.Sum(x => x.TotalSelectedProjects),
                FamilyProjects = rows.Sum(x => x.FamilyProjects),
                IndividualProjects = rows.Sum(x => x.IndividualProjects),
                GroupPbl = rows.Sum(x => x.GroupPbl)
            };
        }

        // =========================================================
        // INDEX
        // =========================================================

        [HttpGet]
        public IActionResult Index()
        {
            if (!HasReportAccess())
                return Forbid();

            // No frontend changes required.
            return View();
        }

        // =========================================================
        // GET AFFILIATE DATA
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> GetAffiliateData(
            CancellationToken cancellationToken = default)
        {
            if (!HasReportAccess())
                return Forbid();

            try
            {
                await using var connection =
                    new NpgsqlConnection(_connectionString);

                await connection.OpenAsync(cancellationToken);

                var rows = await GetAffiliateRows(
                    connection,
                    cancellationToken);

                var response = new AffiliateApiResponse
                {
                    Rows = rows,
                    Summary = BuildSummary(rows)
                };

                return Json(response);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    message = "Unable to load affiliate performance data."
                });
            }
        }

        // =========================================================
        // EXPORT
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Export(
            string format,
            CancellationToken cancellationToken = default)
        {
            if (!HasReportAccess())
                return Forbid();

            format = format?.Trim().ToLowerInvariant() ?? "";

            if (format is not ("xlsx" or "csv" or "pdf"))
            {
                return BadRequest(
                    "Supported formats are xlsx, csv and pdf.");
            }

            try
            {
                await using var connection =
                    new NpgsqlConnection(_connectionString);

                await connection.OpenAsync(cancellationToken);

                var rows = await GetAffiliateRows(
                    connection,
                    cancellationToken);

                string fileName =
                    $"affiliate-performance-{DateTime.UtcNow:yyyyMMddHHmmss}";

                // CSV
                if (format == "csv")
                {
                    string csv = BuildCsv(rows);

                    return File(
                        new UTF8Encoding(true).GetBytes(csv),
                        "text/csv; charset=utf-8",
                        fileName + ".csv");
                }

                // EXCEL
                if (format == "xlsx")
                {
                    using var workbook = new XLWorkbook();

                    var worksheet =
                        workbook.Worksheets.Add("Affiliate Performance");

                    worksheet.Cell(1, 1).InsertTable(
                        rows.Select(x => new
                        {
                            x.AffiliateId,
                            x.Name,
                            x.ReferralCode,
                            x.MobileNo,
                            x.UserName,
                            x.Password,
                            x.EmailId,
                            x.CityName,
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
                        fileName + ".xlsx");
                }

                // PDF
                var pdf = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(15);
                        page.DefaultTextStyle(x => x.FontSize(7));

                        page.Header()
                            .Text("Affiliate Performance Report")
                            .FontSize(16)
                            .Bold();

                        page.Content().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                for (int i = 0; i < 17; i++)
                                    columns.RelativeColumn();
                            });

                            string[] headers =
                            {
                                "ID", "Affiliate", "Referral", "Mobile",
                                "User", "Pass", "Email", "City",
                                "Schools", "Students", "Active S.",
                                "Teachers", "Active T.", "Projects",
                                "Family", "Indiv.", "PBL"
                            };

                            table.Header(header =>
                            {
                                foreach (var heading in headers)
                                {
                                    header.Cell()
                                        .Background(Colors.Grey.Lighten2)
                                        .Padding(2)
                                        .Text(heading)
                                        .Bold();
                                }
                            });

                            foreach (var row in rows)
                            {
                                object[] values =
                                {
                                    row.AffiliateId,
                                    row.Name,
                                    row.ReferralCode,
                                    row.MobileNo,
                                    row.UserName,
                                    row.Password,
                                    row.EmailId,
                                    row.CityName,
                                    row.TotalSchools,
                                    row.TotalStudents,
                                    row.ActiveStudents,
                                    row.TotalTeachers,
                                    row.ActiveTeachers,
                                    row.TotalSelectedProjects,
                                    row.FamilyProjects,
                                    row.IndividualProjects,
                                    row.GroupPbl
                                };

                                foreach (var value in values)
                                {
                                    table.Cell()
                                        .Padding(2)
                                        .Text(Convert.ToString(
                                            value,
                                            CultureInfo.InvariantCulture) ?? "");
                                }
                            }
                        });

                        page.Footer()
                            .AlignRight()
                            .Text(
                                $"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                    });
                }).GeneratePdf();

                return File(
                    pdf,
                    "application/pdf",
                    fileName + ".pdf");
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        // =========================================================
        // CSV BUILDER
        // =========================================================

        private static string BuildCsv(
            IEnumerable<AffiliatePerformanceRow> rows)
        {
            var builder = new StringBuilder();

            var headers = new[]
            {
                "Affiliate ID",
                "Name",
                "Referral Code",
                "Mobile No",
                "User Name",
                "Password",
                "Email Id",
                "City Name",
                "Total Schools",
                "Total Students",
                "Active Students",
                "Total Teachers",
                "Active Teachers",
                "Total Selected Projects",
                "Family Projects",
                "Individual Projects",
                "Group PBL"
            };

            builder.AppendLine(
                string.Join(",", headers.Select(CsvEscape)));

            foreach (var row in rows)
            {
                var values = new[]
                {
                    row.AffiliateId.ToString(CultureInfo.InvariantCulture),
                    row.Name,
                    row.ReferralCode,
                    row.MobileNo,
                    row.UserName,
                    row.Password,
                    row.EmailId,
                    row.CityName,
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

                builder.AppendLine(
                    string.Join(",", values.Select(CsvEscape)));
            }

            return builder.ToString();
        }

        private static string CsvEscape(string? value)
        {
            value ??= "";

            if (value.Length > 0 &&
                "=+-@".Contains(value[0]))
            {
                value = "'" + value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }

    // =============================================================
    // DTO
    // =============================================================

    public sealed class AffiliatePerformanceRow
    {
        public long AffiliateId { get; set; }

        public string Name { get; set; } = "";
        public string ReferralCode { get; set; } = "";
        public string MobileNo { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
        public string EmailId { get; set; } = "";
        public string CityName { get; set; } = "";

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
        public int TotalAffiliates { get; set; }
        public long TotalSchools { get; set; }
        public long TotalStudents { get; set; }
        public long ActiveStudents { get; set; }
        public long TotalTeachers { get; set; }
        public long ActiveTeachers { get; set; }
        public long TotalProjects { get; set; }
        public long FamilyProjects { get; set; }
        public long IndividualProjects { get; set; }
        public long GroupPbl { get; set; }
    }

    public sealed class AffiliateApiResponse
    {
        public List<AffiliatePerformanceRow> Rows { get; set; } = new();
        public AffiliatePerformanceSummary Summary { get; set; } = new();
    }
}
