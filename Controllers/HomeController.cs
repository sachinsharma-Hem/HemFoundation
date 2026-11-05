using ClosedXML.Excel;
using HemFoundation.Common;
using HemFoundation.Data; // Apne ApplicationDbContext ka namespace
using HemFoundation.DTOs;
using HemFoundation.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using System.Diagnostics;
namespace HemFoundation.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public HomeController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<IActionResult> Index()
        {
            var model = new HemHomeModel();

            try
            {
                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                SELECT 
                    COUNT(CASE WHEN ""UserRoles"" LIKE '%WJ_Students%' THEN 1 END)+158782 AS StudentCounts,
                    COUNT(CASE WHEN ""UserRoles"" LIKE '%WJ_Teacher%' THEN 1 END)+1744 AS TeacherCounts,
                    COUNT(CASE WHEN ""UserRoles"" LIKE '%WJ_School%' THEN 1 END)+1011 AS SchoolCounts
                FROM public.hf_view_users;";

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            model.StudentCounts = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader["StudentCounts"]);
                            model.TeacherCounts = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader["TeacherCounts"]);
                            model.SchoolCounts = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader["SchoolCounts"]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                model.StudentCounts = 500438;
                model.TeacherCounts = 15564;
                model.SchoolCounts = 7670;
                System.Diagnostics.Debug.WriteLine("Counter fetch error: " + ex.Message);
            }

            return View(model);
        }

        public IActionResult FAQ() => View();
        [HttpGet("home/terms")]
        [HttpGet("home/PrivacyAndTerms")]
        public IActionResult PrivacyAndTerms() => View();
        public IActionResult RulesRegulations() => View();

        [HttpGet("Home/unsubscribe")]
        [HttpGet("Home/Unsubscriber")]

        public IActionResult Unsubscriber() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

            // Retrieve the exact exception that occurred anywhere in the app
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

            if (exceptionFeature?.Error != null)
            {
                var ex = exceptionFeature.Error;

                var log = new ErrorLog
                {
                    RequestId = requestId,
                    RequestPath = exceptionFeature.Path, // URL where crash happened
                    HttpMethod = HttpContext.Request.Method,
                    Message = ex.Message,
                    ExceptionType = ex.GetType().FullName,
                    StackTrace = ex.StackTrace,
                    InnerException = ex.InnerException?.ToString(),
                    UserIdentifier = User.Identity?.IsAuthenticated == true ? User.Identity.Name : "Anonymous",
                    CreatedAtUtc = DateTime.UtcNow
                };

                _context.ErrorLogs.Add(log);
                await _context.SaveChangesAsync();
            }

            return View(new ErrorViewModel { RequestId = requestId });
        }
        public async Task<IActionResult> ErrorLogs(string? search)
        {
            var query = _context.ErrorLogs.AsNoTracking().OrderByDescending(e => e.CreatedAtUtc).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(e =>
                    e.RequestId.Contains(search) ||
                    (e.Message != null && e.Message.Contains(search)) ||
                    (e.RequestPath != null && e.RequestPath.Contains(search)));
            }

            var logs = await query.Take(100).ToListAsync();
            ViewBag.Search = search;
            return View(logs);
        }

        [HttpGet]
        public IActionResult CommonReports()
        {
            SqlQueryModel sqlQueryModel = SqlQuerylist();
            return View(sqlQueryModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExportToExcel(string Query)
        {
            if (string.IsNullOrEmpty(Query))
                return Content("Please select a query.");

            DataTable dt = new DataTable();
            string connString = _configuration.GetConnectionString("DefaultConnection");

            using (var con = new NpgsqlConnection(connString))
            using (var cmd = new NpgsqlCommand(Query, con))
            {
                // In PostgreSQL, use CommandType.Text for queries or functions returning tables
                cmd.CommandType = CommandType.Text;
                using (var da = new NpgsqlDataAdapter(cmd))
                {
                    da.Fill(dt);
                }
            }

            using (var wb = new XLWorkbook())
            {
                wb.Worksheets.Add(dt, "Report");

                using (var stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    stream.Position = 0;
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"{Query}_Report.xlsx");
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CommonReports(SqlQueryModel sqlQueryModel)
        {
            SqlQueryModel model = SqlQuerylist();
            model.Query = sqlQueryModel?.Query;

            if (string.IsNullOrWhiteSpace(model.Query))
            {
                model.Message = "Please enter a SQL query.";
                return View(model);
            }

            try
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (var conn = new NpgsqlConnection(connectionString))
                using (var cmd = new NpgsqlCommand(model.Query, conn))
                {
                    cmd.CommandTimeout = 180;
                    conn.Open();
                    var sql = model.Query.TrimStart().ToLowerInvariant();

                    if (sql.StartsWith("select") || sql.StartsWith("with"))
                    {
                        using (var da = new NpgsqlDataAdapter(cmd))
                        {
                            var dt = new DataTable();
                            da.Fill(dt);
                            model.Results = dt;
                        }
                    }
                    else
                    {
                        int affected = cmd.ExecuteNonQuery();
                        model.Message = $"Query executed successfully. Rows affected: {affected}";
                    }
                }
            }
            catch (Exception ex)
            {
                model.Message = "Error: " + ex.Message;
            }

            return View(model);
        }

        public SqlQueryModel SqlQuerylist()
        {
            SqlQueryModel sqlQueryModel = new SqlQueryModel();
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            // Assuming HF_SP_GetSqlQueryList is a PostgreSQL function/procedure returning a result set
            // In PostgreSQL, stored procedures/functions are often queried via SELECT * FROM function_name()
            string query = "SELECT * FROM HF_SP_GetSqlQueryList()";

            using (var conn = new NpgsqlConnection(connectionString))
            using (var cmd = new NpgsqlCommand(query, conn))
            {
                conn.Open();
                using (var da = new NpgsqlDataAdapter(cmd))
                {
                    var ds = new DataSet();
                    da.Fill(ds);
                    if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    {
                        var dt = ds.Tables[0];
                        // Adapt your CommonMethod conversion logic to .NET Core
                        sqlQueryModel.sqlQuerylists = Utility.ConvertToList1<SqlQuerylist>(dt);
                    }
                }
            }
            return sqlQueryModel;
        }
    }
}