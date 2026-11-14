using HemFoundation.Services.HemSampark;
using Microsoft.AspNetCore.Mvc;

namespace HemFoundation.Controllers
{
    public class HemSamparkController : Controller
    {
        private readonly HemSamparkAccess _access;

        public HemSamparkController(HemSamparkAccess access)
        {
            _access = access;
        }

        // Home page with the HEM Sampark login modal opened.
        private IActionResult LoginRedirect() => Redirect("/?samparkLogin=1");

        [HttpGet("HemSampark/Admin")]
        public async Task<IActionResult> Admin()
        {
            if (_access.Current == null) return LoginRedirect();

            // Logged-in non-admins go to the user dashboard.
            if (!await _access.IsAdminAsync())
                return RedirectToAction(nameof(SamparkDashboard));

            return View();
        }

        [HttpGet("HemSampark/SamparkDashboard")]
        [HttpGet("hem-sampark/dashboard")]
        public IActionResult SamparkDashboard()
        {
            if (_access.Current == null) return LoginRedirect();

            return View("~/Views/HemSampark/Dashboard.cshtml");
        }
    }
}
