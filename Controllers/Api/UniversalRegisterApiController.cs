using HemFoundation.Data;
using HemFoundation.DTOs;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Route("api/universal")]
    [ApiController]
    public class UniversalRegisterApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IWebHostEnvironment _env;

        public UniversalRegisterApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _env = env;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromForm] UniversalRegisterDto model,
            [FromForm] string? referredBy,
            [FromForm] string? standard,
            [FromForm] string? division,
            [FromForm] string? guardianName,
            [FromForm] string? qualification,
            [FromForm] string? relationWithStudent,
            [FromForm] string? gender)
        {
            if (!ModelState.IsValid)
            {
                var errorMessages = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault() ?? "Please fill all required fields correctly.";

                return BadRequest(new { success = false, message = errorMessages });
            }

            var portal = await _context.PortalMasters
                .FirstOrDefaultAsync(p => p.Id == model.PortalId && p.IsActive);

            if (portal == null)
                return BadRequest(new { success = false, message = "Invalid Portal selected." });

            string roleName = portal.PortalName;

            if (!await _roleManager.RoleExistsAsync(roleName))
                return BadRequest(new { success = false, message = $"Role '{roleName}' does not exist in system." });

            string baseMobile = model.MobileNo.Trim();
            string uniqueUsername = baseMobile;

            // Duplicate mobile number safe username generator
            int counter = 1;
            while (await _userManager.FindByNameAsync(uniqueUsername) != null)
            {
                uniqueUsername = $"{baseMobile}_{counter}";
                counter++;
            }

            string photoUrl = "-";
            if (model.ProfilePhoto != null && model.ProfilePhoto.Length > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles", roleName);
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ProfilePhoto.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfilePhoto.CopyToAsync(fileStream);
                }
                photoUrl = $"/uploads/profiles/{roleName}/{uniqueFileName}";
            }

            var appUser = new ApplicationUser
            {
                UserName = uniqueUsername,
                Email = model.EmailID,
                FullName = $"{model.FirstName} {model.LastName}",
                PhoneNumber = baseMobile,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(appUser, baseMobile);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { success = false, message = errors });
            }

            await _userManager.AddToRoleAsync(appUser, roleName);

            // Role-wise saving logic
            if (roleName == "Affiliate")
            {
                long count = await _context.Affiliates.CountAsync() + 1;
                var affiliate = new Affiliate
                {
                    UserCode = $"AFF{count:D3}",
                    OrganisationName = model.OrganisationName ?? "N/A",
                    TypeOfOrganisation = model.TypeOfOrganisation ?? "N/A",
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    MobileNo = baseMobile,
                    EmailID = model.EmailID,
                    AddressLine1 = model.Address,
                    BranchName = model.BranchName,
                    City = model.City,
                    State = model.State,
                    Pincode = model.Pincode,
                    AspNetUserID = appUser.Id,
                    RegisterFrom = roleName,
                    Password_ = baseMobile,
                    PhotoUrl = photoUrl,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                _context.Affiliates.Add(affiliate);
            }
            else if (roleName == "School")
            {
                var institute = new Institute
                {
                    InstituteType = "School",
                    Name = model.OrganisationName ?? "N/A",
                    TypeOfOrganisation = model.TypeOfOrganisation ?? "N/A",
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    MobileNo = baseMobile,
                    EmailID = model.EmailID,
                    Address = model.Address,
                    BranchName = model.BranchName,
                    City = model.City,
                    State = model.State,
                    Pincode = model.Pincode,
                    ProfileUrl = photoUrl,
                    AspNetUserID = appUser.Id,
                    ReferredByAffiliateId = referredBy,
                    IsActive = true,
                    IsVerify = false,
                    CreatedDate = DateTime.UtcNow
                };
                _context.Institutes.Add(institute);
            }
            else if (roleName == "Student")
            {
                var student = new Student
                {
                    InstituteID = null,
                    Standard = standard,
                    Division = division,
                    GardianName = guardianName,
                    GuardianEmailID = model.EmailID,
                    GuardianMobileNo = baseMobile,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    AspNetUserID = appUser.Id,
                    PhotoUrl = photoUrl,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                _context.Students.Add(student);
            }
            else if (roleName == "Teacher")
            {
                var teacher = new Teacher
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    MobileNo = baseMobile,
                    EmailID = model.EmailID,
                    Qualification = qualification ?? "Not Specified",
                    Address = model.Address,
                    City = model.City,
                    State = model.State,
                    Pincode = model.Pincode,
                    PhotoUrl = photoUrl,
                    AspNetUserID = appUser.Id,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                _context.Teachers.Add(teacher);
            }
            else if (roleName == "Parent")
            {
                var parent = new Parent
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    MobileNo = baseMobile,
                    EmailID = model.EmailID,
                    RelationWithStudent = relationWithStudent ?? "Guardian",
                    Address = model.Address,
                    City = model.City,
                    State = model.State,
                    Pincode = model.Pincode,
                    PhotoUrl = photoUrl,
                    AspNetUserID = appUser.Id,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                _context.Parents.Add(parent);
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"{roleName} registered successfully!" });
        }

        [HttpGet("affiliates-list")]
        public async Task<IActionResult> GetAffiliatesList()
        {
            var list = await _context.Affiliates
                .Where(a => a.IsActive)
                .Select(a => new { id = a.UserCode, name = $"{a.FirstName} {a.LastName} ({a.OrganisationName})" })
                .ToListAsync();
            return Ok(list);
        }

        [HttpGet("standards-list")]
        public async Task<IActionResult> GetStandardsList()
        {
            var list = await _context.StandardMasters
                .Where(s => s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .Select(s => new { id = s.StandardName, name = s.StandardName })
                .ToListAsync();
            return Ok(list);
        }
    }
}