using HemFoundation.Data;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Route("api/admin")]
    [ApiController]
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Example API endpoint to fetch dashboard statistics or authenticated user info securely
        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized(new { success = false, message = "Unauthorized access." });

            var totalUsers = await _userManager.Users.CountAsync();
            var totalMenus = await _context.Menus.CountAsync();

            return Ok(new
            {
                success = true,
                stats = new
                {
                    totalUsers,
                    totalMenus,
                    serverTime = DateTime.Now
                }
            });
        }
    }
}