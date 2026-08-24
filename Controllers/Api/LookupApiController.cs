using HemFoundation.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Route("api/lookup")]
    [ApiController]
    public class LookupApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;

        public LookupApiController(ApplicationDbContext context, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _roleManager = roleManager;
        }

        // =========================================================================
        // 1. GET ACTIVE PORTALS DROPDOWN LIST
        // URL: GET /api/lookup/portals
        // =========================================================================
        [HttpGet("portals")]
        public async Task<IActionResult> GetPortals()
        {
            var portals = await _context.PortalMasters
                .Where(p => p.IsActive == true)
                .OrderBy(p => p.Id)
                .Select(p => new {
                    Value = p.Id,
                    Text = p.PortalName,
                    Description = p.Description
                })
                .ToListAsync();

            return Ok(new { success = true, data = portals });
        }

        // =========================================================================
        // 2. GET ACTIVE GENDERS DROPDOWN LIST
        // URL: GET /api/lookup/genders
        // =========================================================================
        [HttpGet("genders")]
        public async Task<IActionResult> GetGenders()
        {
            var genders = await _context.GenderMasters
                .Where(g => g.IsActive == true)
                .OrderBy(g => g.Id)
                .Select(g => new {
                    Value = g.Id,
                    Text = g.GenderName
                })
                .ToListAsync();

            return Ok(new { success = true, data = genders });
        }

        // =========================================================================
        // 3. GET ROLES DROPDOWN LIST (From AspNetRoles Table)
        // URL: GET /api/lookup/roles
        // =========================================================================
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => new {
                    Value = r.Id,
                    Text = r.Name
                })
                .ToListAsync();

            return Ok(new { success = true, data = roles });
        }

        // =========================================================================
        // 4. SWAGGER UTILITY: AUTO-CREATE ROLES IN AspNetRoles TABLE
        // URL: POST /api/lookup/seed-roles
        // =========================================================================
        [HttpPost("seed-roles")]
        public async Task<IActionResult> SeedRolesFromSwagger()
        {
            string[] portalRoles = {
                "Admin",
                "Teacher",
                "Student",
                "School",
                "Parent",
                "Affiliate",
                "Volunteer",
                "TesterEmployee"
            };

            List<string> createdRoles = new List<string>();
            List<string> existingRoles = new List<string>();

            foreach (var roleName in portalRoles)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
                    if (result.Succeeded)
                    {
                        createdRoles.Add(roleName);
                    }
                }
                else
                {
                    existingRoles.Add(roleName);
                }
            }

            return Ok(new
            {
                success = true,
                message = "Roles seeding process completed!",
                NewlyCreated = createdRoles,
                AlreadyExisted = existingRoles
            });
        }

      
    }
}