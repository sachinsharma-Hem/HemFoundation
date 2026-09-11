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
                state = customUser?.AddressLine3 ?? "", // State is stored in AddressLine3
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
        public async Task<IActionResult> UpdateProfile([FromForm] ProfileUpdateDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "User not authenticated."
                });
            }

            // =========================================================
            // 1. Get Identity User
            // FindByIdAsync does NOT require NormalizedUserName
            // =========================================================
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "User not found."
                });
            }

            // =========================================================
            // 2. Handle Profile Photo
            // =========================================================
            string? photoUrl = null;

            if (model.ProfilePhoto != null && model.ProfilePhoto.Length > 0)
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
                    $"{Guid.NewGuid()}_{Path.GetFileName(model.ProfilePhoto.FileName)}";

                string filePath = Path.Combine(
                    uploadsFolder,
                    uniqueFileName
                );

                await using (var fileStream = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await model.ProfilePhoto.CopyToAsync(fileStream);
                }

                photoUrl = $"/uploads/profiles/Updates/{uniqueFileName}";
            }

            // =========================================================
            // 3. Update AspNetUsers directly
            // DO NOT use _userManager.UpdateAsync()
            // because NormalizedUserName is ignored/unmapped
            // =========================================================
            if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                int identityRows = await _context.Set<ApplicationUser>()
    .Where(u => u.Id == userId)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(
            u => u.PhoneNumber,
            model.PhoneNumber.Trim()
        )
    );

                if (identityRows == 0)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Phone number could not be updated in AspNetUsers."
                    });
                }
            }

            // =========================================================
            // 4. Find custom Users record
            // =========================================================
            var customUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.AspNetUserID == userId);

            // =========================================================
            // 5. If custom Users record does not exist
            // =========================================================
            if (customUser == null)
            {
                var newCustomUser = new Users
                {
                    AspNetUserID = userId,

                    EmailID = user.Email,

                    FirstName = string.IsNullOrWhiteSpace(model.FirstName)
                        ? null
                        : model.FirstName.Trim(),

                    LastName = string.IsNullOrWhiteSpace(model.LastName)
                        ? null
                        : model.LastName.Trim(),

                    MobileNo = string.IsNullOrWhiteSpace(model.PhoneNumber)
                        ? null
                        : model.PhoneNumber.Trim(),

                    AddressLine1 = string.IsNullOrWhiteSpace(model.Address)
                        ? null
                        : model.Address.Trim(),

                    AddressLine3 = string.IsNullOrWhiteSpace(model.State)
                        ? null
                        : model.State.Trim(),

                    CityName = string.IsNullOrWhiteSpace(model.City)
                        ? null
                        : model.City.Trim(),

                    Pincode = string.IsNullOrWhiteSpace(model.Pincode)
                        ? null
                        : model.Pincode.Trim(),

                    PhotoUrl = photoUrl
                        ?? "/uploads/profiles/default-avatar.png",

                    IsActive = true,

                    CreatedBy = 1,

                    CreatedDate = DateTime.UtcNow
                };

                _context.Users.Add(newCustomUser);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Profile updated successfully!",
                    photoUrl = newCustomUser.PhotoUrl
                });
            }

            // =========================================================
            // 6. Update existing Users record directly
            // =========================================================
            int affectedRows = await _context.Users
                .Where(u => u.UserID == customUser.UserID)
                .ExecuteUpdateAsync(setters => setters

                    .SetProperty(
                        u => u.FirstName,
                        string.IsNullOrWhiteSpace(model.FirstName)
                            ? customUser.FirstName
                            : model.FirstName.Trim()
                    )

                    .SetProperty(
                        u => u.LastName,
                        string.IsNullOrWhiteSpace(model.LastName)
                            ? customUser.LastName
                            : model.LastName.Trim()
                    )

                    .SetProperty(
                        u => u.MobileNo,
                        string.IsNullOrWhiteSpace(model.PhoneNumber)
                            ? customUser.MobileNo
                            : model.PhoneNumber.Trim()
                    )

                    .SetProperty(
                        u => u.AddressLine1,
                        string.IsNullOrWhiteSpace(model.Address)
                            ? customUser.AddressLine1
                            : model.Address.Trim()
                    )

                    .SetProperty(
                        u => u.AddressLine3,
                        string.IsNullOrWhiteSpace(model.State)
                            ? customUser.AddressLine3
                            : model.State.Trim()
                    )

                    .SetProperty(
                        u => u.CityName,
                        string.IsNullOrWhiteSpace(model.City)
                            ? customUser.CityName
                            : model.City.Trim()
                    )

                    .SetProperty(
                        u => u.Pincode,
                        string.IsNullOrWhiteSpace(model.Pincode)
                            ? customUser.Pincode
                            : model.Pincode.Trim()
                    )

                    .SetProperty(
                        u => u.PhotoUrl,
                        string.IsNullOrWhiteSpace(photoUrl)
                            ? customUser.PhotoUrl
                            : photoUrl
                    )

                    .SetProperty(
                        u => u.ModifiedDate,
                        DateTime.UtcNow
                    )
                );

            // =========================================================
            // 7. Verify update
            // =========================================================
            if (affectedRows == 0)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Profile record could not be updated.",
                    userId = customUser.UserID
                });
            }

            // =========================================================
            // 8. Success
            // =========================================================
            return Ok(new
            {
                success = true,
                message = "Profile updated successfully!",
                photoUrl = string.IsNullOrWhiteSpace(photoUrl)
                    ? customUser.PhotoUrl
                    : photoUrl
            });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            // =========================================================
            // 1. Validate request
            // =========================================================

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

            if (model.NewPassword.Length < 6)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "New password must be at least 6 characters long."
                });
            }

            if (model.CurrentPassword == model.NewPassword)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "New password must be different from current password."
                });
            }


            // =========================================================
            // 2. Get logged-in User ID
            // =========================================================

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "User not authenticated."
                });
            }


            // =========================================================
            // 3. Get Identity User WITHOUT tracking
            // =========================================================

            var user = await _context
                .Set<ApplicationUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "User not found."
                });
            }


            // =========================================================
            // 4. Check existing password hash
            // =========================================================

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Current password is not configured."
                });
            }


            // =========================================================
            // 5. Verify Current Password
            // =========================================================

            var passwordHasher = new PasswordHasher<ApplicationUser>();

            var verificationResult =
                passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
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


            // =========================================================
            // 6. Generate NEW Password Hash
            // =========================================================

            var newPasswordHash =
                passwordHasher.HashPassword(
                    user,
                    model.NewPassword
                );


            // =========================================================
            // 7. Update aspnetusers.passwordhash
            //    HASH password will be stored here
            // =========================================================

            var identityRows = await _context
                .Set<ApplicationUser>()
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        u => u.PasswordHash,
                        newPasswordHash
                    )
                );

            if (identityRows == 0)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Password update failed. User record was not found."
                });
            }


            // =========================================================
            // 8. Update users.password_
            //    PLAIN TEXT password will be stored here
            // =========================================================

            var customUserRows = await _context
                .Users
                .Where(u => u.AspNetUserID == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        u => u.Password_,
                        model.NewPassword
                    )
                );

            if (customUserRows == 0)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Password hash was updated, but users password could not be updated."
                });
            }


            // =========================================================
            // 9. Success
            // =========================================================

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

