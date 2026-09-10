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

            // Fetch custom profile details from 'users' table using AspNetUserID
            var customUser = await _context.Users.FirstOrDefaultAsync(u => u.AspNetUserID == userId);
            string orgName = "";

            if (primaryRole.Equals("WJ_Affiliate", StringComparison.OrdinalIgnoreCase) || primaryRole.Equals("WJ_School", StringComparison.OrdinalIgnoreCase))
            {
                if (customUser != null)
                {
                    var institute = await _context.WJ_Institutes
                        .Where(i => i.InstituteID == customUser.UserID)
                        .FirstOrDefaultAsync();

                    orgName = primaryRole.Equals("WJ_Affiliate", StringComparison.OrdinalIgnoreCase)
                        ? (institute?.SocialOrgName ?? "")
                        : (institute?.Name ?? "");
                }
            }

            var profileData = new
            {
                firstName = customUser?.FirstName ?? "",
                lastName = customUser?.LastName ?? "",
                organisationName = orgName,
                address = customUser?.AddressLine1 ?? "",
                city = customUser?.CityName ?? "",
                state = "", // Add state property if available in Users model
                pincode = customUser?.Pincode ?? "",
                mobileNo = customUser?.MobileNo ?? user.PhoneNumber ?? "",
                photoUrl = string.IsNullOrEmpty(customUser?.PhotoUrl) ? "/uploads/profiles/default-avatar.png" : customUser.PhotoUrl
            };

            return Ok(new
            {
                success = true,
                role = primaryRole,
                user = new
                {
                    user.Id,
                    fullName = (customUser != null ? $"{customUser.FirstName} {customUser.LastName}".Trim() : user.UserName),
                    user.Email,
                    phoneNumber = customUser?.MobileNo ?? user.PhoneNumber
                },
                profile = profileData
            });
        }
        [HttpPost("update")]
        public async Task<IActionResult> UpdateProfile(
    [FromForm] ProfileUpdateDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "User not authenticated."
                });
            }

            // 1. Get Identity User
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "User not found."
                });
            }

            // 2. Handle Photo Upload
            string photoUrl = string.Empty;

            if (model.ProfilePhoto != null &&
                model.ProfilePhoto.Length > 0)
            {
                string uploadsFolder = Path.Combine(
                    _env.WebRootPath,
                    "uploads",
                    "profiles",
                    "Updates"
                );

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string uniqueFileName =
                    Guid.NewGuid().ToString() + "_" +
                    Path.GetFileName(model.ProfilePhoto.FileName);

                string filePath = Path.Combine(
                    uploadsFolder,
                    uniqueFileName
                );

                using (var fileStream = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await model.ProfilePhoto.CopyToAsync(fileStream);
                }

                photoUrl =
                    $"/uploads/profiles/Updates/{uniqueFileName}";
            }

            // 3. Update AspNetUsers directly
            if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                user.PhoneNumber = model.PhoneNumber.Trim();

                _context.Entry(user)
                    .Property(x => x.PhoneNumber)
                    .IsModified = true;
            }

            // 4. Get Custom Users record
            var customUser = await _context.Users
                .FirstOrDefaultAsync(u => u.AspNetUserID == userId);

            if (customUser != null)
            {
                customUser.FirstName =
                    string.IsNullOrWhiteSpace(model.FirstName)
                        ? customUser.FirstName
                        : model.FirstName.Trim();

                customUser.LastName =
                    string.IsNullOrWhiteSpace(model.LastName)
                        ? customUser.LastName
                        : model.LastName.Trim();

                customUser.MobileNo =
                    string.IsNullOrWhiteSpace(model.PhoneNumber)
                        ? customUser.MobileNo
                        : model.PhoneNumber.Trim();

                customUser.AddressLine1 =
                    string.IsNullOrWhiteSpace(model.Address)
                        ? customUser.AddressLine1
                        : model.Address.Trim();

                customUser.CityName =
                    string.IsNullOrWhiteSpace(model.City)
                        ? customUser.CityName
                        : model.City.Trim();

                customUser.Pincode =
                    string.IsNullOrWhiteSpace(model.Pincode)
                        ? customUser.Pincode
                        : model.Pincode.Trim();

                if (!string.IsNullOrEmpty(photoUrl))
                {
                    customUser.PhotoUrl = photoUrl;
                }
            }
            else
            {
                // 5. Create Users record if it does not exist
                customUser = new Users
                {
                    AspNetUserID = userId,
                    EmailID = user.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    MobileNo = model.PhoneNumber,
                    AddressLine1 = model.Address,
                    CityName = model.City,
                    Pincode = model.Pincode,
                    PhotoUrl = string.IsNullOrEmpty(photoUrl)
                        ? "/uploads/profiles/default-avatar.png"
                        : photoUrl,
                    IsActive = true,
                    CreatedBy = 1,
                    CreatedDate = DateTime.UtcNow
                };

                _context.Users.Add(customUser);
            }

            // 6. Save
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Profile updated successfully!",
                photoUrl = string.IsNullOrEmpty(photoUrl)
                    ? customUser.PhotoUrl
                    : photoUrl
            });
        }
        //[HttpPost("update")]
        //public async Task<IActionResult> UpdateProfile([FromForm] ProfileUpdateDto model)
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    if (string.IsNullOrEmpty(userId))
        //        return Unauthorized(new { success = false, message = "User not authenticated." });

        //    var user = await _userManager.FindByIdAsync(userId);
        //    if (user == null)
        //        return NotFound(new { success = false, message = "User not found." });

        //    // Handle Photo Upload
        //    string photoUrl = string.Empty;
        //    if (model.ProfilePhoto != null && model.ProfilePhoto.Length > 0)
        //    {
        //        string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles", "Updates");
        //        if (!Directory.Exists(uploadsFolder))
        //            Directory.CreateDirectory(uploadsFolder);

        //        string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ProfilePhoto.FileName;
        //        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

        //        using (var fileStream = new FileStream(filePath, FileMode.Create))
        //        {
        //            await model.ProfilePhoto.CopyToAsync(fileStream);
        //        }
        //        photoUrl = $"/uploads/profiles/Updates/{uniqueFileName}";
        //    }

        //    // Update AspNetUsers phone number if provided
        //    if (!string.IsNullOrEmpty(model.PhoneNumber))
        //    {
        //        user.PhoneNumber = model.PhoneNumber;
        //        await _userManager.SetPhoneNumberAsync(user, model.PhoneNumber);
        //    }

        //    // Update Custom 'Users' Table
        //    var customUser = await _context.Users.FirstOrDefaultAsync(u => u.AspNetUserID == userId);
        //    if (customUser != null)
        //    {
        //        customUser.FirstName = model.FirstName ?? customUser.FirstName;
        //        customUser.LastName = model.LastName ?? customUser.LastName;
        //        customUser.MobileNo = model.PhoneNumber ?? customUser.MobileNo;
        //        customUser.AddressLine1 = model.Address ?? customUser.AddressLine1;
        //        customUser.CityName = model.City ?? customUser.CityName;
        //        customUser.Pincode = model.Pincode ?? customUser.Pincode;

        //        if (!string.IsNullOrEmpty(photoUrl))
        //        {
        //            customUser.PhotoUrl = photoUrl;
        //        }

        //        _context.Users.Update(customUser);
        //        await _context.SaveChangesAsync();
        //    }
        //    else
        //    {
        //        // Fallback if custom user row doesn't exist yet
        //        var newUserRecord = new Users
        //        {
        //            AspNetUserID = userId,
        //            EmailID = user.Email,
        //            FirstName = model.FirstName,
        //            LastName = model.LastName,
        //            MobileNo = model.PhoneNumber,
        //            AddressLine1 = model.Address,
        //            CityName = model.City,
        //            Pincode = model.Pincode,
        //            PhotoUrl = string.IsNullOrEmpty(photoUrl) ? "/uploads/profiles/default-avatar.png" : photoUrl,
        //            CreatedDate = DateTime.Now
        //        };
        //        _context.Users.Add(newUserRecord);
        //        await _context.SaveChangesAsync();
        //    }

        //    return Ok(new { success = true, message = "Profile updated successfully!", photoUrl = string.IsNullOrEmpty(photoUrl) ? customUser?.PhotoUrl : photoUrl });
        //}

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(
    [FromBody] ChangePasswordDto model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.CurrentPassword) ||
                string.IsNullOrWhiteSpace(model.NewPassword))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Current password and new password are required."
                });
            }

            if (string.IsNullOrWhiteSpace(model.NewPassword) ||
                model.NewPassword.Length < 6)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "New password must be at least 6 characters long."
                });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "User not authenticated."
                });
            }

            // Find user by Identity ID
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "User not found."
                });
            }

            // Verify current password directly
            var passwordHasher = new PasswordHasher<ApplicationUser>();

            var verificationResult = passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash ?? string.Empty,
                model.CurrentPassword
            );

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Current password is incorrect."
                });
            }

            // Generate new password hash
            var newPasswordHash = passwordHasher.HashPassword(
                user,
                model.NewPassword
            );

            // Update password hash
            user.PasswordHash = newPasswordHash;

            _context.Entry(user)
                .Property(x => x.PasswordHash)
                .IsModified = true;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Password changed successfully!"
            });
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