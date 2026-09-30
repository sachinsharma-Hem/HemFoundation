using DocumentFormat.OpenXml.Spreadsheet;
using HemFoundation.Common;
using HemFoundation.Data;
using HemFoundation.DTOs;
using HemFoundation.Migrations;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; 

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
        private readonly SMSManager _smsManager;

        public UniversalRegisterApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IWebHostEnvironment env, SMSManager smsManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _env = env;
            _smsManager = smsManager;
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
     [FromForm] string? gender,
     [FromForm] string? role)
        {

            if (!ModelState.IsValid)
            {
                var errorMessages = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault() ?? "Please fill all required fields correctly.";

                return BadRequest(new { success = false, message = errorMessages });
            }
            string messagerolename = "";
            var portal = await _context.PortalMasters
                .FirstOrDefaultAsync(p => p.Id == model.PortalId && p.IsActive);

            if (portal == null)
                return BadRequest(new { success = false, message = "Invalid Portal selected." });

            string roleName = role ?? portal.PortalName;

            bool roleExists = await HemFoundation.Common.Utility.CheckRoleExistsAsync(_context, roleName);
            if (!roleExists)
            {
                return BadRequest(new { success = false, message = $"Role '{roleName}' does not exist in system." });
            }

            string baseMobile = model.MobileNo.Trim();
            string uniqueUsername = baseMobile;

            int counter = 1;
            while (await HemFoundation.Common.Utility.CheckUsernameExistsAsync(_context, uniqueUsername))
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

                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await model.ProfilePhoto.CopyToAsync(fileStream);
                }
                photoUrl = $"/uploads/profiles/{roleName}/{uniqueFileName}";
            }

            string aspNetUserId = await HemFoundation.Common.Utility.CreateIdentityUserRawAsync(_context, uniqueUsername, model.EmailID, baseMobile, roleName);
            string? genderCode = gender?.Trim().ToLower() switch
            {
                "male" or "m" => "1",
                "female" or "f" => "2",
                "other" or "o" => "3",
                _ => null
            };
            var coreUser = new HemFoundation.Models.Entities.Users
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Gender = gender?.Trim().ToUpper(),
                MobileNo = baseMobile,
                EmailID = model.EmailID,
                IsActive = true,
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow,
                AspNetUserID = aspNetUserId,
                Password_ = baseMobile,
                RegisterFrom = "HemVirtue",
                CityName = model.City,
                Pincode = model.Pincode,
                AddressLine1 = model.Address,
                AddressLine3 = model.State,
                PhotoUrl=photoUrl
            };
            _context.Users.Add(coreUser);
            await _context.SaveChangesAsync();

            int generatedUserId = coreUser.UserID; 

            int? mappedInstituteId = null;
            int? currentUserId = HttpContext.Session.GetInt32("UserID");

            if (currentUserId == null || currentUserId == 0)
            {
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                currentUserId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }
            if (!string.IsNullOrEmpty(currentUserId?.ToString()) && currentUserId > 0)
            {
                var inst = await _context.WJ_Institutes.FirstOrDefaultAsync(i => i.InstituteID == currentUserId);
                if (inst != null) mappedInstituteId = inst.InstituteID;
            }

            String roleNAME = HttpContext.Session.GetString("UserRole") ?? "NA";

            if (roleName == "WJ_School")
            {
                var referralCode = await _context.WJ_ReferralCodes
                .Where(x => x.UserID ==Convert.ToInt32(referredBy))
                .Select(x => x.ReferralCode)
                .FirstOrDefaultAsync();
                messagerolename = "School";
                var institute = new WJ_Institutes
                {
                    InstituteID = generatedUserId,

                    InstituteType = "School",

                    Name = model.OrganisationName ?? "N/A",

                    TypeOfOrganisation = model.TypeOfOrganisation ?? "N/A",

                    ProfileUrl = uniqueUsername,

                    IsActive = true,

                    IsVerify = true,

                    ReferralCode = referralCode
                };

                _context.WJ_Institutes.Add(institute);
            }
            else if (roleName == "WJ_Students")
            {
                messagerolename = "Student";
                var student = new WJ_Students
                {
                    StudentID = generatedUserId,
                    Standard = standard,
                    Division = division,
                    GardianName = guardianName,
                    ReferralCode = referredBy,
                    RegistrationAmount = 1000,
                    ProfileUrl = uniqueUsername,
                    IsActive = true,
                    IsPaid = false,
                    SharingPermission = false
                };

                _context.WJ_Students.Add(student);

                if (!string.IsNullOrEmpty(referredBy))
                {
                    var refCodeRecord = await _context.WJ_ReferralCodes
                        .FirstOrDefaultAsync(rc =>
                            rc.ReferralCode == referredBy &&
                            rc.IsActive == true);

                    if (refCodeRecord != null)
                    {
                        int referralUserId = refCodeRecord.UserID ?? 0;

                        bool isInstitute = await _context.WJ_Institutes
                            .AnyAsync(i => i.InstituteID == referralUserId);

                        if (isInstitute)
                        {
                            _context.WJ_StudentAssociates.Add(
                                new WJ_StudentAssociates
                                {
                                    StudentID = generatedUserId,
                                    InstituteID = referralUserId,
                                    IsPrimaryAssociate = true
                                });
                        }
                    }
                }
                else if (mappedInstituteId.HasValue &&
                         mappedInstituteId.Value > 0)
                {
                    _context.WJ_StudentAssociates.Add(
                        new WJ_StudentAssociates
                        {
                            StudentID = generatedUserId,
                            InstituteID = mappedInstituteId.Value,
                            IsPrimaryAssociate = true
                        });
                }
            }
            else if (roleName == "WJ_Teacher")
            {
                messagerolename = "Teacher";
                var teacher = new WJ_Teacher
                {
                    TeacherID = generatedUserId,
                    Standard = standard,
                    Division = division,
                    ProfileUrl = uniqueUsername
                };

                _context.WJ_Teachers.Add(teacher);

                if (mappedInstituteId.HasValue &&
                    mappedInstituteId.Value > 0)
                {
                    _context.WJ_TeacherAssociates.Add(
                        new WJ_TeacherAssociates
                        {
                            TeacherID = generatedUserId,
                            InstituteID = mappedInstituteId.Value,
                            IsPrimaryAssociate = true
                        });
                }
            }
            else if (roleName == "WJ_Affiliate")
            {
                messagerolename = "Affiliate";
                var affiliate = new WJ_Institutes
                {
                    InstituteID = generatedUserId,

                    SocialOrgName = model.OrganisationName ?? "N/A",

                    TypeOfOrganisation = model.TypeOfOrganisation ?? "N/A",

                    ProfileUrl = uniqueUsername,

                    InstituteType = "Affiliate",

                    IsActive = true,

                    IsVerify = false
                };

                _context.WJ_Institutes.Add(affiliate);
            }
            else if (roleName == "HF_Volunteers")
            {
                messagerolename = "Volunteer";

                int associateintid = 0;
                int associateaffiliate = 0;

                if (roleNAME == "WJ_School")
                {
                    associateintid = currentUserId ?? 0;
                }
                else if (roleNAME == "WJ_Affiliate")
                {
                    associateaffiliate = currentUserId ?? 0;
                }

                // If AffiliateId is not directly available,
                // get AffiliateId using InstituteId -> ReferralCode -> UserID
                if (associateaffiliate == 0 && associateintid > 0)
                {
                    associateaffiliate =
                        (from institute in _context.WJ_Institutes
                         join referral in _context.WJ_ReferralCodes
                             on institute.ReferralCode equals referral.ReferralCode
                         where institute.InstituteID == associateintid
                         select referral.UserID)
                        .FirstOrDefault() ?? 0;
                }

                var volunteer = new Volunteer
                {
                    Id = generatedUserId,

                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    MobileNo = model.MobileNo,
                    Email = model.EmailID,

                    InstituteId = associateintid,
                    AffiliateId = associateaffiliate,

                    AspNetUserId = aspNetUserId,
                    Password_ = model.MobileNo,

                    IsActive = true,
                    IsDeleted = false
                };

                _context.Volunteers.Add(volunteer);
            }

            await _context.SaveChangesAsync();

            try
            {
               
                if (!string.IsNullOrEmpty(baseMobile))
                {
                    _smsManager.StudentRegistration(baseMobile, model.FirstName, uniqueUsername, baseMobile);
                    if (roleName == "WJ_Students")
                    {
                       
                    }
                    else
                    {
                        
                    }
                }

                if (!string.IsNullOrEmpty(model.EmailID))
                {
                    var studentRegObj = new StudentRegistration
                    {
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        EmailID = model.EmailID,
                        UserName = uniqueUsername,
                        Password = baseMobile,
                        MobileNo = baseMobile
                    };
                    Utility.StudentRegistration(studentRegObj);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Notification dispatch failed: " + ex.ToString());
            }

            return Ok(new { success = true, message = $"{messagerolename} registered successfully!" });
        }
        public async Task<IActionResult> Registerold(
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

            // =========================================================================
            // UPDATE: Logged-in school user ki InstituteID nikalne ke liye logic
            // =========================================================================
            int? mappedInstituteId = null;
            string? currentUserId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(currentUserId))
            {
                var inst = await _context.Institutes.FirstOrDefaultAsync(i => i.AspNetUserID == currentUserId);
                if (inst != null) mappedInstituteId = inst.InstituteID;
            }

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
                    InstituteID = mappedInstituteId, 
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
                    InstituteID = mappedInstituteId, 
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
            var list = await _context.View_HF_Affiliate
                .AsNoTracking()
                .Where(x => x.SchoolCount >= 5)
                .Select(x => new
                {
                    id = x.InstituteID,
                    name = (x.FirstName ?? "") + " " + (x.LastName ?? "")
                })
                .Distinct()
                .OrderBy(x => x.name)
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


        [HttpPost("bulk-upload")]
        public async Task<IActionResult> BulkUpload([FromForm] IFormFile excelFile, [FromForm] string uploadType)
        {
            if (excelFile == null || excelFile.Length == 0)
                return BadRequest(new { success = false, message = "Please select a valid Excel file." });
            string extension = Path.GetExtension(excelFile.FileName).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
            {
                return BadRequest(new { success = false, message = "Only Excel files (.xlsx or .xls) are allowed." });
            }
            int? currentUserId = HttpContext.Session.GetInt32("UserID");

            if (currentUserId == null || currentUserId == 0)
            {
                // Fall back to claims if session is missing
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                currentUserId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }

            if (currentUserId == null || currentUserId == 0)
            {
                return Unauthorized(new { success = false, message = "User not logged in." });
            }

            var institute = await _context.SchoolProfileDetails.FirstOrDefaultAsync(i => i.instituteid == currentUserId);

            if (institute == null)
                return BadRequest(new { success = false, message = "Authorized school institute not found." });

            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "bulkUploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = $"{institute.institute_name?.Replace(" ", "_")}_{uploadType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(excelFile.FileName)}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await excelFile.CopyToAsync(stream);
            }

            var log = new BulkUploadLog
            {
                InstituteID = institute.instituteid,
                UploadType = uploadType,
                FileName = excelFile.FileName,
                FilePath = $"/uploads/bulkUploads/{uniqueFileName}",
                TotalRecords = 0,
                Status = "Uploaded Successfully",
                UploadedDate = DateTime.UtcNow
            };

            _context.BulkUploadLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"{uploadType}s Excel file uploaded and saved successfully! Users will be updated in next 24 hr." });
        }

        [HttpGet("download-sample/{fileType}")]
        public IActionResult DownloadSample(string fileType)
        {
            
            string fileName = fileType?.ToLowerInvariant() switch
            {
                "teacher" => "SampleTeacherRegistration.xlsx",
                "volunteer" => "SampleVolunteerRegistration.xlsx",
                _ => "SampleStudentRegistration.xlsx" 
            };
            var filePath = Path.Combine(_env.WebRootPath, "images", "bulkUploadSample", fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound(new { success = false, message = "Sample file not found on server." });

            var mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return PhysicalFile(filePath, mimeType, fileName);
        }

        [HttpGet("school-lists")]
        public async Task<IActionResult> GetSchoolLists()
        {
           
            int? currentUserId = HttpContext.Session.GetInt32("UserID");

            if (currentUserId == null || currentUserId == 0)
            {
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                currentUserId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }

            if (currentUserId == null || currentUserId == 0)
            {
                return Unauthorized(new { success = false, message = "User not logged in." });
            }
            //var institute = await _context.view_wj_institutes.FirstOrDefaultAsync(i => i.instituteid == currentUserId);

            var institutes = await _context.view_wj_instituteslist.AsNoTracking().Where(i => i.instituteid == currentUserId).Take(1).ToListAsync();

            var institute = institutes.FirstOrDefault();
            if (institute == null) return BadRequest(new { success = false, message = "Unauthorized school user." });

            var students = await _context.hf_view_studentsdetails.Where(s => s.InstituteID == institute.instituteid && s.IsActive == true).ToListAsync();
            var teachers = await _context.ViewHfTeacherDetails.Where(t => t.InstituteID == institute.instituteid && t.IsActive == true).ToListAsync();
            var logs = await _context.BulkUploadLogs.Where(l => l.InstituteID == institute.instituteid).OrderByDescending(l => l.UploadedDate).ToListAsync();

            var volunteers = new List<VolunteerWithUsernameDto>();
            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = @"
                    SELECT v.*, a.username 
                        FROM public.""Volunteers"" v
                        JOIN public.aspnetusers a ON v.""AspNetUserId"" = a.id
                        WHERE v.""IsDeleted"" = false and v.""InstituteId""=@instituteId";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@instituteId";
                parameter.Value = institute.instituteid;
                command.Parameters.Add(parameter);

                await _context.Database.OpenConnectionAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        volunteers.Add(new VolunteerWithUsernameDto
                        {
                            // Map your properties here as needed
                            Username = reader["username"]?.ToString(),
                            Password_ = reader["password_"]?.ToString(),
                            AspNetUserId = reader["username"]?.ToString(),
                            AffiliateId = Convert.ToInt32(reader["affiliateId"]?.ToString()),
                            InstituteId = Convert.ToInt32(reader["instituteId"]?.ToString()),
                            FirstName = reader["firstName"]?.ToString(),
                            LastName = reader["lastName"]?.ToString(),
                            Email= reader["email"]?.ToString(),
                            MobileNo= reader["mobileNo"]?.ToString(),
                            ID= Convert.ToInt32(reader["Id"]?.ToString()),


                        });

                    }
                }
            }

            return Ok(new { success = true, students, teachers, logs, volunteers });
        }
    }
}