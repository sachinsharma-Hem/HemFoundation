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

        // 4. DELETE ROLE BY ID
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteRole(string id)
        {
            try
            {
                // -----------------------------------------
                // 1. Role ko direct DB se get karo
                //    RoleManager use nahi karna
                //    ConcurrencyStamp bhi read nahi hoga
                // -----------------------------------------
                var role = await _context.AspNetRoles
                    .Where(r => r.Id == id)
                    .Select(r => new
                    {
                        r.Id,
                        r.Name
                    })
                    .FirstOrDefaultAsync();

                if (role == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Role not found."
                    });
                }

                // -----------------------------------------
                // 2. Admin role delete nahi karna
                // -----------------------------------------
                if (string.Equals(
                        role.Name,
                        "Admin",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "The 'Admin' role cannot be deleted."
                    });
                }

                // -----------------------------------------
                // 3. Check karo role kisi user ko assigned hai ya nahi
                //    UserManager use nahi karna
                // -----------------------------------------
                var usersInRole = await _context
                    .Set<IdentityUserRole<string>>()
                    .AnyAsync(x => x.RoleId == id);

                if (usersInRole)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            $"Cannot delete role '{role.Name}' because it is assigned to users."
                    });
                }

                // -----------------------------------------
                // 4. Pehle Role Menu Rights delete karo
                // -----------------------------------------
                var rightsToRemove = await _context.RoleMenuRights
                    .Where(r => r.RoleId == id)
                    .ToListAsync();

                if (rightsToRemove.Any())
                {
                    _context.RoleMenuRights.RemoveRange(rightsToRemove);

                    await _context.SaveChangesAsync();
                }

                // -----------------------------------------
                // 5. Role direct SQL se delete karo
                //    RoleManager.DeleteAsync() nahi
                //    Isliye ConcurrencyStamp check nahi hoga
                // -----------------------------------------
                var deletedRows = await _context.Database
                    .ExecuteSqlInterpolatedAsync(
                        $"DELETE FROM public.aspnetroles WHERE id = {id}");

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
                    message = $"Role '{role.Name}' deleted successfully!"
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

        // 5. GET MENUS AND RIGHTS FOR A SPECIFIC ROLE
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

        // 6. SAVE / UPDATE ROLE PERMISSIONS (Bulk Upsert)
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