using Dapper;
using HemFoundation.Data;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.RegularExpressions;

namespace HemFoundation.Controllers.Api
{
    /// <summary>
    /// Admin login accounts (public."Admins") for User Management → "Admin accounts".
    /// Any admin can see the list; only a super admin can add admins or change them.
    /// An admin's role is kept in the optional "RoleName" column
    /// (Data/Scripts/RunOnce_Update_2026_11_25.sql); without it every admin has the "Admin" role.
    /// </summary>
    [Route("api/admin-accounts")]
    [ApiController]
    public class AdminAccountsApiController : ControllerBase
    {
        public const string DefaultRole = "Admin";
        private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AdminAccountsApiController> _logger;

        public AdminAccountsApiController(ApplicationDbContext context, RoleManager<IdentityRole> roleManager, ILogger<AdminAccountsApiController> logger)
        {
            _context = context;
            _roleManager = roleManager;
            _logger = logger;
        }

        private IActionResult Fail(int status, string message) => StatusCode(status, new { success = false, message });

        // ------------------------------------------------------------------
        // Who is calling
        // ------------------------------------------------------------------

        /// <summary>The logged-in admin, or null when the session is not an admin login.</summary>
        private async Task<Admin?> CurrentAdminAsync()
        {
            var session = HttpContext.Session;
            if (session.GetString("IsAdminLoggedIn") != "true") return null;

            // Set by the admin login. Older sessions: an admin login has a numeric UserId and no AspNetUserID.
            string? id = session.GetString("AdminId");
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(session.GetString("AspNetUserID")))
                id = session.GetString("UserId");

            if (!long.TryParse(id, out long adminId)) return null;

