using HemFoundation.Data; // Apne ApplicationDbContext ka namespace
using HemFoundation.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics;
namespace HemFoundation.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
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
        public IActionResult PrivacyAndTerms() => View();
        public IActionResult RulesRegulations() => View();
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
    }
}