//using HemFoundation.Data;
//using HemFoundation.Models.Entities;
//using HemFoundation.Models.Identity;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using System.Security.Claims;
//using Microsoft.AspNetCore.Authentication.JwtBearer;

//namespace HemFoundation.Controllers.Api
//{
//    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//    [Authorize]
//    [ApiController]
//    [Route("api/profile")]
//    public class ProfileApiController : ControllerBase
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly UserManager<ApplicationUser> _userManager;
//        private readonly IWebHostEnvironment _env;

//        public ProfileApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
//        {
//            _context = context;
//            _userManager = userManager;
//            _env = env;
//        }

//        [HttpGet("details")]
//        public async Task<IActionResult> GetProfileDetails()
//        {
//            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
//            if (string.IsNullOrEmpty(userId))
//                return Unauthorized(new { success = false, message = "User not authenticated." });

//            var user = await _userManager.FindByIdAsync(userId);
//            if (user == null)
//                return NotFound(new { success = false, message = "User not found." });

//            var roles = await _userManager.GetRolesAsync(user);
//            string primaryRole = roles.FirstOrDefault() ?? "User";

//            // Fetch custom profile details from 'users' table using AspNetUserID
//            var customUser = await _context.Users.FirstOrDefaultAsync(u => u.AspNetUserID == userId);
//            string orgName = "";

