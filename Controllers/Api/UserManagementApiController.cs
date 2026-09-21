using HemFoundation.Data;
using HemFoundation.Models.Identity; // Aapke ApplicationUser ka namespace
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Route("api/usermanagement")]
    [ApiController]
    public class UserManagementApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserManagementApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // 1. GET ALL USERS DIRECTLY FROM OPTIMIZED VIEW
        [HttpGet("all-users")]
        public async Task<IActionResult> GetAllUsersList()
        {
            var list = await _context.HfViewUsers
                .AsNoTracking()
                .OrderByDescending(x => x.createddate)
                .Select(u => new
                {
                    id = u.userid,
                    identityId = u.aspnetuserid ?? u.userid.ToString(),
                    userCode = u.username ?? ("USR-" + u.userid),
                    username = u.username ?? "-",
                    fullName = ((u.firstname ?? "") + " " + (u.lastname ?? "")).Trim(),
                    email = u.emailid ?? "-",
                    phoneNumber = u.mobileno ?? "-",
                    password = u.password_ ?? "-",
                    portalName = u.UserRoles ?? "User",
                    city = "-",
                    state = "-",
                    address = u.addressline1 ?? "-",
                    pincode = u.pincode ?? "-",
                    createdDate = u.createddate.HasValue
                        ? u.createddate.Value.ToString("dd MMM yyyy, hh:mm tt")
                        : "-",
                    isActive = u.isactive
                })
                .ToListAsync();

            // ROLE WISE COUNT
            var roleCounts = new Dictionary<string, int>();

            foreach (var user in list)
            {
                var roles = (user.portalName ?? "User")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim());

                foreach (var role in roles)
                {
                    if (roleCounts.ContainsKey(role))
                    {
                        roleCounts[role]++;
                    }
                    else
                    {
                        roleCounts[role] = 1;
                    }
                }
            }

            return Ok(new
            {
                success = true,
                totalUsers = list.Count,
                roleCounts = roleCounts,
                data = list
            });
        }

        // 2. DYNAMIC ROLES FOR FILTER DROPDOWN FROM aspnetroles
        [HttpGet("roles-list")]
        public async Task<IActionResult> GetAspNetRoles()
        {
            var roles = await _context.Database
                .SqlQueryRaw<RoleDto>("SELECT id, \"Name\" as RoleName FROM public.aspnetroles")
                .ToListAsync();

            return Ok(new { success = true, data = roles });
        }

        // 3. UPDATE USER ROLE ENDPOINT
        [HttpPost("update-role")]
        public async Task<IActionResult> UpdateUserRole([FromBody] UpdateUserRoleDto model)
        {
            if (string.IsNullOrWhiteSpace(model.IdentityId) || string.IsNullOrWhiteSpace(model.RoleName))
            {
                return BadRequest(new { success = false, message = "User ID and Role Name are required." });
            }

            // Find user in AspNetUsers table
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AspNetUserID == model.IdentityId || u.UserID.ToString() == model.IdentityId);
            string actualIdentityId = user?.AspNetUserID ?? model.IdentityId;

            string targetRoleName = model.RoleName.Trim();

            // Check if role exists safely using client-side evaluation (.AsEnumerable())
            var targetRole = _context.Roles
                .AsEnumerable()
                .FirstOrDefault(r => r.Name != null && r.Name.Equals(targetRoleName, StringComparison.OrdinalIgnoreCase));

            if (targetRole == null)
            {
                return BadRequest(new { success = false, message = $"Role '{targetRoleName}' does not exist in system." });
            }

            try
            {
                // 1. Remove existing roles mapping from AspNetUserRoles table directly
                var existingUserRoles = await _context.Set<IdentityUserRole<string>>()
                    .Where(ur => ur.UserId == actualIdentityId)
                    .ToListAsync();

                if (existingUserRoles.Any())
                {
                    _context.Set<IdentityUserRole<string>>().RemoveRange(existingUserRoles);
                }

                // 2. Assign new role mapping in AspNetUserRoles table directly
                _context.Set<IdentityUserRole<string>>().Add(new IdentityUserRole<string>
                {
                    UserId = actualIdentityId,
                    RoleId = targetRole.Id
                });

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = $"User role updated to '{targetRole.Name}' successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to update role: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }
    }


    public class RoleDto
    {
        public string id { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }

    public class UpdateUserRoleDto
    {
        public string IdentityId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }
}