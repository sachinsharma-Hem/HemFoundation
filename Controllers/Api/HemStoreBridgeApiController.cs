using HemFoundation.Data;
using HemFoundation.Models.Identity;
using HemFoundation.Services.HemStore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace HemFoundation.Controllers.Api
{
    /// <summary>
    /// Server-to-server API used only by the HEM Store website. Every request must be signed with
    /// the shared secret (see <see cref="HemStoreBridge.VerifyRequest"/>); browsers cannot call it.
    /// </summary>
    [Route("api/hemstore-bridge")]
    [ApiController]
    public class HemStoreBridgeApiController : ControllerBase
    {
        private readonly HemStoreBridge _bridge;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HemStoreBridgeApiController> _logger;

        public HemStoreBridgeApiController(HemStoreBridge bridge, ApplicationDbContext context, ILogger<HemStoreBridgeApiController> logger)
        {
            _bridge = bridge;
            _context = context;
            _logger = logger;
        }

        private IActionResult Fail(int status, string message) => StatusCode(status, new { success = false, message });

        private async Task<(bool ok, string body)> CheckSignatureAsync()
        {
            Request.EnableBuffering();
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
                body = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            bool ok = _bridge.VerifyRequest(Request.Headers["X-HemStore-Timestamp"], Request.Headers["X-HemStore-Signature"],
                Request.Method, Request.Path.Value ?? "", body);
            if (!ok) _logger.LogWarning("HEM Store bridge: rejected an unsigned or wrongly signed request to {Path}", Request.Path.Value);
            return (ok, body);
        }

        /// <summary>Student profile and total points (the dashboard's "Total Marks").</summary>
        [HttpGet("students/{id:int}")]
        public async Task<IActionResult> Student(int id)
        {
            if (!_bridge.IsConfigured) return Fail(503, "Not configured.");
            var (ok, _) = await CheckSignatureAsync();
            if (!ok) return Fail(401, "Invalid signature.");

            var p = await _bridge.StudentAsync(id);
            if (p == null) return Fail(404, "Student not found.");
            return Ok(new { success = true, student = p });
        }

        public class LoginDto { public string? Username { get; set; } public string? Password { get; set; } }

        /// <summary>
        /// Checks a student's HEM Virtues ID and password for the store's own login page.
        /// Same rules as the HEM Virtues login; the password is never stored by the store.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login()
        {
            if (!_bridge.IsConfigured) return Fail(503, "Not configured.");
            var (ok, body) = await CheckSignatureAsync();
            if (!ok) return Fail(401, "Invalid signature.");

            LoginDto? m;
            try { m = JsonSerializer.Deserialize<LoginDto>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch { return Fail(400, "Invalid request."); }
            string username = (m?.Username ?? "").Trim(), password = m?.Password ?? "";
            if (username.Length == 0 || password.Length == 0) return Fail(400, "Enter your ID and password.");

            const string wrong = "Invalid ID or password.";
            var user = await _context.Set<ApplicationUser>().AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);
            if (user == null) return Fail(401, wrong);
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
                return Fail(403, "Your account is locked. Please contact your school.");

            var result = new PasswordHasher<ApplicationUser>().VerifyHashedPassword(user, user.PasswordHash ?? "", password);
            if (result == PasswordVerificationResult.Failed) return Fail(401, wrong);

            bool isStudent = await (from ur in _context.UserRoles
                                    join r in _context.Roles on ur.RoleId equals r.Id
                                    where ur.UserId == user.Id && r.Name == "WJ_Students"
                                    select r.Id).AnyAsync();
            if (!isStudent) return Fail(403, "HEM Store is for student accounts.");

            int? studentId = await _context.Users.AsNoTracking().Where(u => u.AspNetUserID == user.Id).Select(u => (int?)u.UserID).FirstOrDefaultAsync();
            if (studentId == null) return Fail(401, wrong);

            var p = await _bridge.StudentAsync(studentId.Value);
            if (p == null || !p.IsActive) return Fail(403, "Your account is inactive. Please contact your school.");
            return Ok(new { success = true, student = p });
        }
    }
}
