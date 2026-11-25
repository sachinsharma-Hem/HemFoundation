using HemFoundation.Services.Parent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HemFoundation.Controllers.Api
{
    /// <summary>Parent portal API: the logged-in parent's children (an admin can pass ?parentId=).</summary>
    [Route("api/parent")]
    [ApiController]
    public class ParentApiController : ControllerBase, IAsyncActionFilter
    {
        private readonly ParentService _parent;
        private readonly ILogger<ParentApiController> _logger;
        private ParentAccess _access = new();

        public ParentApiController(ParentService parent, ILogger<ParentApiController> logger)
        {
            _parent = parent;
            _logger = logger;
        }

        private IActionResult Fail(int status, string message) => StatusCode(status, new { success = false, message });

        [NonAction]
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            _access = ParentAccess.From(HttpContext);
            if (!_access.LoggedIn) { context.Result = Fail(401, "Your session has expired. Please log in again."); return; }
            if (_access.ParentId == 0) { context.Result = Fail(403, "This is available to parent accounts."); return; }
            if (!HttpMethods.IsGet(Request.Method) && !Request.Headers.ContainsKey("X-Requested-With")) { context.Result = Fail(400, "Invalid request."); return; }
            if (!await _parent.TableExistsAsync()) { context.Result = Fail(503, "The parent table is not set up yet. Run Data/Scripts/RunOnce_Parent_2026_11_27.sql."); return; }
            await next();
        }

        private async Task<IActionResult> Run(Func<Task<IActionResult>> work)
        {
            try { return await work(); }
            catch (ParentService.ParentException ex) { return Fail(400, ex.Message); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Parent API error");
                var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                return Fail(500, env.IsDevelopment() ? "Server error: " + ex.GetBaseException().Message : "Something went wrong. Please try again.");
            }
        }

        /// <summary>Children already linked (GetMappedChildren).</summary>
        [HttpGet("children")]
        public Task<IActionResult> Children() => Run(async () =>
            Ok(new { success = true, children = await _parent.ChildrenAsync(_access.ParentId) }));

        /// <summary>Students registered with the parent's mobile number and not linked yet (GetUnMappedChildren).</summary>
        [HttpGet("children/find")]
        public Task<IActionResult> Find() => Run(async () =>
        {
            var (mobile, list) = await _parent.FindChildrenAsync(_access.ParentId);
            string masked = string.IsNullOrEmpty(mobile) || mobile.Length < 4 ? (mobile ?? "") : new string('•', mobile.Length - 4) + mobile[^4..];
            return Ok(new { success = true, mobile = masked, hasMobile = !string.IsNullOrEmpty(mobile), children = list });
        });

        /// <summary>Link a child (MapChild).</summary>
        [HttpPost("children/{id:int}")]
        public Task<IActionResult> Map(int id) => Run(async () =>
        {
            var child = await _parent.MapChildAsync(_access.ParentId, id);
            return Ok(new { success = true, message = $"{child.Name} has been added to your children." });
        });

        /// <summary>Progress of one linked child (BindDashBoard + GetStudentsCourseData).</summary>
        [HttpGet("children/{id:int}/dashboard")]
        public Task<IActionResult> Dashboard(int id, bool fresh = false) => Run(async () =>
        {
            if (!await _parent.IsMappedAsync(_access.ParentId, id)) return Fail(403, "This child is not linked to your account.");
            var d = await _parent.DashboardAsync(id, fresh);
            if (d == null) return Fail(404, "Student not found.");
            return Ok(new
            {
                success = true,
                child = d.Child,
                summary = new
                {
                    d.TotalCourses, d.CoursesDone, d.CoursesInProgress, d.CoursesNotStarted, d.Certificates,
                    d.TotalMarks, d.MarksGained, d.Mcq, d.Practical, d.FamilyProjects, d.GroupProjects, d.Surveys,
                    d.ClassRank, d.ClassSize, d.GeneratedAt
                },
                courses = d.Courses.Select(x => new
                {
                    x.CourseName, x.Mcq, x.Practical, x.FamilyProject, x.GroupProject, x.Survey,
                    x.McqPoints, x.PracticalPoints, x.PblPoints, x.Marks, maxMarks = ParentService.CourseRow.MaxMarks,
                    x.Certified, x.Status, x.LastActivity
                }),
                recent = d.Recent
            });
        });
    }
}
