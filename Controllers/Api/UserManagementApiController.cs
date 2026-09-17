using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Route("api/usermanagement")]
    [ApiController]
    public class UserManagementApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserManagementApiController(ApplicationDbContext context)
        {
            _context = context;
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


            // ----------------------------------------
            // ROLE WISE COUNT
            // ----------------------------------------

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


            // ----------------------------------------
            // RETURN
            // ----------------------------------------

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
    }

    public class RoleDto
    {
        public string id { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }
}