            return await _context.Admins.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == adminId && a.IsActive && !a.IsDeleted);
        }

        private async Task<(Admin? me, IActionResult? error)> RequireSuperAdminAsync()
        {
            if (!HttpContext.Request.Headers.ContainsKey("X-Requested-With")) return (null, Fail(400, "Invalid request."));
            var me = await CurrentAdminAsync();
            if (me == null) return (null, Fail(401, "Please log in with an admin account."));
            if (!me.IsSuperAdmin) return (null, Fail(403, "Only a super admin can add or change admin accounts."));
            return (me, null);
        }

        // ------------------------------------------------------------------
        // RoleName column (optional)
        // ------------------------------------------------------------------

        private static bool? _hasRoleColumn;

        public static async Task<bool> HasRoleColumnAsync(ApplicationDbContext context)
        {
            if (_hasRoleColumn == true) return true;
            try
            {
                var conn = context.Database.GetDbConnection();
                bool has = await conn.ExecuteScalarAsync<bool>(
                    @"SELECT EXISTS (SELECT 1 FROM information_schema.columns
                      WHERE table_schema = 'public' AND table_name = 'Admins' AND column_name = 'RoleName')");
                _hasRoleColumn = has;   // remembered once found; re-checked until then
                return has;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Role chosen for an admin, or null (used by the admin login).</summary>
        public static async Task<string?> GetAdminRoleAsync(ApplicationDbContext context, long adminId)
        {
            if (!await HasRoleColumnAsync(context)) return null;
            try
            {
                var conn = context.Database.GetDbConnection();
                return await conn.ExecuteScalarAsync<string?>(@"SELECT ""RoleName"" FROM public.""Admins"" WHERE ""Id"" = @id", new { id = adminId });
            }
            catch
            {
                return null;
            }
        }

        private async Task<Dictionary<long, string?>> RolesByAdminAsync()
        {
            if (!await HasRoleColumnAsync(_context)) return new();
            var conn = _context.Database.GetDbConnection();
            var rows = await conn.QueryAsync<(long Id, string? RoleName)>(@"SELECT ""Id"", ""RoleName"" FROM public.""Admins""");
            return rows.ToDictionary(r => r.Id, r => r.RoleName);
        }

        private async Task SetRoleAsync(long adminId, string? roleName)
        {
            var conn = _context.Database.GetDbConnection();
            await conn.ExecuteAsync(@"UPDATE public.""Admins"" SET ""RoleName"" = @role, ""UpdatedOn"" = now() WHERE ""Id"" = @id",
                new { role = roleName, id = adminId });
        }

        /// <summary>Roles an admin can get. "Admin" is always offered: it is what the login uses by default.</summary>
        private async Task<List<string>> RoleNamesAsync()
        {
            var names = await _roleManager.Roles.Select(r => r.Name!).Where(n => n != null).ToListAsync();
            if (!names.Contains(DefaultRole)) names.Add(DefaultRole);
            return names.Distinct().OrderBy(n => n == DefaultRole ? 0 : 1).ThenBy(n => n).ToList();
        }

        // ------------------------------------------------------------------
        // List
        // ------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var me = await CurrentAdminAsync();
            if (me == null) return Fail(401, "Please log in with an admin account.");

            var admins = await _context.Admins.AsNoTracking().Where(a => !a.IsDeleted)
                .OrderByDescending(a => a.IsActive).ThenByDescending(a => a.IsSuperAdmin).ThenBy(a => a.FirstName)
                .ToListAsync();
            var roles = await RolesByAdminAsync();
            bool roleColumn = await HasRoleColumnAsync(_context);

            return Ok(new
            {
                success = true,
                canManage = me.IsSuperAdmin,
                meId = me.Id,
                roleColumn,
                roles = await RoleNamesAsync(),
                total = admins.Count,
                active = admins.Count(a => a.IsActive),
                superAdmins = admins.Count(a => a.IsSuperAdmin && a.IsActive),
                items = admins.Select(a => new
                {
                    a.Id,
                    name = $"{a.FirstName} {a.LastName}".Trim(),
                    a.FirstName,
                    a.LastName,
                    a.Email,
                    mobile = a.MobileNo,
                    role = a.IsSuperAdmin ? "Super admin" : (roles.TryGetValue(a.Id, out var r) && !string.IsNullOrWhiteSpace(r) ? r : DefaultRole),
                    a.IsSuperAdmin,
                    a.IsActive,
                    createdOn = a.CreatedOn.ToString("dd MMM yyyy")
                })
            });
        }

        // ------------------------------------------------------------------
        // Create
        // ------------------------------------------------------------------

        public class CreateAdminDto
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Email { get; set; }
            public string? Mobile { get; set; }
            public string? Password { get; set; }
            public string? Role { get; set; }
            public bool IsSuperAdmin { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAdminDto model)
        {
            var (me, error) = await RequireSuperAdminAsync();
            if (error != null) return error;
            if (model == null) return Fail(400, "Invalid request.");

            string first = (model.FirstName ?? "").Trim(), last = (model.LastName ?? "").Trim();
            string email = (model.Email ?? "").Trim(), mobile = (model.Mobile ?? "").Trim();
            string password = model.Password ?? "";
            string? role = string.IsNullOrWhiteSpace(model.Role) ? DefaultRole : model.Role.Trim();

            if (first.Length < 2 || first.Length > 100) return Fail(400, "Please enter the first name.");
            if (last.Length > 100) return Fail(400, "The last name is too long.");
            if (email.Length > 200 || !EmailPattern.IsMatch(email)) return Fail(400, "Please enter a valid email address. It is used to log in.");
            if (mobile.Length > 0 && (mobile.Length != 10 || !mobile.All(char.IsDigit))) return Fail(400, "Mobile number must be 10 digits.");
            if (password.Length < 8 || password.Length > 100) return Fail(400, "The password must be 8 to 100 characters.");
            if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return Fail(400, "The password must have letters and numbers.");

            if (!model.IsSuperAdmin)
            {
                if (!(await RoleNamesAsync()).Contains(role)) return Fail(400, "Choose a role from the list.");
                if (role != DefaultRole && !await HasRoleColumnAsync(_context))
                    return Fail(400, "To give a role other than Admin, run Data/Scripts/RunOnce_Update_2026_11_25.sql on the database first.");
            }

            // The login looks for an admin by e-mail first, so the e-mail must not belong to anyone else.
            string emailLower = email.ToLower();
            if (await _context.Admins.AnyAsync(a => a.Email != null && a.Email.ToLower() == emailLower && !a.IsDeleted))
                return Fail(409, "An admin with this email already exists.");
            if (await _context.Set<ApplicationUser>().AnyAsync(u => (u.Email != null && u.Email.ToLower() == emailLower) || (u.UserName != null && u.UserName.ToLower() == emailLower)))
                return Fail(409, "This email is already used by a portal user. Use a different email for the admin login.");

            var admin = new Admin
            {
                IdentityUserId = "adm-" + Guid.NewGuid().ToString("N"),
                FirstName = first,
                LastName = string.IsNullOrEmpty(last) ? null : last,
                Email = email,
                MobileNo = string.IsNullOrEmpty(mobile) ? null : mobile,
                PlainPassword = password,      // the admin login compares this column (existing behaviour)
                IsSuperAdmin = model.IsSuperAdmin,
                IsActive = true,
                IsDeleted = false,
                CreatedOn = DateTime.Now
            };

            _context.Admins.Add(admin);
            await _context.SaveChangesAsync();

            if (!model.IsSuperAdmin && role != DefaultRole) await SetRoleAsync(admin.Id, role);

            _logger.LogInformation("Admin {NewAdmin} created by admin {By}", admin.Id, me!.Id);
            return Ok(new { success = true, message = $"Admin account created for {first}. They can log in with {email}.", id = admin.Id });
        }

        // ------------------------------------------------------------------
        // Edit name, email, mobile and (optionally) set a new password
        // ------------------------------------------------------------------

        public class UpdateAdminDto
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Email { get; set; }
            public string? Mobile { get; set; }
            /// <summary>Empty keeps the current password.</summary>
            public string? NewPassword { get; set; }
        }

        /// <summary>A super admin can edit any admin (e.g. reset a forgotten password); every admin can edit their own account.</summary>
        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateAdminDto model)
        {
            if (!HttpContext.Request.Headers.ContainsKey("X-Requested-With")) return Fail(400, "Invalid request.");
            var me = await CurrentAdminAsync();
            if (me == null) return Fail(401, "Please log in with an admin account.");
            if (!me.IsSuperAdmin && me.Id != id) return Fail(403, "Only a super admin can change another admin's account.");
            if (model == null) return Fail(400, "Invalid request.");

            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
            if (admin == null) return Fail(404, "Admin not found.");

            string first = (model.FirstName ?? "").Trim(), last = (model.LastName ?? "").Trim();
            string email = (model.Email ?? "").Trim(), mobile = (model.Mobile ?? "").Trim();
            string password = model.NewPassword ?? "";

            if (first.Length < 2 || first.Length > 100) return Fail(400, "Please enter the first name.");
            if (last.Length > 100) return Fail(400, "The last name is too long.");
            if (email.Length > 200 || !EmailPattern.IsMatch(email)) return Fail(400, "Please enter a valid email address. It is used to log in.");
            if (mobile.Length > 0 && (mobile.Length != 10 || !mobile.All(char.IsDigit))) return Fail(400, "Mobile number must be 10 digits.");
            if (password.Length > 0)
            {
                if (password.Length < 8 || password.Length > 100) return Fail(400, "The new password must be 8 to 100 characters.");
                if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return Fail(400, "The new password must have letters and numbers.");
            }

            string emailLower = email.ToLower();
            if (!string.Equals(admin.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                if (await _context.Admins.AnyAsync(a => a.Id != id && a.Email != null && a.Email.ToLower() == emailLower && !a.IsDeleted))
                    return Fail(409, "Another admin already uses this email.");
                if (await _context.Set<ApplicationUser>().AnyAsync(u => (u.Email != null && u.Email.ToLower() == emailLower) || (u.UserName != null && u.UserName.ToLower() == emailLower)))
                    return Fail(409, "This email is already used by a portal user. Use a different email for the admin login.");
            }

            admin.FirstName = first;
            admin.LastName = string.IsNullOrEmpty(last) ? null : last;
            admin.Email = email;
            admin.MobileNo = string.IsNullOrEmpty(mobile) ? null : mobile;
            bool passwordChanged = password.Length > 0;
            if (passwordChanged) admin.PlainPassword = password;   // the admin login compares this column (existing behaviour)
            admin.UpdatedOn = DateTime.Now;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {Admin} updated by admin {By}{Pw}", admin.Id, me.Id, passwordChanged ? " (password changed)" : "");
            string who = admin.Id == me.Id ? "Your account" : $"{first}'s account";
            return Ok(new
            {
                success = true,
                message = passwordChanged ? $"{who} is updated. The new password works from the next login." : $"{who} is updated."
            });
        }

        // ------------------------------------------------------------------
        // Change role / super admin
        // ------------------------------------------------------------------

        public class RoleDto
        {
            public string? Role { get; set; }
            public bool IsSuperAdmin { get; set; }
        }

        [HttpPut("{id:long}/role")]
        public async Task<IActionResult> ChangeRole(long id, [FromBody] RoleDto model)
        {
            var (me, error) = await RequireSuperAdminAsync();
            if (error != null) return error;

            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
            if (admin == null) return Fail(404, "Admin not found.");

            string role = string.IsNullOrWhiteSpace(model?.Role) ? DefaultRole : model!.Role!.Trim();
            bool makeSuper = model?.IsSuperAdmin == true;

            if (!makeSuper && !(await RoleNamesAsync()).Contains(role)) return Fail(400, "Choose a role from the list.");
            if (!makeSuper && role != DefaultRole && !await HasRoleColumnAsync(_context))
                return Fail(400, "To give a role other than Admin, run Data/Scripts/RunOnce_Update_2026_11_25.sql on the database first.");

            // Keep at least one active super admin.
            if (admin.IsSuperAdmin && !makeSuper)
            {
                int others = await _context.Admins.CountAsync(a => a.Id != id && a.IsSuperAdmin && a.IsActive && !a.IsDeleted);
                if (others == 0) return Fail(400, "There must be at least one active super admin.");
                if (admin.Id == me!.Id) return Fail(400, "You cannot remove your own super admin access. Ask another super admin.");
            }

            admin.IsSuperAdmin = makeSuper;
            admin.UpdatedOn = DateTime.Now;
            await _context.SaveChangesAsync();
            if (await HasRoleColumnAsync(_context)) await SetRoleAsync(admin.Id, makeSuper || role == DefaultRole ? null : role);

            return Ok(new { success = true, message = $"{admin.FirstName} is now {(makeSuper ? "a super admin" : role)}. It applies from their next login." });
        }

        // ------------------------------------------------------------------
        // Activate / deactivate
        // ------------------------------------------------------------------

        public class StatusDto { public bool IsActive { get; set; } }

        [HttpPut("{id:long}/status")]
        public async Task<IActionResult> ChangeStatus(long id, [FromBody] StatusDto model)
        {
            var (me, error) = await RequireSuperAdminAsync();
            if (error != null) return error;

            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
            if (admin == null) return Fail(404, "Admin not found.");
            bool active = model?.IsActive == true;

            if (!active)
            {
                if (admin.Id == me!.Id) return Fail(400, "You cannot deactivate your own account.");
                if (admin.IsSuperAdmin && await _context.Admins.CountAsync(a => a.Id != id && a.IsSuperAdmin && a.IsActive && !a.IsDeleted) == 0)
                    return Fail(400, "There must be at least one active super admin.");
            }

            admin.IsActive = active;
            admin.UpdatedOn = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = active ? $"{admin.FirstName} can log in again." : $"{admin.FirstName} can no longer log in." });
        }
    }
}
