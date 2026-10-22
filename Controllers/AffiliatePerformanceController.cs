using ClosedXML.Excel;
using Dapper;
using Microsoft.AspNetCore.Mvc;
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
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' was not configured.");

            QuestPDF.Settings.License = LicenseType.Community;
        }

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
            FROM public.hf_view_Affiliate_PerformanceReport";

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAffiliateData(CancellationToken cancellationToken = default)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var rows = (await connection.QueryAsync<AffiliatePerformanceRow>(
                new CommandDefinition(BaseSql + " ORDER BY \"Name\" ASC", cancellationToken: cancellationToken))).ToList();

            var summary = new AffiliatePerformanceSummary
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

            var responseDto = new AffiliateApiResponse
            {
                Rows = rows,
                Summary = summary
            };

            return Json(responseDto);
        }

        [HttpGet]
        public async Task<IActionResult> Export(
            string format,
            CancellationToken cancellationToken = default)
        {
            format = format?.Trim().ToLowerInvariant() ?? "";

            if (format is not ("xlsx" or "csv" or "pdf"))
                return BadRequest("Supported formats are xlsx, csv and pdf.");

            await using var connection = new NpgsqlConnection(_connectionString);
            var rows = (await connection.QueryAsync<AffiliatePerformanceRow>(
                new CommandDefinition(BaseSql + " ORDER BY \"Name\" ASC", cancellationToken: cancellationToken))).ToList();

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
                    $"affiliate-performance-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
            }

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
                            "ID", "Affiliate", "Referral", "Mobile", "User", "Pass", "Email", "City",
                            "Schools", "Students", "Active S.", "Teachers", "Active T.",
                            "Projects", "Family", "Indiv.", "PBL"
                        };

                        table.Header(header =>
                        {
                            foreach (var heading in headers)
                            {
                                header.Cell()
                                    .Background(Colors.Grey.Lighten2)
                                    .Padding(2)
                                    .Text(heading).Bold();
                            }
                        });

                        foreach (var row in rows)
                        {
                            object[] values =
                            {
                                row.AffiliateId, row.Name, row.ReferralCode, row.MobileNo,
                                row.UserName, row.Password, row.EmailId, row.CityName,
                                row.TotalSchools, row.TotalStudents, row.ActiveStudents,
                                row.TotalTeachers, row.ActiveTeachers,
                                row.TotalSelectedProjects, row.FamilyProjects,
                                row.IndividualProjects, row.GroupPbl
                            };

                            foreach (var value in values)
                            {
                                table.Cell().Padding(2)
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
                "Affiliate ID", "Name", "Referral Code", "Mobile No", "User Name",
                "Password", "Email Id", "City Name", "Total Schools",
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

                builder.AppendLine(string.Join(",", values.Select(CsvEscape)));
            }

            return builder.ToString();
        }

        private static string CsvEscape(string? value)
        {
            value ??= "";
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