//            if (primaryRole.Equals("WJ_Affiliate", StringComparison.OrdinalIgnoreCase) || primaryRole.Equals("WJ_School", StringComparison.OrdinalIgnoreCase))
//            {
//                if (customUser != null)
//                {
//                    var institute = await _context.WJ_Institutes
//                        .Where(i => i.InstituteID == customUser.UserID)
//                        .FirstOrDefaultAsync();

//                    orgName = primaryRole.Equals("WJ_Affiliate", StringComparison.OrdinalIgnoreCase)
//                        ? (institute?.SocialOrgName ?? "")
//                        : (institute?.Name ?? "");
//                }
//            }

//            var profileData = new
//            {
//                firstName = customUser?.FirstName ?? "",
//                lastName = customUser?.LastName ?? "",
//                organisationName = orgName,
//                address = customUser?.AddressLine1 ?? "",
//                city = customUser?.CityName ?? "",
//                state = "", // Add state property if available in Users model
//                pincode = customUser?.Pincode ?? "",
//                mobileNo = customUser?.MobileNo ?? user.PhoneNumber ?? "",
//                photoUrl = string.IsNullOrEmpty(customUser?.PhotoUrl) ? "/uploads/profiles/default-avatar.png" : customUser.PhotoUrl
//            };

//            return Ok(new
//            {
//                success = true,
//                role = primaryRole,
//                user = new
//                {
//                    user.Id,
//                    fullName = (customUser != null ? $"{customUser.FirstName} {customUser.LastName}".Trim() : user.UserName),
//                    user.Email,
//                    phoneNumber = customUser?.MobileNo ?? user.PhoneNumber
//                },
//                profile = profileData
//            });
//        }
//        [HttpPost("update")]
//        public async Task<IActionResult> UpdateProfile([FromForm] ProfileUpdateDto model)
//        {
//            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

//            if (string.IsNullOrEmpty(userId))
//            {
//                return Unauthorized(new
//                {
//                    success = false,
//                    message = "User not authenticated."
//                });
//            }

//            // 1. Get Identity User
//            var user = await _userManager.FindByIdAsync(userId);

//            if (user == null)
//            {
//                return NotFound(new
//                {
//                    success = false,
//                    message = "User not found."
//                });
//            }

//            // 2. Handle Photo Upload
//            string photoUrl = string.Empty;

//            if (model.ProfilePhoto != null &&
//                model.ProfilePhoto.Length > 0)
//            {
//                string uploadsFolder = Path.Combine(
//                    _env.WebRootPath,
//                    "uploads",
//                    "profiles",
//                    "Updates"
//                );

//                if (!Directory.Exists(uploadsFolder))
//                {
//                    Directory.CreateDirectory(uploadsFolder);
//                }

//                string uniqueFileName =
//                    Guid.NewGuid().ToString() + "_" +
//                    Path.GetFileName(model.ProfilePhoto.FileName);

//                string filePath = Path.Combine(
//                    uploadsFolder,
//                    uniqueFileName
//                );

//                using (var fileStream = new FileStream(
//                    filePath,
//                    FileMode.Create,
//                    FileAccess.Write,
//                    FileShare.None))
//                {
//                    await model.ProfilePhoto.CopyToAsync(fileStream);
//                }

//                photoUrl =
//                    $"/uploads/profiles/Updates/{uniqueFileName}";
//            }

//            // 3. Update AspNetUsers directly
//            if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
//            {
//                user.PhoneNumber = model.PhoneNumber.Trim();

//                _context.Entry(user)
//                    .Property(x => x.PhoneNumber)
//                    .IsModified = true;
//            }

//            // 4. Get Custom Users record
//            var customUser = await _context.Users
//                .FirstOrDefaultAsync(u => u.AspNetUserID == userId);

//            if (customUser != null)
//            {
//                customUser.FirstName =
//                    string.IsNullOrWhiteSpace(model.FirstName)
//                        ? customUser.FirstName
//                        : model.FirstName.Trim();

//                customUser.LastName =
//                    string.IsNullOrWhiteSpace(model.LastName)
//                        ? customUser.LastName
//                        : model.LastName.Trim();

