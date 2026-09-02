using HemFoundation.Data;
using HemFoundation.DTOs;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HemFoundation.Controllers.Api
{
    [Route("api/auth")]
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthApiController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid payload or missing fields" });

            // 1. Strictly Login via Username and Password (Email check removed)
            string username = model.Email?.Trim() ?? ""; // Using model.Email property from DTO as the username/mobile input field

            if (string.IsNullOrEmpty(username))
                return BadRequest(new { success = false, message = "Username or Mobile Number is required." });

            ApplicationUser? user = await _userManager.FindByNameAsync(username);

            // Fallback: If exact username not found, search by phone number
            if (user == null)
            {
                user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == username);
            }

            if (user == null)
                return Unauthorized(new { success = false, message = "Invalid credentials or account inactive." });

            // 2. Verify Password using SignInManager
            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
                return Unauthorized(new { success = false, message = "Invalid username or password." });

            var roles = await _userManager.GetRolesAsync(user);
            var authClaims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Email, user.Email ?? ""),
        new Claim(ClaimTypes.Name, user.FullName ?? user.UserName ?? "")
    };

            foreach (var role in roles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var jwtKey = _configuration["JwtSettings:Secret"] ?? "HemFoundation_Super_Secret_Key_2026_Secure!";
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            int expiryHours = int.TryParse(_configuration["JwtSettings:ExpiryHours"], out var hours) ? hours : 8;

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"] ?? "HemFoundation",
                audience: _configuration["JwtSettings:Audience"] ?? "HemFoundationUsers",
                expires: DateTime.Now.AddHours(expiryHours),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            var roleIds = await _context.Roles
                .Where(r => roles.Contains(r.Name!))
                .Select(r => r.Id)
                .ToListAsync();

            var menuRights = await _context.RoleMenuRights
                .Where(rm => roleIds.Contains(rm.RoleId) && rm.CanView == true && rm.IsActive == true && rm.IsDeleted == false)
                .ToListAsync();

            var menuIds = menuRights.Select(rm => rm.MenuId).Distinct().ToList();

            string primaryRole = roles.FirstOrDefault() ?? "User";

            // 1. Check RoleDashboardMappings in Database
            var dashboardMapping = await _context.RoleDashboardMappings
                .FirstOrDefaultAsync(r => r.RoleName == primaryRole && r.IsActive);

            string redirectUrl = "";
            string dynamicDashboardController = "Home";

            if (dashboardMapping != null && !string.IsNullOrEmpty(dashboardMapping.RedirectUrl))
            {
                // Agar database me mapping milti hai
                redirectUrl = dashboardMapping.RedirectUrl;
            }
            else
            {
                // 2. Check if user exists in Admins table
                bool isAdminUser = await _context.Admins
                    .AnyAsync(a => a.IdentityUserId == user.Id || a.Email == user.Email);

                if (isAdminUser)
                {
                    // Agar wo Admins table me hai aur mapping nahi mili, toh seedha Admin Dashboard par bhejo
                    redirectUrl = "/Admin/Dashboard";
                }
                else
                {
                    // Baaki sabhi ke liye default fallback
                    redirectUrl = "/Home/Index";
                }
            }

            // Extract Controller name from redirectUrl for dynamic menus
            if (!string.IsNullOrEmpty(redirectUrl))
            {
                var parts = redirectUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    dynamicDashboardController = parts[0];
                }
            }

            var dbMenus = await _context.Menus
                .Where(m => menuIds.Contains(m.Id) && m.IsActive == true && m.IsDeleted == false)
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync();

            var menus = dbMenus.Select(m => new {
                m.Id,
                m.ParentId,
                MenuName = m.MenuName,
                Controller = m.MenuName == "Dashboard" ? dynamicDashboardController : m.Controller,
                Action = m.MenuName == "Dashboard" ? "Dashboard" : m.Action,
                RouteUrl = m.MenuName == "Dashboard" ? dashboardMapping?.RedirectUrl : m.RouteUrl,
                m.Icon,
                m.DisplayOrder,
                m.MenuType
            }).ToList();

            HttpContext.Session.SetString("IsAdminLoggedIn", "true");
            HttpContext.Session.SetString("UserId", user.Id);
            HttpContext.Session.SetString("UserEmail", user.Email ?? "");
            HttpContext.Session.SetString("UserFullName", user.FullName ?? user.UserName ?? "");
            HttpContext.Session.SetString("UserRole", primaryRole);

            return Ok(new
            {
                success = true,
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token.ValidTo,
                redirectUrl = redirectUrl,
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    Roles = roles
                },
                menus,
                rights = menuRights
            });
        }

        [HttpPost("logout")]
        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            // Clear C# Server Session completely
            HttpContext.Session.Clear();

            // Sign out from ASP.NET Core Identity Cookies if used
            await _signInManager.SignOutAsync();

            return Ok(new { success = true, message = "Logged out successfully from server." });
        }
    }
}