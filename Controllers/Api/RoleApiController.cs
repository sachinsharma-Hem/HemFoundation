using HemFoundation.Data;
using HemFoundation.DTOs;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Route("api/role")]
    [ApiController]
    public class RoleApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public RoleApiController(
            ApplicationDbContext context,
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        // 1. GET ALL ROLES (For Dropdown & Sidebar Selection)
        [HttpGet("list")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => new { r.Id, r.Name })
                .ToListAsync();

            return Ok(new { success = true, data = roles });
        }

        // 2. CREATE NEW ROLE
       
        [HttpPost("create")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto model)
        {
            if (string.IsNullOrWhiteSpace(model.RoleName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Role name is required."
                });
            }

            string roleName = model.RoleName.Trim();

            // Check duplicate role
            var existingRole = await _context.AspNetRoles
                .FirstOrDefaultAsync(x => x.Name == roleName);

            if (existingRole != null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Role '{roleName}' already exists."
                });
            }

            // Create role
            var role = new AspNetRoles
            {
                Id = Guid.NewGuid().ToString(),
                Name = roleName,

            };

            _context.AspNetRoles.Add(role);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Role '{roleName}' created successfully!"
            });
        }
        public async Task<IActionResult> CreateRole1([FromBody] CreateRoleDto model)
        {
            if (string.IsNullOrWhiteSpace(model.RoleName))
                return BadRequest(new { success = false, message = "Role name is required." });

            string roleName = model.RoleName.Trim();

            if (await _roleManager.RoleExistsAsync(roleName))
                return BadRequest(new { success = false, message = $"Role '{roleName}' already exists." });

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { success = false, message = errors });
            }

            return Ok(new { success = true, message = $"Role '{roleName}' created successfully!" });
        }

        // 3. UPDATE / RENAME ROLE BY ID
        
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateRole(
    string id,
    [FromBody] CreateRoleDto model)
        {
            if (string.IsNullOrWhiteSpace(model.RoleName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Role name cannot be empty."
                });
            }

            string newName = model.RoleName.Trim();

            // Get role directly from aspnetroles table
            var role = await _context.AspNetRoles
                .FirstOrDefaultAsync(x => x.Id == id);

            if (role == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Role not found."
                });
            }

            // Check duplicate role name
            var existingRole = await _context.AspNetRoles
                .FirstOrDefaultAsync(x =>
                    x.Name == newName &&
                    x.Id != id);

            if (existingRole != null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Role '{newName}' already exists."
                });
            }

            // ONLY Name will be changed
            role.Name = newName;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Role renamed to '{newName}' successfully!"
            });
        }
        public async Task<IActionResult> UpdateRole1(string id, [FromBody] CreateRoleDto model)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return NotFound(new { success = false, message = "Role not found." });

            if (string.IsNullOrWhiteSpace(model.RoleName))
                return BadRequest(new { success = false, message = "Role name cannot be empty." });

            string newName = model.RoleName.Trim();

            var existingRole = await _roleManager.FindByNameAsync(newName);
            if (existingRole != null && existingRole.Id != id)
                return BadRequest(new { success = false, message = $"Role '{newName}' already exists." });

            role.Name = newName;
            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { success = false, message = errors });
            }

            return Ok(new { success = true, message = $"Role renamed to '{newName}' successfully!" });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteRole(string id)
        {
            try
            {
                var role = await _context.AspNetRoles
                    .Where(r => r.Id == id)
                    .Select(r => new { r.Id, r.Name })
                    .FirstOrDefaultAsync();

                if (role == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Role not found."
                    });
                }

                // Admin role delete nahi hone dena
                if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(role.Name, "WhizClassical-Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"The '{role.Name}' role cannot be deleted."
                    });
                }

                // 1. Pehle Role Menu Rights delete karo
                var rightsToRemove = await _context.RoleMenuRights
                    .Where(r => r.RoleId == id)
                    .ToListAsync();

                if (rightsToRemove.Any())
                {
                    _context.RoleMenuRights.RemoveRange(rightsToRemove);
                }

                // 2. Is role se jude hue saare Users find karo aur delete karo (Optional / Cascade Clean)
                var userIdsInRole = await _context.Set<IdentityUserRole<string>>()
                    .Where(x => x.RoleId == id)
                    .Select(x => x.UserId)
                    .ToListAsync();

                if (userIdsInRole.Any())
                {
                    // Users table se bhi delete karna ho toh ye use karein
                    var usersToDelete = await _context.Users
                        .Where(u => userIdsInRole.Contains(u.AspNetUserID))
                        .ToListAsync();

                    if (usersToDelete.Any())
                    {
                        _context.Users.RemoveRange(usersToDelete);
                    }

                    // Identity User Roles mapping clean karein
                    var userRolesToRemove = await _context.Set<IdentityUserRole<string>>()
                        .Where(x => x.RoleId == id)
                        .ToListAsync();

                    _context.Set<IdentityUserRole<string>>().RemoveRange(userRolesToRemove);
                }

                await _context.SaveChangesAsync();

                // 3. Role direct SQL se delete karo taaki Concurrency error na aaye
                var deletedRows = await _context.Database
                    .ExecuteSqlInterpolatedAsync($"DELETE FROM public.aspnetroles WHERE id = {id}");

                if (deletedRows == 0)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Role not found or already deleted."
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = $"Role '{role.Name}' and its associated users deleted successfully!"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Failed to delete role.",
                    error = ex.InnerException?.Message ?? ex.Message
                });
            }
        }

        [HttpGet("rights/{roleId}")]
        public async Task<IActionResult> GetRoleRights(string roleId)
        {
            var allMenus = await _context.Menus
                .Where(m => m.IsActive == true && m.IsDeleted == false)
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new { m.Id, m.ParentId, m.MenuName, m.Icon, m.DisplayOrder })
                .ToListAsync();

            var assignedRights = await _context.RoleMenuRights
                .Where(rm => rm.RoleId == roleId && rm.IsDeleted == false)
                .ToListAsync();

            var mergedData = allMenus.Select(menu => {
                var right = assignedRights.FirstOrDefault(r => r.MenuId == menu.Id);
                return new
                {
                    menu.Id,
                    menu.ParentId,
                    menu.MenuName,
                    menu.Icon,
                    menu.DisplayOrder,
                    CanView = right != null && right.CanView,
                    CanAdd = right != null && right.CanAdd,
                    CanEdit = right != null && right.CanEdit,
                    CanDelete = right != null && right.CanDelete
                };
            }).ToList();

            return Ok(new { success = true, data = mergedData });
        }

        [HttpPost("save-rights")]
        public async Task<IActionResult> SaveRoleRights([FromBody] SaveRoleRightsPayload payload)
        {
            if (string.IsNullOrEmpty(payload.RoleId))
                return BadRequest(new { success = false, message = "Invalid Role selected." });

            try
            {
                var existingRights = await _context.RoleMenuRights
                    .Where(rm => rm.RoleId == payload.RoleId)
                    .ToListAsync();

                _context.RoleMenuRights.RemoveRange(existingRights);

                var newRights = new List<RoleMenuRight>();
                foreach (var item in payload.Rights)
                {
                    if (item.CanView || item.CanAdd || item.CanEdit || item.CanDelete)
                    {
                        newRights.Add(new RoleMenuRight
                        {
                            RoleId = payload.RoleId,
                            MenuId = item.MenuId,
                            CanView = item.CanView,
                            CanAdd = item.CanAdd,
                            CanEdit = item.CanEdit,
                            CanDelete = item.CanDelete,
                            IsActive = true,
                            IsDeleted = false,
                            CreatedOn = DateTime.UtcNow
                        });
                    }
                }

                if (newRights.Any())
                {
                    _context.RoleMenuRights.AddRange(newRights);
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Role permissions updated successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error saving permissions: " + ex.Message });
            }
        }
    }

    public class CreateRoleDto
    {
        public string RoleName { get; set; } = string.Empty;
    }
}