//                customUser.MobileNo =
//                    string.IsNullOrWhiteSpace(model.PhoneNumber)
//                        ? customUser.MobileNo
//                        : model.PhoneNumber.Trim();

//                customUser.AddressLine1 =
//                    string.IsNullOrWhiteSpace(model.Address)
//                        ? customUser.AddressLine1
//                        : model.Address.Trim();

//                customUser.CityName =
//                    string.IsNullOrWhiteSpace(model.City)
//                        ? customUser.CityName
//                        : model.City.Trim();

//                customUser.Pincode =
//                    string.IsNullOrWhiteSpace(model.Pincode)
//                        ? customUser.Pincode
//                        : model.Pincode.Trim();

//                if (!string.IsNullOrEmpty(photoUrl))
//                {
//                    customUser.PhotoUrl = photoUrl;
//                }
//            }
//            else
//            {
//                // 5. Create Users record if it does not exist
//                customUser = new Users
//                {
//                    AspNetUserID = userId,
//                    EmailID = user.Email,
//                    FirstName = model.FirstName,
//                    LastName = model.LastName,
//                    MobileNo = model.PhoneNumber,
//                    AddressLine1 = model.Address,
//                    CityName = model.City,
//                    Pincode = model.Pincode,
//                    PhotoUrl = string.IsNullOrEmpty(photoUrl)
//                        ? "/uploads/profiles/default-avatar.png"
//                        : photoUrl,
//                    IsActive = true,
//                    CreatedBy = 1,
//                    CreatedDate = DateTime.UtcNow
//                };

//                _context.Users.Add(customUser);
//            }

//            // 6. Save
//            await _context.SaveChangesAsync();

//            return Ok(new
//            {
//                success = true,
//                message = "Profile updated successfully!",
//                photoUrl = string.IsNullOrEmpty(photoUrl)
//                    ? customUser.PhotoUrl
//                    : photoUrl
//            });
//        }


//        [HttpPost("change-password")]
//        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
//        {
//            if (model == null ||
//                string.IsNullOrWhiteSpace(model.CurrentPassword) ||
//                string.IsNullOrWhiteSpace(model.NewPassword))
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Current password and new password are required."
//                });
//            }

//            if (string.IsNullOrWhiteSpace(model.NewPassword) ||
//                model.NewPassword.Length < 6)
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "New password must be at least 6 characters long."
//                });
//            }

//            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

//            if (string.IsNullOrEmpty(userId))
//            {
//                return Unauthorized(new
//                {
//                    success = false,
//                    message = "User not authenticated."
//                });
//            }

//            // Find user by Identity ID
//            var user = await _userManager.FindByIdAsync(userId);

//            if (user == null)
//            {
//                return NotFound(new
//                {
//                    success = false,
//                    message = "User not found."
//                });
//            }

//            // Verify current password directly
//            var passwordHasher = new PasswordHasher<ApplicationUser>();

//            var verificationResult = passwordHasher.VerifyHashedPassword(
//                user,
//                user.PasswordHash ?? string.Empty,
//                model.CurrentPassword
//            );

//            if (verificationResult == PasswordVerificationResult.Failed)
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Current password is incorrect."
//                });
//            }

//            // Generate new password hash
//            var newPasswordHash = passwordHasher.HashPassword(
//                user,
//                model.NewPassword
//            );

//            // Update password hash
//            user.PasswordHash = newPasswordHash;

//            _context.Entry(user)
//                .Property(x => x.PasswordHash)
//                .IsModified = true;

//            await _context.SaveChangesAsync();

//            return Ok(new
//            {
//                success = true,
//                message = "Password changed successfully!"
//            });
//        }
//    }

//    public class ProfileUpdateDto
//    {
//        public string? FullName { get; set; }
//        public string? PhoneNumber { get; set; }
//        public string? FirstName { get; set; }
//        public string? LastName { get; set; }
//        public string? OrganisationName { get; set; }
//        public string? Address { get; set; }
//        public string? City { get; set; }
//        public string? State { get; set; }
//        public string? Pincode { get; set; }
//        public IFormFile? ProfilePhoto { get; set; }
//    }

//    public class ChangePasswordDto
//    {
//        public string CurrentPassword { get; set; }
//        public string NewPassword { get; set; }
//    }
//}