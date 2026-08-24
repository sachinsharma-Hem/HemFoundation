using HemFoundation.Data;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace HemFoundation.Controllers.Api
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Authorize]
    [ApiController]
    [Route("api/profile")]
    public class ProfileApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public ProfileApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        [HttpGet("details")]
        public async Task<IActionResult> GetProfileDetails()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { success = false, message = "User not found." });

            var roles = await _userManager.GetRolesAsync(user);
            string primaryRole = roles.FirstOrDefault() ?? "User";

            object profileData = null;

            if (primaryRole.Equals("Affiliate", StringComparison.OrdinalIgnoreCase))
            {
                var aff = await _context.Affiliates.FirstOrDefaultAsync(a => a.AspNetUserID == userId || a.EmailID == user.Email);
                profileData = new
                {
                    aff?.FirstName,
                    aff?.LastName,
                    organisationName = aff?.OrganisationName,
                    address = aff?.AddressLine1,
                    aff?.City,
                    aff?.State,
                    pincode = aff?.Pincode,
                    mobileNo = aff?.MobileNo,
                    photoUrl = aff?.PhotoUrl
                };
            }
            else if (primaryRole.Equals("School", StringComparison.OrdinalIgnoreCase))
            {
                var sch = await _context.Institutes.FirstOrDefaultAsync(i => i.AspNetUserID == userId || i.EmailID == user.Email);
                profileData = new
                {
                    sch?.FirstName,
                    sch?.LastName,
                    organisationName = sch?.Name,
                    address = sch?.Address,
                    sch?.City,
                    sch?.State,
                    pincode = sch?.Pincode,
                    mobileNo = sch?.MobileNo,
                    photoUrl = sch?.ProfileUrl
                };
            }
            else if (primaryRole.Equals("Teacher", StringComparison.OrdinalIgnoreCase))
            {
                var tch = await _context.Teachers.FirstOrDefaultAsync(t => t.AspNetUserID == userId || t.EmailID == user.Email);
                profileData = new
                {
                    tch?.FirstName,
                    tch?.LastName,
                    organisationName = "",
                    address = tch?.Address,
                    tch?.City,
                    tch?.State,
                    pincode = tch?.Pincode,
                    mobileNo = tch?.MobileNo,
                    photoUrl = tch?.PhotoUrl
                };
            }
            else if (primaryRole.Equals("Parent", StringComparison.OrdinalIgnoreCase))
            {
                var par = await _context.Parents.FirstOrDefaultAsync(p => p.AspNetUserID == userId || p.EmailID == user.Email);
                profileData = new
                {
                    par?.FirstName,
                    par?.LastName,
                    organisationName = "",
                    address = par?.Address,
                    par?.City,
                    par?.State,
                    pincode = par?.Pincode,
                    photoUrl = par?.PhotoUrl
                };
            }
            else
            {
                var adm = await _context.Admins.FirstOrDefaultAsync(a => a.IdentityUserId == userId || a.Email == user.Email);
                profileData = new
                {
                    adm?.FirstName,
                    adm?.LastName,
                    organisationName = "",
                    address = adm?.Address,
                    adm?.City,
                    adm?.State,
                    pincode = adm?.PinCode,
                    mobileNo = adm?.MobileNo,
                    photoUrl = adm?.ProfileImage
                };
            }

            return Ok(new
            {
                success = true,
                role = primaryRole,
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.PhoneNumber
                },
                profile = profileData ?? new { }
            });
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateProfile([FromForm] ProfileUpdateDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not authenticated." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { success = false, message = "User not found." });

            if (!string.IsNullOrEmpty(model.FullName))
            {
                user.FullName = model.FullName;
                user.PhoneNumber = model.PhoneNumber ?? user.PhoneNumber;
                await _userManager.UpdateAsync(user);
            }

            string photoUrl = string.Empty;
            if (model.ProfilePhoto != null && model.ProfilePhoto.Length > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles", "Updates");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ProfilePhoto.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfilePhoto.CopyToAsync(fileStream);
                }
                photoUrl = $"/uploads/profiles/Updates/{uniqueFileName}";
            }

            var roles = await _userManager.GetRolesAsync(user);
            string primaryRole = roles.FirstOrDefault() ?? "User";

            if (primaryRole.Equals("Affiliate", StringComparison.OrdinalIgnoreCase))
            {
                var affiliate = await _context.Affiliates.FirstOrDefaultAsync(a => a.AspNetUserID == userId || a.EmailID == user.Email);
                if (affiliate != null)
                {
                    affiliate.FirstName = model.FirstName ?? affiliate.FirstName;
                    affiliate.LastName = model.LastName ?? affiliate.LastName;
                    affiliate.OrganisationName = model.OrganisationName ?? affiliate.OrganisationName;
                    affiliate.MobileNo = model.PhoneNumber ?? affiliate.MobileNo;
                    affiliate.AddressLine1 = model.Address ?? affiliate.AddressLine1;
                    affiliate.City = model.City ?? affiliate.City;
                    affiliate.State = model.State ?? affiliate.State;
                    affiliate.Pincode = model.Pincode ?? affiliate.Pincode;
                    if (!string.IsNullOrEmpty(photoUrl)) affiliate.PhotoUrl = photoUrl;
                    _context.Affiliates.Update(affiliate);
                }
            }
            else if (primaryRole.Equals("School", StringComparison.OrdinalIgnoreCase))
            {
                var institute = await _context.Institutes.FirstOrDefaultAsync(i => i.AspNetUserID == userId || i.EmailID == user.Email);
                if (institute != null)
                {
                    institute.FirstName = model.FirstName ?? institute.FirstName;
                    institute.LastName = model.LastName ?? institute.LastName;
                    institute.Name = model.OrganisationName ?? institute.Name;
                    institute.MobileNo = model.PhoneNumber ?? institute.MobileNo;
                    institute.Address = model.Address ?? institute.Address;
                    institute.City = model.City ?? institute.City;
                    institute.State = model.State ?? institute.State;
                    institute.Pincode = model.Pincode ?? institute.Pincode;
                    if (!string.IsNullOrEmpty(photoUrl)) institute.ProfileUrl = photoUrl;
                    _context.Institutes.Update(institute);
                }
            }
            else if (primaryRole.Equals("Teacher", StringComparison.OrdinalIgnoreCase))
            {
                var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.AspNetUserID == userId || t.EmailID == user.Email);
                if (teacher != null)
                {
                    teacher.FirstName = model.FirstName ?? teacher.FirstName;
                    teacher.LastName = model.LastName ?? teacher.LastName;
                    teacher.MobileNo = model.PhoneNumber ?? teacher.MobileNo;
                    teacher.Address = model.Address ?? teacher.Address;
                    teacher.City = model.City ?? teacher.City;
                    teacher.State = model.State ?? teacher.State;
                    teacher.Pincode = model.Pincode ?? teacher.Pincode;
                    if (!string.IsNullOrEmpty(photoUrl)) teacher.PhotoUrl = photoUrl;
                    _context.Teachers.Update(teacher);
                }
            }
            else if (primaryRole.Equals("Parent", StringComparison.OrdinalIgnoreCase))
            {
                var parent = await _context.Parents.FirstOrDefaultAsync(p => p.AspNetUserID == userId || p.EmailID == user.Email);
                if (parent != null)
                {
                    parent.FirstName = model.FirstName ?? parent.FirstName;
                    parent.LastName = model.LastName ?? parent.LastName;
                    parent.Address = model.Address ?? parent.Address;
                    parent.City = model.City ?? parent.City;
                    parent.State = model.State ?? parent.State;
                    parent.Pincode = model.Pincode ?? parent.Pincode;
                    if (!string.IsNullOrEmpty(photoUrl)) parent.PhotoUrl = photoUrl;
                    _context.Parents.Update(parent);
                }
            }
            else
            {
                var admin = await _context.Admins.FirstOrDefaultAsync(a => a.IdentityUserId == userId || a.Email == user.Email);
                if (admin != null)
                {
                    admin.FirstName = model.FirstName ?? admin.FirstName;
                    admin.LastName = model.LastName ?? admin.LastName;
                    admin.MobileNo = model.PhoneNumber ?? admin.MobileNo;
                    admin.Address = model.Address ?? admin.Address;
                    admin.City = model.City ?? admin.City;
                    admin.State = model.State ?? admin.State;
                    admin.PinCode = model.Pincode ?? admin.PinCode;
                    if (!string.IsNullOrEmpty(photoUrl)) admin.ProfileImage = photoUrl;
                    _context.Admins.Update(admin);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Profile updated successfully!", photoUrl });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound(new { success = false, message = "User not found." });

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { success = false, message = errors });
            }

            return Ok(new { success = true, message = "Password changed successfully!" });
        }
    }

    public class ProfileUpdateDto
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? OrganisationName { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public IFormFile? ProfilePhoto { get; set; }
    }

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }
}