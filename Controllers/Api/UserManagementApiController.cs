using HemFoundation.Data;
using HemFoundation.DTOs;
using HemFoundation.Helpers;
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

        // GET ALL USERS (Unified Master List)
        [HttpGet("all-users")]
        public async Task<IActionResult> GetAllUsersList()
        {
            var masterUsersList = new List<object>();

            // 1. Admins
            var adminProfiles = await _context.Admins.Where(a => !a.IsDeleted).ToListAsync();
            foreach (var a in adminProfiles)
            {
                var user = await _userManager.FindByIdAsync(a.IdentityUserId ?? "");
                masterUsersList.Add(new
                {
                    id = a.Id,
                    identityId = a.IdentityUserId ?? a.Id.ToString(),
                    userCode = user?.UserCode ?? "USR-",
                    username = user?.UserName ?? "-",
                    fullName = $"{a.FirstName} {a.LastName}",
                    email = a.Email ?? "-",
                    phoneNumber = a.MobileNo ?? "-",
                    password = a.PlainPassword ?? "-",
                    portalName = "Admin",
                    city = a.City ?? "-",
                    state = a.State ?? "-",
                    address = a.Address ?? "-",
                    pincode = a.PinCode ?? "-",
                    createdDate = a.CreatedOn.ToString("dd MMM yyyy, hh:mm tt"),
                    isActive = a.IsActive
                });
            }

            // 2. Teachers
            var teachers = await _context.Users.ToListAsync();
            foreach (var t in teachers)
            {
                var user = await _userManager.FindByIdAsync(t.AspNetUserID ?? "");
                masterUsersList.Add(new
                {
                    id = t.UserID,
                    identityId = t.AspNetUserID ?? t.UserID.ToString(),
                    userCode = user?.UserCode ?? ("TCH-" + t.UserID),
                    username = user?.UserName ?? "-",
                    fullName = $"{t.FirstName} {t.LastName}",
                    email = t.EmailID ?? "-",
                    phoneNumber = t.MobileNo ?? "-",
                    password = user?.PasswordHash ?? t.MobileNo ?? "-",
                    portalName = "Teacher",
                    city = t.CityName ?? "-",
                    //state = t.State ?? "-",
                    //address = t.AddressLine1 ?? "-",
                    pincode = t.Pincode ?? "-",
                    createdDate = t.CreatedDate.ToString("dd MMM yyyy, hh:mm tt"),
                    isActive = t.IsActive
                });
            }

            // 3. Students
            var students = await _context.Students.ToListAsync();
            foreach (var s in students)
            {
                var user = await _userManager.FindByIdAsync(s.AspNetUserID ?? "");
                masterUsersList.Add(new
                {
                    id = s.StudentID,
                    identityId = s.AspNetUserID ?? s.StudentID.ToString(),
                    userCode = user?.UserCode ?? ("STD-" + s.StudentID),
                    username = user?.UserName ?? "-",
                    fullName = $"{s.FirstName} {s.LastName}",
                    email = s.GuardianEmailID ?? "-",
                    phoneNumber = s.GuardianMobileNo ?? "-",
                    password = s.GuardianMobileNo ?? "-",
                    portalName = "Student",
                    city = "-",
                    state = "-",
                    address = "-",
                    pincode = "-",
                    createdDate = s.CreatedDate.ToString("dd MMM yyyy, hh:mm tt"),
                    isActive = s.IsActive
                });
            }

            // 4. Schools / Institutes
            var institutes = await _context.Institutes.ToListAsync();
            foreach (var i in institutes)
            {
                var user = await _userManager.FindByIdAsync(i.AspNetUserID ?? "");
                masterUsersList.Add(new
                {
                    id = i.InstituteID,
                    identityId = i.AspNetUserID ?? i.InstituteID.ToString(),
                    userCode = user?.UserCode ?? ("SCH-" + i.InstituteID),
                    username = user?.UserName ?? "-",
                    fullName = i.Name ?? "-",
                    email = i.EmailID ?? "-",
                    phoneNumber = i.MobileNo ?? "-",
                    password = i.MobileNo ?? "-",
                    portalName = "School",
                    city = i.City ?? "-",
                    state = i.State ?? "-",
                    address = i.Address ?? "-",
                    pincode = i.Pincode ?? "-",
                    createdDate = i.CreatedDate.ToString("dd MMM yyyy, hh:mm tt"),
                    isActive = i.IsActive
                });
            }

            // 5. Parents
            var parents = await _context.Parents.ToListAsync();
            foreach (var p in parents)
            {
                var user = await _userManager.FindByIdAsync(p.AspNetUserID ?? "");
                masterUsersList.Add(new
                {
                    id = p.ParentID,
                    identityId = p.AspNetUserID ?? p.ParentID.ToString(),
                    userCode = user?.UserCode ?? ("PAR-" + p.ParentID),
                    username = user?.UserName ?? "-",
                    fullName = $"{p.FirstName} {p.LastName}",
                    email = p.EmailID ?? "-",
                    phoneNumber = p.MobileNo ?? "-",
                    password = p.MobileNo ?? "-",
                    portalName = "Parent",
                    city = p.City ?? "-",
                    state = p.State ?? "-",
                    address = p.Address ?? "-",
                    pincode = p.Pincode ?? "-",
                    createdDate = p.CreatedDate.ToString("dd MMM yyyy, hh:mm tt"),
                    isActive = p.IsActive
                });
            }

            // 6. Affiliates
            var affiliates = await _context.Affiliates.ToListAsync();
            foreach (var a in affiliates)
            {
                var user = await _userManager.FindByIdAsync(a.AspNetUserID ?? "");
                masterUsersList.Add(new
                {
                    id = a.UserID,
                    identityId = a.AspNetUserID ?? a.UserID.ToString(),
                    userCode = a.UserCode ?? "AFF-",
                    username = user?.UserName ?? "-",
                    fullName = $"{a.FirstName} {a.LastName} ({a.OrganisationName})",
                    email = a.EmailID ?? "-",
                    phoneNumber = a.MobileNo ?? "-",
                    password = a.Password_ ?? a.MobileNo ?? "-",
                    portalName = "Affiliate",
                    city = a.City ?? "-",
                    state = a.State ?? "-",
                    address = a.AddressLine1 ?? "-",
                    pincode = a.Pincode ?? "-",
                    createdDate = a.CreatedDate.ToString("dd MMM yyyy, hh:mm tt"),
                    isActive = a.IsActive
                });
            }

            return Ok(new { success = true, data = masterUsersList });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            var adminProfile = await _context.Admins.FirstOrDefaultAsync(a => a.IdentityUserId == id);
            var roles = user != null ? await _userManager.GetRolesAsync(user) : new List<string>();

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = user?.Id ?? id,
                    fullName = user?.FullName ?? adminProfile?.FirstName,
                    email = user?.Email ?? adminProfile?.Email,
                    phoneNumber = user?.PhoneNumber ?? adminProfile?.MobileNo,
                    roleName = roles.FirstOrDefault() ?? "Admin",
                    genderId = adminProfile?.GenderId,
                    portalId = adminProfile?.PortalId,
                    pinCode = adminProfile?.PinCode,
                    address = adminProfile?.Address,
                    city = adminProfile?.City,
                    state = adminProfile?.State
                }
            });
        }

        // ==========================================
        // ADDED: CREATE ADMIN USER METHOD (POST)
        // ==========================================
        [HttpPost("create")]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Please fill all required fields correctly." });

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest(new { success = false, message = "Email is already registered." });

            int userCount = await _userManager.Users.CountAsync() + 1;
            string generatedCode = $"USR{userCount:D3}";

            var appUser = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                UserCode = generatedCode,
                PhoneNumber = model.MobileNo,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(appUser, model.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { success = false, message = errors });
            }

            string roleToAssign = !string.IsNullOrEmpty(model.RoleName) ? model.RoleName : "Admin";
            if (await _roleManager.RoleExistsAsync(roleToAssign))
            {
                await _userManager.AddToRoleAsync(appUser, roleToAssign);
            }

            string firstName = model.FullName;
            string? lastName = null;
            int spaceIdx = model.FullName.IndexOf(' ');
            if (spaceIdx > 0)
            {
                firstName = model.FullName.Substring(0, spaceIdx);
                lastName = model.FullName.Substring(spaceIdx + 1);
            }

            var adminProfile = new Admin
            {
                IdentityUserId = appUser.Id,
                FirstName = firstName,
                LastName = lastName,
                Email = model.Email,
                PlainPassword = model.Password,
                MobileNo = model.MobileNo,
                GenderId = model.GenderId,
                PortalId = model.PortalId ?? 1,
                PinCode = model.PinCode,
                Address = model.Address,
                City = model.City,
                State = model.State,
                IsActive = true,
                IsDeleted = false,
                CreatedOn = DateTimeHelper.UtcNow
            };

            _context.Admins.Add(adminProfile);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "User created successfully!" });
        }

        // ==========================================
        // ADDED: UPDATE ADMIN USER METHOD (PUT)
        // ==========================================
        [HttpPut("update")]
        public async Task<IActionResult> UpdateUser([FromBody] UserUpdateDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid data payload." });

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
                return NotFound(new { success = false, message = "User not found." });

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;
            user.PhoneNumber = model.MobileNo;
            await _userManager.UpdateAsync(user);

            if (!string.IsNullOrEmpty(model.RoleName))
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (!currentRoles.Contains(model.RoleName))
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (await _roleManager.RoleExistsAsync(model.RoleName))
                    {
                        await _userManager.AddToRoleAsync(user, model.RoleName);
                    }
                }
            }

            var profile = await _context.Admins.FirstOrDefaultAsync(a => a.IdentityUserId == model.Id);
            if (profile != null)
            {
                string firstName = model.FullName;
                string? lastName = null;
                int spaceIdx = model.FullName.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    firstName = model.FullName.Substring(0, spaceIdx);
                    lastName = model.FullName.Substring(spaceIdx + 1);
                }

                profile.FirstName = firstName;
                profile.LastName = lastName;
                profile.Email = model.Email;
                profile.MobileNo = model.MobileNo;
                profile.GenderId = model.GenderId;
                profile.PortalId = model.PortalId;
                profile.PinCode = model.PinCode;
                profile.Address = model.Address;
                profile.City = model.City;
                profile.State = model.State;
                profile.UpdatedOn = DateTimeHelper.UtcNow;

                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, message = "User updated successfully!" });
        }

        [HttpPost("toggle-status/{identityId}")]
        public async Task<IActionResult> ToggleStatus(string identityId)
        {
            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.IdentityUserId == identityId);
            if (admin != null) { admin.IsActive = !admin.IsActive; await _context.SaveChangesAsync(); return Ok(new { success = true, message = "Status updated!" }); }

            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.AspNetUserID == identityId);
            if (teacher != null) { teacher.IsActive = !teacher.IsActive; await _context.SaveChangesAsync(); return Ok(new { success = true, message = "Status updated!" }); }

            var student = await _context.Students.FirstOrDefaultAsync(s => s.AspNetUserID == identityId);
            if (student != null) { student.IsActive = !student.IsActive; await _context.SaveChangesAsync(); return Ok(new { success = true, message = "Status updated!" }); }

            var school = await _context.Institutes.FirstOrDefaultAsync(i => i.AspNetUserID == identityId);
            if (school != null) { school.IsActive = !school.IsActive; await _context.SaveChangesAsync(); return Ok(new { success = true, message = "Status updated!" }); }

            var parent = await _context.Parents.FirstOrDefaultAsync(p => p.AspNetUserID == identityId);
            if (parent != null) { parent.IsActive = !parent.IsActive; await _context.SaveChangesAsync(); return Ok(new { success = true, message = "Status updated!" }); }

            var affiliate = await _context.Affiliates.FirstOrDefaultAsync(a => a.AspNetUserID == identityId);
            if (affiliate != null) { affiliate.IsActive = !affiliate.IsActive; await _context.SaveChangesAsync(); return Ok(new { success = true, message = "Status updated!" }); }

            return NotFound(new { success = false, message = "User not found." });
        }

        [HttpDelete("delete/{identityId}")]
        public async Task<IActionResult> DeleteUser(string identityId)
        {
            var user = await _userManager.FindByIdAsync(identityId);
            if (user != null) await _userManager.DeleteAsync(user);

            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.IdentityUserId == identityId);
            if (admin != null) { admin.IsDeleted = true; admin.IsActive = false; }

            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.AspNetUserID == identityId);
            if (teacher != null) _context.Teachers.Remove(teacher);

            var student = await _context.Students.FirstOrDefaultAsync(s => s.AspNetUserID == identityId);
            if (student != null) _context.Students.Remove(student);

            var school = await _context.Institutes.FirstOrDefaultAsync(i => i.AspNetUserID == identityId);
            if (school != null) _context.Institutes.Remove(school);

            var parent = await _context.Parents.FirstOrDefaultAsync(p => p.AspNetUserID == identityId);
            if (parent != null) _context.Parents.Remove(parent);

            var affiliate = await _context.Affiliates.FirstOrDefaultAsync(a => a.AspNetUserID == identityId);
            if (affiliate != null) _context.Affiliates.Remove(affiliate);

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "User deleted successfully!" });
        }
    }
}