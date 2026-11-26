using HemFoundation.Services.HemStore;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    /// <summary>"Redeem" on the student dashboard: opens HEM Store with the student already logged in.</summary>
    [Route("HemStore")]
    public class HemStoreController : Controller
    {
        private readonly HemStoreBridge _bridge;
        private readonly ILogger<HemStoreController> _logger;

        public HemStoreController(HemStoreBridge bridge, ILogger<HemStoreController> logger)
        {
            _bridge = bridge;
            _logger = logger;
        }

        [HttpGet("Go")]
        public IActionResult Go(string? to = null)
        {
            var s = HttpContext.Session;
            if (s.GetString("IsAdminLoggedIn") != "true") return RedirectToAction("Index", "Home");

            string role = s.GetString("UserRole") ?? "";
            int studentId = s.GetInt32("UserID") ?? 0;
            if (!role.Equals("WJ_Students", StringComparison.OrdinalIgnoreCase) || studentId <= 0)
                return View("~/Views/Shared/HemStoreMessage.cshtml", "HEM Store is for student accounts. Please log in as a student.");

            if (!_bridge.IsConfigured)
            {
                _logger.LogWarning("HEM Store link is not configured (HemStore:StoreUrl / HemStore:SharedSecret).");
                return View("~/Views/Shared/HemStoreMessage.cshtml", "HEM Store is not available right now. Please try again later.");
            }

            // Only a known page of the store may be asked for (no open redirect).
            string page = to is "orders" or "wallet" ? to : "home";
            string ticket = _bridge.CreateTicket(studentId);
            return Redirect($"{_bridge.StoreUrl}/sso?ticket={Uri.EscapeDataString(ticket)}&to={page}");
        }
    }
}
