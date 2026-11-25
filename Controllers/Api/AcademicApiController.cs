using HemFoundation.Services.Academic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HemFoundation.Controllers.Api
{
    /// <summary>Academic module API for a school (or an admin with ?instituteId=).</summary>
    [Route("api/academic")]
    [ApiController]
    public class AcademicApiController : ControllerBase, IAsyncActionFilter
    {
        private readonly AcademicService _academic;
        private readonly ILogger<AcademicApiController> _logger;
        private AcademicAccess _access = new();

        public AcademicApiController(AcademicService academic, ILogger<AcademicApiController> logger)
        {
            _academic = academic;
            _logger = logger;
        }

        private int Inst => _access.InstituteId;
        private IActionResult Fail(int status, string message) => StatusCode(status, new { success = false, message });

        [NonAction]
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            _access = AcademicAccess.From(HttpContext);
            if (!_access.LoggedIn) { context.Result = Fail(401, "Your session has expired. Please log in again."); return; }
            if (_access.InstituteId == 0) { context.Result = Fail(403, "Academics is available to school accounts."); return; }
            if (!HttpMethods.IsGet(Request.Method) && !Request.Headers.ContainsKey("X-Requested-With")) { context.Result = Fail(400, "Invalid request."); return; }
            if (!await _academic.TablesExistAsync()) { context.Result = Fail(503, "The academic tables are not set up yet. Run Data/Scripts/RunOnce_Academic_2026_11_26.sql."); return; }

            try { await next(); }
            catch (AcademicService.AcademicException ex) { context.Result = Fail(400, ex.Message); }
        }

        // The access check above runs before every action (the controller is its own action filter).
        private async Task<IActionResult> Run(Func<Task<IActionResult>> work)
        {
            try { return await work(); }
            catch (AcademicService.AcademicException ex) { return Fail(400, ex.Message); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Academic API error");
                // On a developer machine show the real cause, so a database difference is easy to spot.
                var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                return Fail(500, env.IsDevelopment() ? "Server error: " + ex.GetBaseException().Message : "Something went wrong. Please try again.");
            }
        }

        // ---------------- overview / lookups ----------------
        [HttpGet("overview")]
        public Task<IActionResult> Overview() => Run(async () => Ok(new
        {
            success = true,
            overview = await _academic.OverviewAsync(Inst),
            exams = await _academic.ExamsAsync(Inst),
            standards = await _academic.StandardsAsync(Inst),
            subjects = await _academic.SubjectsAsync(Inst),
            terms = await _academic.TermsAsync(Inst)
        }));

        // ---------------- subjects ----------------
        public class SubjectDto { public int? Id { get; set; } public string? Name { get; set; } public string? Code { get; set; } }

        [HttpPost("subjects")]
        public Task<IActionResult> SaveSubject([FromBody] SubjectDto m) => Run(async () =>
        {
            int id = await _academic.SaveSubjectAsync(Inst, m?.Id, m?.Name ?? "", m?.Code, _access.UserId);
            return Ok(new { success = true, id, message = m?.Id > 0 ? "Subject updated." : "Subject added." });
        });

        [HttpDelete("subjects/{id:int}")]
        public Task<IActionResult> RemoveSubject(int id) => Run(async () =>
        {
            await _academic.RemoveSubjectAsync(Inst, id);
            return Ok(new { success = true, message = "Subject removed from the list. Exams already set up keep it." });
        });

        // ---------------- terms ----------------
        public class TermDto { public int? Id { get; set; } public string? Name { get; set; } public string? Year { get; set; } }

        [HttpPost("terms")]
        public Task<IActionResult> SaveTerm([FromBody] TermDto m) => Run(async () =>
        {
            int id = await _academic.SaveTermAsync(Inst, m?.Id, m?.Name ?? "", m?.Year ?? "", _access.UserId);
            return Ok(new { success = true, id, message = m?.Id > 0 ? "Term updated." : "Term added." });
        });

        [HttpDelete("terms/{id:int}")]
        public Task<IActionResult> RemoveTerm(int id) => Run(async () =>
        {
            await _academic.RemoveTermAsync(Inst, id);
            return Ok(new { success = true, message = "Term removed." });
        });

        // ---------------- exams ----------------
        public class ExamDto { public int TermId { get; set; } public string? Standard { get; set; } public List<int>? SubjectIds { get; set; } public decimal MaxMarks { get; set; } public decimal PassingMarks { get; set; } }

        /// <summary>One rule per subject chosen (the old form did one subject at a time).</summary>
        [HttpPost("exams")]
        public Task<IActionResult> SaveExam([FromBody] ExamDto m) => Run(async () =>
        {
            if (m?.SubjectIds == null || m.SubjectIds.Count == 0) return Fail(400, "Choose at least one subject.");
            int added = 0, updated = 0;
            foreach (var subjectId in m.SubjectIds.Distinct())
            {
                var r = await _academic.SaveExamAsync(Inst, m.TermId, m.Standard ?? "", subjectId, m.MaxMarks, m.PassingMarks);
                if (r.Updated) updated++; else added++;
            }
            string msg = (added > 0 ? $"{added} exam{(added == 1 ? "" : "s")} added" : "") + (added > 0 && updated > 0 ? ", " : "") + (updated > 0 ? $"{updated} updated" : "") + ".";
            return Ok(new { success = true, message = char.ToUpper(msg[0]) + msg[1..] });
        });

        [HttpDelete("exams/{id:int}")]
        public Task<IActionResult> RemoveExam(int id) => Run(async () =>
        {
            await _academic.RemoveExamAsync(Inst, id);
            return Ok(new { success = true, message = "Exam removed." });
        });

        // ---------------- marks ----------------
        [HttpGet("exams/{id:int}/marks")]
        public Task<IActionResult> Marks(int id) => Run(async () => Ok(new
        {
            success = true,
            exam = await _academic.ExamAsync(Inst, id) ?? throw new AcademicService.AcademicException("Exam not found."),
            students = await _academic.MarksAsync(Inst, id)
        }));

        [HttpPost("exams/{id:int}/marks")]
        public Task<IActionResult> SaveMarks(int id, [FromBody] List<AcademicService.MarkInput> rows) => Run(async () =>
        {
            var students = (await _academic.MarksAsync(Inst, id)).ToDictionary(s => s.StudentId, s => s.Name);
            var list = (rows ?? new()).Select(r => (students.TryGetValue(r.StudentId, out var n) ? n : "Student " + r.StudentId, r)).ToList();
            var result = await _academic.SaveMarksAsync(Inst, id, list, _access.UserId);
            return result.Success
                ? Ok(new { success = true, message = $"Marks saved for {result.Saved} student{(result.Saved == 1 ? "" : "s")}." })
                : BadRequest(new { success = false, message = "Nothing was saved. Please correct these rows.", errors = result.Errors });
        });

        [HttpGet("exams/{id:int}/template")]
        public Task<IActionResult> Template(int id) => Run(async () =>
        {
            var (bytes, name) = await _academic.TemplateAsync(Inst, id);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
        });

        [HttpPost("exams/{id:int}/upload")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public Task<IActionResult> Upload(int id, IFormFile? file) => Run(async () =>
        {
            if (file == null || file.Length == 0) return Fail(400, "Choose the filled Excel template.");
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return Fail(400, "Only .xlsx files can be uploaded.");
            if (file.Length > 5 * 1024 * 1024) return Fail(400, "The file must be 5 MB or smaller.");

            await using var stream = file.OpenReadStream();
            var result = await _academic.UploadAsync(Inst, id, stream, _access.UserId);
            return result.Success
                ? Ok(new { success = true, message = $"Upload complete. Marks saved for {result.Saved} student{(result.Saved == 1 ? "" : "s")}." })
                : BadRequest(new { success = false, message = "Nothing was saved. Please correct these rows and upload again.", errors = result.Errors });
        });

        // ---------------- report cards ----------------
        [HttpGet("students")]
        public Task<IActionResult> Students([FromQuery] string? standard) => Run(async () => Ok(new
        {
            success = true,
            students = await _academic.StudentsAsync(Inst, standard)
        }));
    }
}
