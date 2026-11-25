using Dapper;
using HemFoundation.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PuppeteerSharp;

namespace HemFoundation.Controllers
{
    public class SurveyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public SurveyController(IConfiguration configuration, ApplicationDbContext context)
        {
            _context = context;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }
        public IActionResult SurveyReport_stdwise1()
        {
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> SurveyReports_new()
        {
            try
            {
                // "Week 1", "Week 4" and anything containing "survey" (case-insensitive).
                // The exact-name matches for Pre/Post Value(s) Survey are already covered by "survey".
                ViewBag.CourseList = await _context.Courses
                    .AsNoTracking()
                    .Where(c => EF.Functions.ILike(c.CourseName, "%Week 1%")
                             || EF.Functions.ILike(c.CourseName, "%Week 4%")
                             || EF.Functions.ILike(c.CourseName, "%survey%"))
                    .OrderBy(c => c.CourseName)
                    .Select(c => new SelectListItem
                    {
                        Text = c.CourseName,
                        Value = c.CourseID.ToString(),
                        Selected = false
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                //_logger.LogError(
                //   ex,
                //   "Error generating survey report for course {Course}",
                //   request?.MainCourseName);
                // _logger.LogError(ex, "Failed to load survey courses");
                ViewBag.CourseList = new List<SelectListItem>();
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSurveyData(int courseId)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);

                var rows = await conn.QueryAsync<SurveyReportModel>(
                    "SELECT * FROM survey_report(@courseId)",
                    new { courseId });

                return Json(rows);
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Failed to load survey data for course {CourseId}", courseId);
                return StatusCode(500, "Failed to fetch survey data.");
            }
        }
    }
}
