using HemFoundation.Data;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
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


        

        [HttpGet("all-users")]
        public async Task<IActionResult> GetAllUsersList()
        {
            try
            {
                var list = await _context.HfViewUsers
                    .AsNoTracking()
                    .OrderByDescending(x => x.createddate)
                    .Select(u => new
                    {
                        id = u.userid,

                        identityId =
                            u.aspnetuserid ??
                            u.userid.ToString(),

                        userCode =
                            u.username ??
                            ("USR-" + u.userid),

                        username =
                            u.username ?? "-",

                        fullName =
                            ((u.firstname ?? "") + " " +
                             (u.lastname ?? "")).Trim(),

                        email =
                            u.emailid ?? "-",

                        phoneNumber =
                            u.mobileno ?? "-",

                        password =
                            u.password_ ?? "-",

                        portalName =
                            u.UserRoles ?? "User",

                        city = "-",

                        state = "-",

                        address =
                            u.addressline1 ?? "-",

                        pincode =
                            u.pincode ?? "-",

                        createdDate =
                            u.createddate.HasValue
                                ? u.createddate.Value.ToString(
                                    "dd MMM yyyy, hh:mm tt")
                                : "-",

                        isActive =
                            u.isactive
                    })
                    .ToListAsync();


                var roleCounts =
                    new Dictionary<string, int>();


                foreach (var user in list)
                {
                    var roles =
                        (user.portalName ?? "User")
                        .Split(
                            ',',
                            StringSplitOptions.RemoveEmptyEntries)
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
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load users.",
                    error =
                        ex.InnerException?.Message ??
                        ex.Message
                });
            }
        }

        [HttpGet("roles-list")]
        public async Task<IActionResult> GetAspNetRoles()
        {
            try
            {
                var roles = await _context.Database
                    .SqlQueryRaw<RoleDto>(
                        """
                        SELECT 
                            id,
                            "Name" AS "RoleName"
                        FROM public.aspnetroles
                        """
                    )
                    .ToListAsync();


                return Ok(new
                {
                    success = true,
                    data = roles
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to load roles.",
                    error =
                        ex.InnerException?.Message ??
                        ex.Message
                });
            }
        }

        [HttpPost("update-role")]
        public async Task<IActionResult> UpdateUserRole(
            [FromBody] UpdateUserRoleDto model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.IdentityId) ||
                string.IsNullOrWhiteSpace(model.RoleName))
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "User ID and Role Name are required."
                });
            }


            try
            {
                var user =
                    await _context.Users
                        .FirstOrDefaultAsync(
                            u =>
                                u.AspNetUserID ==
                                model.IdentityId ||
                                u.UserID.ToString() ==
                                model.IdentityId);


                string actualIdentityId =
                    user?.AspNetUserID ??
                    model.IdentityId;


                var targetRole =
                    _context.Roles
                        .AsEnumerable()
                        .FirstOrDefault(
                            r =>
                                r.Name != null &&
                                r.Name.Equals(
                                    model.RoleName.Trim(),
                                    StringComparison
                                        .OrdinalIgnoreCase));


                if (targetRole == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message =
                            $"Role '{model.RoleName}' does not exist in system."
                    });
                }


                var existingUserRoles =
                    await _context
                        .Set<IdentityUserRole<string>>()
                        .Where(
                            ur =>
                                ur.UserId ==
                                actualIdentityId)
                        .ToListAsync();


                if (existingUserRoles.Any())
                {
                    _context
                        .Set<IdentityUserRole<string>>()
                        .RemoveRange(
                            existingUserRoles);
                }


                _context
                    .Set<IdentityUserRole<string>>()
                    .Add(
                        new IdentityUserRole<string>
                        {
                            UserId =
                                actualIdentityId,

                            RoleId =
                                targetRole.Id
                        });


                await _context.SaveChangesAsync();


                return Ok(new
                {
                    success = true,
                    message =
                        $"User role updated to '{targetRole.Name}' successfully!"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message =
                        "Failed to update user role.",
                    error =
                        ex.InnerException?.Message ??
                        ex.Message
                });
            }
        }

        [HttpDelete("delete/{identityId}")]
        public async Task<IActionResult> DeleteUser(string identityId)
        {
            if (string.IsNullOrWhiteSpace(identityId))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid user ID."
                });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                

                var userExists =
                    await _context.Database
                        .SqlQueryRaw<int>(
                            """
                    SELECT COUNT(*) AS "Value"
                    FROM public.users
                    WHERE aspnetuserid = {0}
                    """,
                            identityId
                        )
                        .FirstOrDefaultAsync();


                var aspNetUserExists =
                    await _context.Database
                        .SqlQueryRaw<int>(
                            """
                    SELECT COUNT(*) AS "Value"
                    FROM public.aspnetusers
                    WHERE id = {0}
                    """,
                            identityId
                        )
                        .FirstOrDefaultAsync();


                if (userExists == 0 && aspNetUserExists == 0)
                {
                    await transaction.RollbackAsync();

                    return NotFound(new
                    {
                        success = false,
                        message = "User not found."
                    });
                }

                var usersUpdated =
                    await _context.Database
                        .ExecuteSqlInterpolatedAsync(
                            $"""
                    UPDATE public.users
                    SET
                        isactive = B'0',
                        isuserdeactivated = B'1',
                        accesstoken = NULL,
                        modifieddate = CURRENT_TIMESTAMP
                    WHERE aspnetuserid = {identityId}
                    """
                        );

                var securityStamp =
                    Guid.NewGuid().ToString();


                var identityUpdated =
                    await _context.Database
                        .ExecuteSqlInterpolatedAsync(
                            $"""
                    UPDATE public.aspnetusers
                    SET
                        lockoutenabled = true,
                        lockoutenddateutc =
                            CURRENT_TIMESTAMP +
                            INTERVAL '100 years',
                        securitystamp = {securityStamp},
                        accessfailedcount = 0
                    WHERE id = {identityId}
                    """
                        );


                await transaction.CommitAsync();


                return Ok(new
                {
                    success = true,

                    message =
                        "User deactivated and account locked successfully.",

                    usersUpdated =
                        usersUpdated,

                    identityUpdated =
                        identityUpdated
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new
                {
                    success = false,

                    message =
                        "Failed to deactivate user.",

                    error =
                        ex.InnerException?.Message ??
                        ex.Message
                });
            }
        }

        [HttpPost("delete-batch")]
        public async Task<IActionResult> DeleteUsersBatch(
            [FromBody] BatchDeleteUsersDto model)
        {
            if (model == null ||
                model.IdentityIds == null ||
                model.IdentityIds.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please select at least one user."
                });
            }


            var identityIds =
                model.IdentityIds
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .Select(x =>
                        x.Trim())
                    .Distinct()
                    .ToList();


            if (!identityIds.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No valid users selected."
                });
            }


            await using var transaction =
                await _context.Database.BeginTransactionAsync();


            try
            {
                int usersUpdated = 0;

                int accountsLocked = 0;

                foreach (var identityId in identityIds)
                {
                    var affected =
                        await _context.Database
                            .ExecuteSqlInterpolatedAsync(
                                $"""
                        UPDATE public.users
                        SET
                            isactive = B'0',
                            isuserdeactivated = B'1',
                            accesstoken = NULL,
                            modifieddate = CURRENT_TIMESTAMP
                        WHERE aspnetuserid = {identityId}
                        """
                            );

                    usersUpdated += affected;
                }


                foreach (var identityId in identityIds)
                {
                    var securityStamp =
                        Guid.NewGuid().ToString();


                    var affected =
                        await _context.Database
                            .ExecuteSqlInterpolatedAsync(
                                $"""
                        UPDATE public.aspnetusers
                        SET
                            lockoutenabled = true,
                            lockoutenddateutc =
                                CURRENT_TIMESTAMP +
                                INTERVAL '100 years',
                            securitystamp = {securityStamp},
                            accessfailedcount = 0
                        WHERE id = {identityId}
                        """
                            );


                    accountsLocked += affected;
                }



                await transaction.CommitAsync();


                return Ok(new
                {
                    success = true,

                    deletedCount =
                        usersUpdated,

                    lockedCount =
                        accountsLocked,

                    message =
                        $"{usersUpdated} user(s) deactivated and " +
                        $"{accountsLocked} account(s) locked successfully."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();


                return StatusCode(500, new
                {
                    success = false,

                    message =
                        "Failed to deactivate selected users.",

                    error =
                        ex.InnerException?.Message ??
                        ex.Message
                });
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


    public class BatchDeleteUsersDto
    {
        public List<string> IdentityIds { get; set; } = new();
    }
}