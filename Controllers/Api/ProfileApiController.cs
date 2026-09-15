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

            // Student specific variables
            string standard = "";
            string division = "";
            string guardianName = "";
            string guardianEmail = "";
            string guardianMobile = "";
            string studentInstituteName = "";
            int? studentInstituteId = null;

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
            else if (primaryRole.Contains("Student", StringComparison.OrdinalIgnoreCase))
            {
                if (customUser != null)
                {
                    // Use Projection (.Select) to safely handle NULL values from database
                    var studentInfo = await _context.WJ_Students
                        .Where(s => s.StudentID == customUser.UserID)
                        .Select(s => new {
                            Standard = s.Standard ?? "Not Updated",
                            Division = s.Division ?? "Not Updated",
                            GardianName = s.GardianName ?? "Not Updated",
                            GuardianEmailID = s.GuardianEmailID ?? "Not Updated",
                            GuardianMobileNo = s.GuardianMobileNo ?? "Not Updated"
                        })
                        .FirstOrDefaultAsync();

                    if (studentInfo != null)
                    {
                        standard = studentInfo.Standard;
                        division = studentInfo.Division;
                        guardianName = studentInfo.GardianName;
                        guardianEmail = studentInfo.GuardianEmailID;
                        guardianMobile = studentInfo.GuardianMobileNo;
                    }

                    // Fetch InstituteID from WJ_StudentAssociates safely
                    var studentAssociate = await _context.WJ_StudentAssociates
                        .Where(sa => sa.StudentID == customUser.UserID)
                        .FirstOrDefaultAsync();

                    if (studentAssociate != null)
                    {
                        studentInstituteId = studentAssociate.InstituteID;
                        var institute = await _context.WJ_Institutes
                            .Where(i => i.InstituteID == studentAssociate.InstituteID)
                            .FirstOrDefaultAsync();

                        studentInstituteName = institute?.Name ?? "Not Updated";
                    }
                }
            }

            // Map Single Character Gender (M/F/O) to Full Text for UI Dropdown selection
            string rawGender = customUser?.Gender?.Trim().ToUpper() ?? "";
            string mappedGender = "";
            if (rawGender == "M") mappedGender = "Male";
            else if (rawGender == "F") mappedGender = "Female";
            else if (rawGender == "O") mappedGender = "Other";

            var profileData = new
            {
                firstName = customUser?.FirstName ?? "",
                lastName = customUser?.LastName ?? "",
                gender = mappedGender,
                organisationName = orgName,
                address = customUser?.AddressLine1 ?? "",
                city = customUser?.CityName ?? "",
                state = customUser?.AddressLine3 ?? "", // State is stored in AddressLine3
                pincode = customUser?.Pincode ?? "",
                mobileNo = customUser?.MobileNo ?? user.PhoneNumber ?? "",
                photoUrl = string.IsNullOrEmpty(customUser?.PhotoUrl) ? "/uploads/profiles/default-avatar.png" : customUser.PhotoUrl,

                // Student Info Fields
                standard = standard,
                division = division,
                guardianName = guardianName,
                guardianEmail = guardianEmail,
                guardianMobile = guardianMobile,
                studentInstituteName = studentInstituteName,
                studentInstituteId = studentInstituteId
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

        // =========================================================
        // INSTITUTES SEARCH ENDPOINT (For Searchable Dropdown)
        // =========================================================
        [HttpGet("institutes")]
        public async Task<IActionResult> GetInstitutes([FromQuery] string? term)
        {
            var query = _context.WJ_Institutes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(term))
            {
                query = query.Where(i => i.Name != null && i.Name.ToLower().Contains(term.ToLower()));
            }

            var list = await query
                .Select(i => new { id = i.InstituteID, text = i.Name })
                .Take(20)
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateProfile([FromForm] ProfileUpdateDto model)
        {
            try
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

                var user = await _userManager.FindByIdAsync(userId);

                if (user == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "User not found."
                    });
                }

                var roles = await _userManager.GetRolesAsync(user);
                string primaryRole = roles.FirstOrDefault() ?? "User";

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

                // Safely format Gender to fit single character (M, F, O)
                string? formattedGender = null;
                if (!string.IsNullOrWhiteSpace(model.Gender))
                {
                    formattedGender = model.Gender.Trim().Substring(0, 1).ToUpper();
                }

                // =========================================================
                // 5. If custom Users record does not exist
                // =========================================================
                if (customUser == null)
                {
                    var newCustomUser = new Users
                    {
                        AspNetUserID = userId,
                        EmailID = user.Email,
                        FirstName = string.IsNullOrWhiteSpace(model.FirstName) ? null : model.FirstName.Trim(),
                        LastName = string.IsNullOrWhiteSpace(model.LastName) ? null : model.LastName.Trim(),
                        Gender = primaryRole.Contains("Student", StringComparison.OrdinalIgnoreCase) ? formattedGender : null,
                        MobileNo = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                        AddressLine1 = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim(),
                        AddressLine3 = string.IsNullOrWhiteSpace(model.State) ? null : model.State.Trim(),
                        CityName = string.IsNullOrWhiteSpace(model.City) ? null : model.City.Trim(),
                        Pincode = string.IsNullOrWhiteSpace(model.Pincode) ? null : model.Pincode.Trim(),
                        PhotoUrl = photoUrl ?? "/uploads/profiles/default-avatar.png",
                        IsActive = true,
                        CreatedBy = 1,
                        CreatedDate = DateTime.UtcNow
                    };

                    _context.Users.Add(newCustomUser);
                    await _context.SaveChangesAsync();

                    if (primaryRole.Contains("Student", StringComparison.OrdinalIgnoreCase))
                    {
                        await UpsertStudentDetailsAsync(newCustomUser.UserID, model);
                    }

                    return Ok(new
                    {
                        success = true,
                        message = "Profile updated successfully!",
                        photoUrl = newCustomUser.PhotoUrl
                    });
                }

                // =========================================================
                // 6. Update existing Users record directly (ExecuteUpdateAsync)
                // =========================================================
                int affectedRows = await _context.Users
                    .Where(u => u.UserID == customUser.UserID)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            u => u.FirstName,
                            string.IsNullOrWhiteSpace(model.FirstName) ? customUser.FirstName : model.FirstName.Trim()
                        )
                        .SetProperty(
                            u => u.LastName,
                            string.IsNullOrWhiteSpace(model.LastName) ? customUser.LastName : model.LastName.Trim()
                        )
                        .SetProperty(
                            u => u.Gender,
                            formattedGender ?? customUser.Gender
                        )
                        .SetProperty(
                            u => u.MobileNo,
                            string.IsNullOrWhiteSpace(model.PhoneNumber) ? customUser.MobileNo : model.PhoneNumber.Trim()
                        )
                        .SetProperty(
                            u => u.AddressLine1,
                            string.IsNullOrWhiteSpace(model.Address) ? customUser.AddressLine1 : model.Address.Trim()
                        )
                        .SetProperty(
                            u => u.AddressLine3,
                            string.IsNullOrWhiteSpace(model.State) ? customUser.AddressLine3 : model.State.Trim()
                        )
                        .SetProperty(
                            u => u.CityName,
                            string.IsNullOrWhiteSpace(model.City) ? customUser.CityName : model.City.Trim()
                        )
                        .SetProperty(
                            u => u.Pincode,
                            string.IsNullOrWhiteSpace(model.Pincode) ? customUser.Pincode : model.Pincode.Trim()
                        )
                        .SetProperty(
                            u => u.PhotoUrl,
                            string.IsNullOrWhiteSpace(photoUrl) ? customUser.PhotoUrl : photoUrl
                        )
                        .SetProperty(
                            u => u.ModifiedDate,
                            DateTime.UtcNow
                        )
                    );

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
                // 7. Update WJ_Students if role is Student
                // =========================================================
                if (primaryRole.Contains("Student", StringComparison.OrdinalIgnoreCase))
                {
                    await UpsertStudentDetailsAsync(customUser.UserID, model);
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
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Internal Server Error: " + ex.Message,
                    details = ex.InnerException?.Message
                });
            }
        }

        // Helper method to insert or update WJ_Students table data safely + Institute Association
        private async Task UpsertStudentDetailsAsync(int studentId, ProfileUpdateDto model)
        {
            var studentRecord = await _context.WJ_Students
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (studentRecord != null)
            {
                studentRecord.Standard = string.IsNullOrWhiteSpace(model.Standard) ? studentRecord.Standard : model.Standard.Trim();
                studentRecord.Division = string.IsNullOrWhiteSpace(model.Division) ? studentRecord.Division : model.Division.Trim();
                studentRecord.GardianName = string.IsNullOrWhiteSpace(model.GuardianName) ? studentRecord.GardianName : model.GuardianName.Trim();
                studentRecord.GuardianEmailID = string.IsNullOrWhiteSpace(model.GuardianEmail) ? studentRecord.GuardianEmailID : model.GuardianEmail.Trim();
                studentRecord.GuardianMobileNo = string.IsNullOrWhiteSpace(model.GuardianMobile) ? studentRecord.GuardianMobileNo : model.GuardianMobile.Trim();

                _context.WJ_Students.Update(studentRecord);
            }
            else
            {
                var newStudent = new WJ_Students
                {
                    StudentID = studentId,
                    Standard = model.Standard?.Trim(),
                    Division = model.Division?.Trim(),
                    GardianName = model.GuardianName?.Trim(),
                    GuardianEmailID = model.GuardianEmail?.Trim(),
                    GuardianMobileNo = model.GuardianMobile?.Trim()
                };
                _context.WJ_Students.Add(newStudent);
            }

            // Update Institute association if selected from searchable dropdown
            if (model.InstituteID.HasValue && model.InstituteID.Value > 0)
            {
                var associate = await _context.WJ_StudentAssociates
                    .FirstOrDefaultAsync(sa => sa.StudentID == studentId);

                if (associate != null)
                {
                    associate.InstituteID = model.InstituteID.Value;
                    _context.WJ_StudentAssociates.Update(associate);
                }
                else
                {
                    _context.WJ_StudentAssociates.Add(new WJ_StudentAssociates
                    {
                        StudentID = studentId,
                        InstituteID = model.InstituteID.Value,
                        IsPrimaryAssociate = true // Assuming "1" indicates primary association
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
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

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "User not authenticated."
                });
            }

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

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Current password is not configured."
                });
            }

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

            var newPasswordHash =
                passwordHasher.HashPassword(
                    user,
                    model.NewPassword
                );

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

            await _context
                .Volunteers
                .Where(v => v.AspNetUserId == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        v => v.Password_,
                        model.NewPassword
                    )
                );

            return Ok(new
            {
                success = true,
                message = "Password changed successfully!"
            });
        }
    }

    public class ProfileUpdateDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public IFormFile? ProfilePhoto { get; set; }

        // Student specific properties
        public string? Gender { get; set; }
        public string? Standard { get; set; }
        public string? Division { get; set; }
        public string? GuardianName { get; set; }
        public string? GuardianEmail { get; set; }
        public string? GuardianMobile { get; set; }
        public int? InstituteID { get; set; } // Added for school dropdown mapping
    }

    public class ChangePasswordDto
    {
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }
}