using HemFoundation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text.RegularExpressions;

namespace HemFoundation.Controllers.Api
{
    /// <summary>
    /// "Schedule a Live Demo" form on the home page: e-mails the request to the admin address
    /// set in appsettings (AdminEmail / AdminEmailId, falling back to EmailFrom).
    /// </summary>
    [Route("api/demo-request")]
    [ApiController]
    public class DemoRequestApiController : ControllerBase
    {
        private const int MaxRequestsPerHour = 5;

        // Key names accepted for the admin address, first match wins.
        private static readonly string[] AdminEmailKeys =
        {
            "AdminEmail", "AdminEmailId", "AdminEmailID", "AdminEmailid",
            "AppSettings:AdminEmail", "AppSettings:AdminEmailId", "AppSettings:AdminEmailID", "AppSettings:AdminEmailid",
            "EmailFrom"
        };

        private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DemoRequestApiController> _logger;

        public DemoRequestApiController(IConfiguration configuration, IMemoryCache cache, ILogger<DemoRequestApiController> logger)
        {
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
        }

        public class DemoRequestDto
        {
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public string? School { get; set; }
            public string? Message { get; set; }
            public string? Website { get; set; }   // hidden "honeypot" field: people leave it empty, bots fill it
        }

        private string? AdminEmail()
        {
            foreach (var key in AdminEmailKeys)
            {
                var value = _configuration[key];
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return null;
        }

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] DemoRequestDto model)
        {
            if (model == null) return BadRequest(new { success = false, message = "Invalid request." });

            // Bots that fill the hidden field get a normal answer but nothing is sent.
            if (!string.IsNullOrEmpty(model.Website)) return Ok(new { success = true });

            string name = (model.Name ?? "").Trim();
            string email = (model.Email ?? "").Trim();
            string phone = (model.Phone ?? "").Trim();
            string school = (model.School ?? "").Trim();
            string message = (model.Message ?? "").Trim();

            string? error =
                name.Length < 2 || name.Length > 100 ? "Please enter your full name." :
                email.Length > 150 || !EmailPattern.IsMatch(email) ? "Please enter a valid email address." :
                Regex.Replace(phone, @"[\s\-+()]", "") is var digits && (digits.Length < 10 || digits.Length > 13 || !digits.All(char.IsDigit)) ? "Please enter a valid phone number." :
                school.Length < 2 || school.Length > 150 ? "Please enter your school or institution name." :
                message.Length > 1000 ? "The message must be 1000 characters or fewer." : null;

            if (error != null) return BadRequest(new { success = false, message = error });

            // A few requests per hour from one address, to stop abuse of the mail account.
            string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            string key = "demo-request:" + ip;
            int count = _cache.TryGetValue(key, out int c) ? c : 0;
            if (count >= MaxRequestsPerHour)
                return StatusCode(429, new { success = false, message = "Too many requests. Please try again after some time or call us." });
            _cache.Set(key, count + 1, TimeSpan.FromHours(1));

            string? to = AdminEmail();
            if (string.IsNullOrEmpty(to))
            {
                _logger.LogError("Demo request not sent: no admin e-mail address in appsettings.");
                return StatusCode(500, new { success = false, message = "We could not send your request right now. Please call us or try again later." });
            }

            string subject = $"Live demo request – {Shorten(school, 60)}";
            string body = BuildBody(name, email, phone, school, message);

            try
            {
                // EmailManager sends synchronously; keep the request thread free while it does.
                await Task.Run(() => EmailManager.SendEmailAws(to, subject, body, null, "", ""));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Demo request e-mail could not be sent to {To}", to);
                return StatusCode(500, new { success = false, message = "We could not send your request right now. Please call us or try again later." });
            }

            return Ok(new { success = true, message = "Thank you for scheduling a live demo. Our value education team will contact you within 24 hours." });
        }

        private static string Shorten(string s, int max) => s.Length <= max ? s : s[..max] + "…";

        private static string BuildBody(string name, string email, string phone, string school, string message)
        {
            string E(string s) => WebUtility.HtmlEncode(s);
            string row(string label, string value) =>
                $"<tr><td style=\"padding:8px 12px;color:#6b7280;font-weight:600;width:170px;vertical-align:top\">{label}</td>" +
                $"<td style=\"padding:8px 12px;color:#111827\">{value}</td></tr>";

            string when = DateTime.Now.ToString("dd MMM yyyy, hh:mm tt");
            string msg = string.IsNullOrEmpty(message) ? "<span style=\"color:#9ca3af\">—</span>" : E(message).Replace("\n", "<br>");

            return $@"<div style=""font-family:Arial,Helvetica,sans-serif;max-width:620px;margin:0 auto;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden"">
  <div style=""background:linear-gradient(90deg,#4B2E83,#E51E86);color:#fff;padding:18px 22px"">
    <div style=""font-size:12px;letter-spacing:1px;text-transform:uppercase;opacity:.9"">HEM Virtues · Website</div>
    <div style=""font-size:20px;font-weight:700;margin-top:4px"">New live demo request</div>
  </div>
  <table style=""width:100%;border-collapse:collapse;font-size:14px"">
    {row("Name", E(name))}
    {row("School / Institution", E(school))}
    {row("Email", $"<a href=\"mailto:{E(email)}\">{E(email)}</a>")}
    {row("Phone", E(phone))}
    {row("Message", msg)}
    {row("Received", E(when))}
  </table>
  <div style=""padding:14px 22px;background:#f9fafb;color:#6b7280;font-size:12px"">Sent from the “Schedule a Live Demo” form on the HEM Virtues home page. Reply to the email address above to contact the school.</div>
</div>";
        }
    }
}
