using ClosedXML.Excel;
using HemFoundation.Common;
using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Humanizer.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Configuration;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace HemFoundation.Controllers.Api
{
    [Authorize]
    [Route("api/volunteer")]
    [ApiController]
    public class VolunteerApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IWebHostEnvironment _env;
        private readonly SMSManager _smsManager;

        private readonly IConfiguration _configuration;

        public VolunteerApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IWebHostEnvironment env, IConfiguration configuration, SMSManager smsManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _env = env;
            _configuration = configuration;
            _smsManager = smsManager;
        }

        private async Task<Users?> GetCurrentAffiliate()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return await _context.Users.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
        }

        [HttpGet("assigned-schools")]
        public async Task<IActionResult> GetAssignedSchools()
        {
            var aff = await GetCurrentAffiliate();
            if (aff == null)
                return Unauthorized();

            var referralCode = await _context.WJ_ReferralCodes
                .Where(x => x.UserID == aff.UserID)
                .Select(x => x.ReferralCode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(referralCode))
                return Ok(new { success = true, data = new List<object>() });

            var schools = await _context.SchoolProfileDetails
                .Where(x => x.referralcode == referralCode)
                .Select(x => new
                {
                    instituteID = x.instituteid,
                    Name = x.institute_name
                })
                .OrderBy(x => x.Name)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = schools
            });
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetList()
        {
            var aff = await GetCurrentAffiliate();
            if (aff == null)
                return Unauthorized();

            var list = await (
                from v in _context.Volunteers
                where v.AffiliateId == aff.UserID && !v.IsDeleted

                join s in _context.SchoolProfileDetails
                    on v.InstituteId equals s.instituteid

                join u in _context.Users
                    on v.AspNetUserId equals u.AspNetUserID into userGroup
                from u in userGroup.DefaultIfEmpty()

                join an in _context.Set<ApplicationUser>()
                    on v.AspNetUserId equals an.Id into aspNetGroup
                from an in aspNetGroup.DefaultIfEmpty()

                orderby v.CreatedDate descending

                select new
                {
                    v.Id,
                    Name = v.FirstName + " " + v.LastName,
                    v.MobileNo,
                    v.Email,
                    SchoolName = s.institute_name,
                    v.IsActive,
                    v.CreatedDate,
                    v.InstituteId,
                    UserName = an != null ? an.UserName : (v.MobileNo ?? "N/A"),
                    Password = u != null ? u.Password_ : (v.Password_ ?? "N/A")
                }
            ).ToListAsync();

            return Ok(new
            {
                success = true,
                data = list
            });
        }

        // 8. GET VOLUNTEER STATS FOR CARDS
        [HttpGet("stats")]
        public async Task<IActionResult> GetVolunteerStats()
        {
            var aff = await GetCurrentAffiliate();
            if (aff == null)
                return Unauthorized();

            var referralCode = await _context.WJ_ReferralCodes
                .Where(x => x.UserID == aff.UserID)
                .Select(x => x.ReferralCode)
                .FirstOrDefaultAsync();

            int activeVolunteers = await _context.Volunteers
                .CountAsync(v =>
                    v.AffiliateId == aff.UserID &&
                    v.IsActive &&
                    !v.IsDeleted);

            int schoolMapped = 0;

            if (!string.IsNullOrEmpty(referralCode))
            {
                schoolMapped = await _context.SchoolProfileDetails
                    .CountAsync(x => x.referralcode == referralCode);
            }

            int assignedTasks = 0;
            int reportsCount = 0;

            return Ok(new
            {
                success = true,
                activeVolunteers,
                schoolMapped,
                assignedTasks,
                reports = reportsCount
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterVolunteer([FromBody] VolunteerDto model)
        {
            var aff = await GetCurrentAffiliate();

            if (aff == null)
                return Unauthorized();

            if (model.Id > 0)
            {
                var vol = await _context.Volunteers
                    .FirstOrDefaultAsync(v =>
                        v.Id == model.Id &&
                        v.AffiliateId == aff.UserID);

                if (vol == null)
                {
                    return NotFound(new { success = false, message = "Volunteer not found." });
                }

                vol.FirstName = model.FirstName;
                vol.LastName = model.LastName;

                string updateMobile = model.MobileNo?.Trim() ?? "";
                if (updateMobile.Length > 10)
                    updateMobile = updateMobile.Substring(updateMobile.Length - 10); 
                vol.MobileNo = updateMobile;

                vol.Email = model.Email;
                vol.InstituteId = model.InstituteId;
                vol.ModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Volunteer updated successfully." });
            }

            string baseMobile = model.MobileNo?.Trim() ?? "";
            if (baseMobile.Length > 10)
            {
                baseMobile = baseMobile.Substring(baseMobile.Length - 10);
            }

            string uniqueUsername = baseMobile;
            int counter = 1;

            while (await _context.Set<ApplicationUser>().AnyAsync(x => x.UserName == uniqueUsername))
            {

                string suffix = $"_{counter}";
                int allowedBaseLength = 10 - suffix.Length; 

                if (allowedBaseLength > 0)
                {
                    uniqueUsername = $"{baseMobile.Substring(0, allowedBaseLength)}{suffix}";
                }
                else
                {
                    uniqueUsername = $"{baseMobile}_{counter}"; 
                }
                counter++;
            }

            string password = uniqueUsername;


            var appUser = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = uniqueUsername,
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                PhoneNumber = baseMobile,
                EmailConfirmed = true,
                PhoneNumberConfirmed = false,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var passwordHasher = new PasswordHasher<ApplicationUser>();
            appUser.PasswordHash = passwordHasher.HashPassword(appUser, password);

            _context.Set<ApplicationUser>().Add(appUser);

            var role = await _context.AspNetRoles.FirstOrDefaultAsync(x => x.Name == "HF_Volunteers");
            if (role == null)
            {
                return BadRequest(new { success = false, message = "Role 'Volunteer' not found in aspnetroles." });
            }

            _context.Set<IdentityUserRole<string>>().Add(
                new IdentityUserRole<string>
                {
                    UserId = appUser.Id,
                    RoleId = role.Id
                }
            );

            int nextId = 1;
            if (await _context.Users.AnyAsync())
            {
                int maxId = await _context.Users.MaxAsync(u => u.UserID);
                nextId = maxId + 1;
            }

            var customUser = new Users
            {
                UserID = nextId,
                UserCode = uniqueUsername,
                FirstName = model.FirstName, 
                LastName = model.LastName,
                MobileNo = baseMobile,
                EmailID = model.Email,
                IsActive = true,
                CreatedBy = aff.UserID,
                CreatedDate = DateTime.UtcNow,
                AspNetUserID = appUser.Id,
                RegisterFrom = "Volunteer",
                Password_ = password,
                IsMobileVerified = false,
                IsEmailVerified = false,
                isUserDeactivated = false
            };

            _context.Users.Add(customUser);
            var volunteer = new Volunteer
            {
                Id = nextId,
                FirstName = model.FirstName,
                LastName = model.LastName,
                MobileNo = baseMobile,
                Email = model.Email,
                InstituteId = model.InstituteId,
                AffiliateId = aff.UserID,
                AspNetUserId = appUser.Id,
                Password_ = password,
                IsActive = true,
                IsDeleted = false,
                CreatedDate = DateTime.UtcNow
            };

            _context.Volunteers.Add(volunteer);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Volunteer registered successfully with synchronized IDs.",
                username = uniqueUsername,
                password = password,
                generatedId = nextId
            });
        }
        [HttpGet("download-sample")]
        [AllowAnonymous]
        public IActionResult DownloadSample()
        {
            var folder = Path.Combine(_env.WebRootPath, "uploads", "formats");
            var filePath = Path.Combine(folder, "Volunteer_Sample_Format.xlsx");

            if (!System.IO.File.Exists(filePath))
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("Volunteers");
                ws.Cell(1, 1).Value = "First Name";
                ws.Cell(1, 2).Value = "Last Name";
                ws.Cell(1, 3).Value = "Mobile Number";
                ws.Cell(1, 4).Value = "Email";

                using var memStream = new MemoryStream();
                workbook.SaveAs(memStream);
                return File(memStream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Volunteer_Sample_Format.xlsx");
            }

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Volunteer_Sample_Format.xlsx");
        }

        [HttpPost("validate-bulk")]
        public async Task<IActionResult> ValidateBulkUpload([FromForm] IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length <= 0)
                return BadRequest(new { success = false, message = "Please upload a valid Excel file." });

            var validData = new List<VolunteerExcelDto>();
            var errors = new List<string>();

            using (var stream = new MemoryStream())
            {
                await excelFile.CopyToAsync(stream);

                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheets.FirstOrDefault();
                    if (worksheet == null)
                        return BadRequest(new { success = false, message = "Excel file is empty or invalid." });

                    var lastRow = worksheet.LastRowUsed();
                    if (lastRow == null || lastRow.RowNumber() <= 1)
                        return BadRequest(new { success = false, message = "No data found in the Excel file." });

                    int rowCount = lastRow.RowNumber();

                    for (int row = 2; row <= rowCount; row++)
                    {
                        string fname = worksheet.Cell(row, 1).GetString()?.Trim() ?? "";
                        string lname = worksheet.Cell(row, 2).GetString()?.Trim() ?? "";
                        string mobile = worksheet.Cell(row, 3).GetString()?.Trim() ?? "";
                        string email = worksheet.Cell(row, 4).GetString()?.Trim() ?? "";

                        if (string.IsNullOrEmpty(fname) && string.IsNullOrEmpty(lname) && string.IsNullOrEmpty(mobile)) continue;

                        bool hasRowError = false;

                        if (string.IsNullOrEmpty(fname)) { errors.Add($"Row {row}: First Name is required."); hasRowError = true; }
                        if (string.IsNullOrEmpty(lname)) { errors.Add($"Row {row}: Last Name is required."); hasRowError = true; }

                        if (string.IsNullOrEmpty(mobile))
                        {
                            errors.Add($"Row {row}: Mobile number is required.");
                            hasRowError = true;
                        }
                        else if (!Regex.IsMatch(mobile, @"^[0-9]{10}$"))
                        {
                            errors.Add($"Row {row}: Mobile number must be exactly 10 digits. (Found: {mobile})");
                            hasRowError = true;
                        }

                        if (!string.IsNullOrEmpty(email) && !email.Contains("@"))
                        {
                            errors.Add($"Row {row}: Invalid email format.");
                            hasRowError = true;
                        }

                        if (!hasRowError)
                        {
                            validData.Add(new VolunteerExcelDto { FirstName = fname, LastName = lname, MobileNo = mobile, Email = email });
                        }
                    }
                }
            }

            if (errors.Any()) return Ok(new { success = true, hasErrors = true, errors = errors });

            return Ok(new { success = true, hasErrors = false, validData = validData });
        }

        [HttpPost("save-bulk")]
        public async Task<IActionResult> SaveBulk([FromBody] BulkSaveRequestDto request)
        {
            try
            {
                if (request == null || request.ValidData == null || !request.ValidData.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid data received."
                    });
                }

                var aff = await GetCurrentAffiliate();
                if (aff == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Affiliate not found."
                    });
                }

                var role = await _context.AspNetRoles
                    .FirstOrDefaultAsync(x => x.Name == "HF_Volunteers");

                if (role == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Role 'Volunteer' not found in aspnetroles."
                    });
                }

                int nextId = 1;
                if (await _context.Users.AnyAsync())
                {
                    int maxId = await _context.Users.MaxAsync(u => u.UserID);
                    nextId = maxId + 1;
                }

                int addedCount = 0;
                int failedCount = 0;
                var errors = new List<object>();

                foreach (var item in request.ValidData)
                {
                    try
                    {
                        string firstName = item.FirstName?.Trim() ?? "";
                        string lastName = item.LastName?.Trim() ?? "";
                        string baseMobile = item.MobileNo?.Trim() ?? "";

                        if (baseMobile.Length > 10)
                        {
                            baseMobile = baseMobile.Substring(baseMobile.Length - 10);
                        }

                        string? cleanEmail = string.IsNullOrWhiteSpace(item.Email) ? null : item.Email.Trim();

                        if (string.IsNullOrWhiteSpace(firstName) ||
                            string.IsNullOrWhiteSpace(lastName) ||
                            string.IsNullOrWhiteSpace(baseMobile))
                        {
                            failedCount++;
                            errors.Add(new
                            {
                                mobile = baseMobile,
                                message = "First Name, Last Name and Mobile Number are required."
                            });
                            continue;
                        }

                        // Unique username generation logic (jaise single register/universal mein hai)
                        string uniqueUsername = baseMobile;
                        int counter = 1;
                        while (await _context.Set<ApplicationUser>().AnyAsync(x => x.UserName == uniqueUsername))
                        {
                            string suffix = $"_{counter}";
                            int allowedBaseLength = 10 - suffix.Length;
                            if (allowedBaseLength > 0 && baseMobile.Length >= allowedBaseLength)
                            {
                                uniqueUsername = $"{baseMobile.Substring(0, allowedBaseLength)}{suffix}";
                            }
                            else
                            {
                                uniqueUsername = $"{baseMobile}_{counter}";
                            }
                            counter++;
                        }

                        string password = uniqueUsername;

                        // 1. Create AspNetUser (Identity)
                        var appUser = new ApplicationUser
                        {
                            Id = Guid.NewGuid().ToString(),
                            UserName = uniqueUsername,
                            Email = cleanEmail,
                            PhoneNumber = baseMobile,
                            EmailConfirmed = true,
                            PhoneNumberConfirmed = false,
                            SecurityStamp = Guid.NewGuid().ToString()
                        };

                        var passwordHasher = new PasswordHasher<ApplicationUser>();
                        appUser.PasswordHash = passwordHasher.HashPassword(appUser, password);

                        _context.Set<ApplicationUser>().Add(appUser);

                        // 2. Assign Role in Identity
                        _context.Set<IdentityUserRole<string>>().Add(
                            new IdentityUserRole<string>
                            {
                                UserId = appUser.Id,
                                RoleId = role.Id
                            }
                        );

                        var customUser = new Users
                        {
                            UserID = nextId,
                            UserCode = uniqueUsername,
                            FirstName = firstName,
                            LastName = lastName,
                            MobileNo = baseMobile,
                            EmailID = cleanEmail,
                            IsActive = true,
                            CreatedBy = aff.UserID,
                            CreatedDate = DateTime.UtcNow,
                            AspNetUserID = appUser.Id,
                            RegisterFrom = "Volunteer",
                            Password_ = password,
                            IsMobileVerified = false,
                            IsEmailVerified = false,
                            isUserDeactivated = false
                        };

                        _context.Users.Add(customUser);

                        var volunteer = new Volunteer
                        {
                            Id = nextId,
                            FirstName = firstName,
                            LastName = lastName,
                            MobileNo = baseMobile,
                            Email = cleanEmail,
                            InstituteId = request.InstituteId,
                            AffiliateId = aff.UserID,
                            AspNetUserId = appUser.Id,
                            Password_ = password,
                            IsActive = true,
                            IsDeleted = false,
                            CreatedDate = DateTime.UtcNow
                        };

                        _context.Volunteers.Add(volunteer);

                        await _context.SaveChangesAsync();

                        try
                        {

                            if (!string.IsNullOrEmpty(baseMobile))
                            {
                                _smsManager.StudentRegistration(baseMobile, volunteer.FirstName, uniqueUsername, baseMobile);
                               
                            }

                            if (!string.IsNullOrEmpty(volunteer.Email))
                            {
                                var studentRegObj = new StudentRegistration
                                {
                                    FirstName = volunteer.FirstName,
                                    LastName = volunteer.LastName,
                                    EmailID = volunteer.Email,
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

                        nextId++;
                        addedCount++;
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        errors.Add(new
                        {
                            mobile = item.MobileNo,
                            message = ex.InnerException?.Message ?? ex.Message
                        });
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = $"{addedCount} Volunteers registered successfully with SMS & login credentials pattern.",
                    addedCount,
                    failedCount,
                    errors
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Error while saving bulk volunteers.",
                    error = ex.InnerException?.Message ?? ex.Message
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var vol = await _context.Volunteers.FindAsync(id);
            if (vol != null)
            {
                vol.IsDeleted = true;
                vol.ModifiedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return Ok(new { success = true, message = "Deleted successfully." });
        }


        [HttpPost("admin/upload-format")]
        public async Task<IActionResult> AdminUploadFormat([FromForm] IFormFile formatFile)
        {
            if (formatFile == null || formatFile.Length == 0)
                return BadRequest(new { success = false, message = "Please select a valid excel file." });

            var folder = Path.Combine(_env.WebRootPath, "uploads", "formats");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var filePath = Path.Combine(folder, "Volunteer_Sample_Format.xlsx");
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                await formatFile.CopyToAsync(fs);
            }

            return Ok(new { success = true, message = "Format file updated successfully. Affiliates can now download it." });
        }

        [HttpGet("admin/filters-data")]
        public async Task<IActionResult> GetAdminFiltersData()
        {
            var affiliates = await _context.View_HF_Affiliate
                .AsNoTracking()
                .Select(x => new
                {
                    id = x.InstituteID,
                    name = ((x.FirstName ?? "") + " " + (x.LastName ?? "")).Trim(),
                    referralCode = x.ReferralCode
                })
                .Distinct()
                .OrderBy(x => x.name)
                .ToListAsync();

            var schools = await _context.WJ_Institutes
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => new
                {
                    id = x.InstituteID,
                    name = x.Name,
                    referralCode = x.ReferralCode
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                affiliates = affiliates,
                schools = schools
            });
        }

        [HttpGet("admin/list")]
        public async Task<IActionResult> GetAdminVolunteersList(int page = 1, int pageSize = 10, long? affiliateId = null, int? instituteId = null, string search = "")
        {
            var query = _context.Volunteers.Where(v => !v.IsDeleted).AsQueryable();

            if (affiliateId.HasValue && affiliateId > 0)
                query = query.Where(v => v.AffiliateId == affiliateId);

            if (instituteId.HasValue && instituteId > 0)
                query = query.Where(v => v.InstituteId == instituteId);

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(v => v.FirstName.ToLower().Contains(search) ||
                                         v.LastName.ToLower().Contains(search) ||
                                         v.MobileNo.Contains(search) ||
                                         (!string.IsNullOrEmpty(v.Email) && v.Email.ToLower().Contains(search)));
            }

            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            var list = await query
                .OrderByDescending(v => v.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new
                {
                    v.Id,
                    Name = v.FirstName + " " + v.LastName,
                    v.MobileNo,
                    v.Email,
                    SchoolName = _context.WJ_Institutes.Where(i => i.InstituteID == v.InstituteId).Select(i => i.Name).FirstOrDefault(),
                    AffiliateName = _context.Users.Where(a => a.UserID == v.AffiliateId).Select(a => a.FirstName + " " + a.LastName).FirstOrDefault(),
                    v.IsActive,
                    v.CreatedDate
                }).ToListAsync();

            return Ok(new { success = true, data = list, totalRecords, totalPages, currentPage = page });
        }


        [HttpGet("mapping-schools/{volunteerId}")]
        public async Task<IActionResult> GetVolunteerSchoolMapping(int volunteerId)
        {
            var aff = await GetCurrentAffiliate();
            if (aff == null)
                return Unauthorized(new { success = false, message = "Affiliate not found." });

            var volunteer = await _context.Volunteers
                .FirstOrDefaultAsync(v => v.Id == volunteerId && v.AffiliateId == aff.UserID && v.IsActive && !v.IsDeleted);

            if (volunteer == null)
            {
                return NotFound(new { success = false, message = "Volunteer not found." });
            }

            var referralCode = await _context.WJ_ReferralCodes
                .Where(x => x.UserID == aff.UserID)
                .Select(x => x.ReferralCode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(referralCode))
            {
                return Ok(new
                {
                    success = true,
                    volunteerId = volunteer.Id,
                    assignedSchools = new List<object>(),
                    unassignedSchools = new List<object>()
                });
            }

            // Include pincode in school projection
            var allSchools = await _context.SchoolProfileDetails
                .Where(x => x.referralcode == referralCode)
                .Select(x => new
                {
                    instituteId = x.instituteid,
                    name = x.institute_name,
                    pincode = x.pincode ?? ""
                })
                .OrderBy(x => x.name)
                .ToListAsync();

            // Fetch active mappings of all volunteers under this affiliate to show who else is mapped
            var allActiveMappings = await (
                from m in _context.VolunteerInstituteMappings
                join v in _context.Volunteers on m.VolunteerId equals v.Id
                where !m.IsDeleted && m.IsActive && v.AffiliateId == aff.UserID
                select new { m.InstituteId, VolunteerName = v.FirstName + " " + v.LastName }
            ).ToListAsync();

            var schoolVolunteersDict = allActiveMappings
                .GroupBy(x => x.InstituteId)
                .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.VolunteerName)));

            var mappedSchoolIds = await _context.VolunteerInstituteMappings
                .Where(m => m.VolunteerId == volunteerId && !m.IsDeleted && m.IsActive)
                .Select(m => m.InstituteId)
                .Distinct()
                .ToListAsync();

            var assignedSchools = allSchools
                .Where(s => mappedSchoolIds.Contains(s.instituteId))
                .Select(s => new {
                    s.instituteId,
                    s.name,
                    s.pincode,
                    assignedVolunteers = schoolVolunteersDict.ContainsKey(s.instituteId) ? schoolVolunteersDict[s.instituteId] : "None"
                })
                .ToList();

            var unassignedSchools = allSchools
                .Where(s => !mappedSchoolIds.Contains(s.instituteId))
                .Select(s => new {
                    s.instituteId,
                    s.name,
                    s.pincode,
                    assignedVolunteers = schoolVolunteersDict.ContainsKey(s.instituteId) ? schoolVolunteersDict[s.instituteId] : "None"
                })
                .ToList();

            return Ok(new
            {
                success = true,
                volunteerId = volunteer.Id,
                assignedSchools,
                unassignedSchools
            });
        }

        [HttpPost("save-mapping")]
        public async Task<IActionResult> SaveVolunteerMapping([FromBody] VolunteerMappingDto model)
        {
            try
            {
                var aff = await GetCurrentAffiliate();
                if (aff == null)
                    return Unauthorized(new { success = false, message = "Affiliate not found." });

                if (model == null || model.VolunteerId <= 0)
                    return BadRequest(new { success = false, message = "Invalid mapping data received." });

                var instituteIds = model.InstituteIds?
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList()
                    ?? new List<int>();

                var volunteer = await _context.Volunteers
                    .FirstOrDefaultAsync(v => v.Id == model.VolunteerId && v.AffiliateId == aff.UserID && v.IsActive && !v.IsDeleted);

                if (volunteer == null)
                    return BadRequest(new { success = false, message = "Volunteer not found." });

                var referralCode = await _context.WJ_ReferralCodes
                    .Where(x => x.UserID == aff.UserID)
                    .Select(x => x.ReferralCode)
                    .FirstOrDefaultAsync();

                var affiliateSchoolIds = await _context.SchoolProfileDetails
                    .Where(x => x.referralcode == referralCode)
                    .Select(x => x.instituteid)
                    .Distinct()
                    .ToListAsync();

                var invalidInstituteIds = instituteIds.Except(affiliateSchoolIds).ToList();
                if (invalidInstituteIds.Any())
                    return BadRequest(new { success = false, message = "One or more selected schools do not belong to this affiliate." });

                string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? aff.UserID.ToString();

                // Existing mappings for this specific volunteer
                var existingMappings = await _context.VolunteerInstituteMappings
                    .Where(m => m.VolunteerId == model.VolunteerId)
                    .ToListAsync();

                // Soft delete old mappings that are no longer selected
                foreach (var mapping in existingMappings)
                {
                    if (!instituteIds.Contains(mapping.InstituteId))
                    {
                        mapping.IsDeleted = true;
                        mapping.IsActive = false;
                        mapping.ModifiedDate = DateTime.UtcNow;
                        mapping.LastUpdatedBy = currentUserId;
                    }
                }

                // Add or Reactivate selected mappings (Many-to-Many allowed, so no check for other volunteers)
                foreach (var instituteId in instituteIds)
                {
                    var existingMapping = existingMappings.FirstOrDefault(x => x.InstituteId == instituteId);

                    if (existingMapping != null)
                    {
                        existingMapping.IsDeleted = false;
                        existingMapping.IsActive = true;
                        existingMapping.ModifiedDate = DateTime.UtcNow;
                        existingMapping.LastUpdatedBy = currentUserId;
                    }
                    else
                    {
                        _context.VolunteerInstituteMappings.Add(
                            new VolunteerInstituteMapping
                            {
                                VolunteerId = model.VolunteerId,
                                InstituteId = instituteId,
                                IsActive = true,
                                IsDeleted = false,
                                CreatedDate = DateTime.UtcNow,
                                ModifiedDate = DateTime.UtcNow,
                                LastUpdatedBy = currentUserId
                            });
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "School mappings saved successfully.",
                    volunteerId = model.VolunteerId,
                    mappedSchools = instituteIds.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Error saving mappings.",
                    error = ex.InnerException?.Message ?? ex.Message
                });
            }
        }

        [HttpGet("my-assigned-schools")]
        public async Task<IActionResult> GetMyAssignedSchools()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { success = false, message = "User not authenticated." });

                var volunteer = await _context.Volunteers
                    .FirstOrDefaultAsync(v => v.AspNetUserId == userId && v.IsActive && !v.IsDeleted);

                if (volunteer == null)
                {
                    return Ok(new { success = true, data = new List<object>() });
                }

                var mappings = await _context.VolunteerInstituteMappings
                    .Where(m => m.VolunteerId == volunteer.Id && m.IsActive && !m.IsDeleted)
                    .Select(m => new { m.InstituteId, m.CreatedDate, m.LastUpdatedBy })
                    .ToListAsync();

                if (!mappings.Any())
                {
                    return Ok(new { success = true, data = new List<object>() });
                }

                // Referral Code check (AffiliateId agar long hai)
                string? referralCode = null;
                if (volunteer.AffiliateId > 0) // <-- Yahan .HasValue hatakar direct > 0 check kiya hai
                {
                    referralCode = await _context.WJ_ReferralCodes
                        .Where(x => x.UserID == volunteer.AffiliateId) // <-- .Value bhi hata diya hai
                        .Select(x => x.ReferralCode)
                        .FirstOrDefaultAsync();
                }

                var instituteIds = mappings.Select(x => x.InstituteId).Distinct().ToList();

                var schoolsQuery = _context.SchoolProfileDetails
                    .AsNoTracking()
                    .Where(x => instituteIds.Contains(x.instituteid));

                if (!string.IsNullOrEmpty(referralCode))
                {
                    schoolsQuery = schoolsQuery.Where(x => x.referralcode == referralCode);
                }

                var schools = await schoolsQuery
                    .Select(x => new { InstituteID = x.instituteid, Name = x.institute_name })
                    .ToListAsync();

                var assignedByIds = mappings
                    .Where(x => !string.IsNullOrWhiteSpace(x.LastUpdatedBy))
                    .Select(x => x.LastUpdatedBy!)
                    .Distinct()
                    .ToList();

                var users = new Dictionary<string, string>();
                if (assignedByIds.Any())
                {
                    users = await _context.Users
                        .Where(u => assignedByIds.Contains(u.AspNetUserID))
                        .Select(u => new { u.AspNetUserID, u.FirstName, u.MobileNo })
                        .ToDictionaryAsync(
                            x => x.AspNetUserID,
                            x => !string.IsNullOrWhiteSpace(x.FirstName) ? x.FirstName : x.MobileNo
                        );
                }

                var result = mappings
                    .Join(schools, m => m.InstituteId, s => s.InstituteID, (m, s) => new
                    {
                        s.InstituteID,
                        s.Name,
                        AssignedDate = m.CreatedDate,
                        AssignedBy = !string.IsNullOrWhiteSpace(m.LastUpdatedBy) && users.ContainsKey(m.LastUpdatedBy)
                            ? users[m.LastUpdatedBy]
                            : (m.LastUpdatedBy ?? "N/A")
                    })
                    .OrderBy(x => x.Name)
                    .ToList();

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Error while loading assigned schools.",
                    error = ex.InnerException?.Message ?? ex.Message
                });
            }
        }

        
        [HttpGet("dashboard-summary-counts")]
        public async Task<IActionResult> GetDashboardSummaryCounts([FromQuery] string? affiliateUserId)
        {
            try
            {
                Int32 reportaffiliateid = Convert.ToInt32(affiliateUserId);

                if (reportaffiliateid == 0)
                {
                    int loggedInUserID = HttpContext.Session.GetInt32("UserID") ?? 0;
                    reportaffiliateid = loggedInUserID;
                }

                string connString = _configuration.GetConnectionString("DefaultConnection");

                int totalinstitute = 0;
                int totalStudents = 0;
                int activeStudents = 0;
                int totalTeachers = 0;
                int activeTeachers = 0;
                int totalSelProjects = 0;
                int familyProjects = 0;
                int individualProjects = 0;
                int groupPbl = 0;

                using (var conn = new Npgsql.NpgsqlConnection(connString))
                {
                    await conn.OpenAsync();

                    string summarySql = "SELECT schoolcount, totalstudents, activestudents, totalteachers, activeteachers, totalselprojects, familyprojects, individualprojects, grouppbl FROM public.get_volunteer_dashboard_summary(@AffiliateId)";

                    using (var cmd = new Npgsql.NpgsqlCommand(summarySql, conn))
                    {
                        cmd.Parameters.AddWithValue("AffiliateId", reportaffiliateid);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            // Read the single returned row from the stored procedure
                            if (await reader.ReadAsync())
                            {
                                totalinstitute = reader.GetInt32(0);
                                totalStudents = reader.GetInt32(1);
                                activeStudents = reader.GetInt32(2);
                                totalTeachers = reader.GetInt32(3);
                                activeTeachers = reader.GetInt32(4);
                                totalSelProjects = reader.GetInt32(5);
                                familyProjects = reader.GetInt32(6);
                                individualProjects = reader.GetInt32(7);
                                groupPbl = reader.GetInt32(8);
                            }
                            else
                            {
                                // If no row returned (e.g. invalid affiliate ID), return zeroes
                                return Ok(new { success = true, data = new { totalinstitute = 0, totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
                            }
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalinstitute,
                        totalStudents,
                        activeStudents,
                        totalTeachers,
                        activeTeachers,
                        totalSelProjects,
                        familyProjects,
                        individualProjects,
                        groupPbl
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error loading dashboard summary counts.",
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        //[HttpGet("dashboard-summary-counts")]
        //public async Task<IActionResult> GetDashboardSummaryCounts()
        //{
        //    try
        //    {
        //        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //        if (string.IsNullOrEmpty(userId))
        //        {
        //            return Unauthorized(new { success = false, message = "User not authenticated." });
        //        }

        //        string connString = _configuration.GetConnectionString("DefaultConnection");

        //        int totalStudents = 0;
        //        int activeStudents = 0;
        //        int totalTeachers = 0;
        //        int activeTeachers = 0;
        //        int totalSelProjects = 0;
        //        int familyProjects = 0;
        //        int individualProjects = 0;
        //        int groupPbl = 0;

        //        using (var conn = new Npgsql.NpgsqlConnection(connString))
        //        {
        //            await conn.OpenAsync();

        //            string summarySql = @"
        //        -- 1. Find the active, non-deleted volunteer mapped to this AspNetUserId
        //        SELECT id FROM volunteers 
        //        WHERE aspnetuserid = @AspNetUserId AND isactive = TRUE AND isdeleted = FALSE 
        //        LIMIT 1;

        //        -- 2. Extract mapped institute IDs and aggregate metrics in a single pass
        //        WITH TargetInstitutes AS (
        //            SELECT DISTINCT instituteid 
        //            FROM volunteerinstitutemappings 
        //            WHERE volunteerid = (SELECT id FROM volunteers WHERE aspnetuserid = @AspNetUserId AND isactive = TRUE AND isdeleted = FALSE LIMIT 1)
        //              AND isactive = TRUE AND isdeleted = FALSE
        //              AND instituteid IS NOT NULL
        //        ),
        //        AggregatedCounts AS (
        //            SELECT 
        //                (SELECT COUNT(DISTINCT studentid) FROM wj_studentassociates WHERE instituteid IN (SELECT instituteid FROM TargetInstitutes)) AS TotalStudents,
        //                (SELECT COUNT(DISTINCT studentid) FROM hf_view_studentactivitylog WHERE instituteid IN (SELECT instituteid FROM TargetInstitutes) AND activity_type != 'Survey') AS ActiveStudents,
        //                (SELECT COUNT(DISTINCT teacherid) FROM wj_teacherassociates WHERE instituteid IN (SELECT instituteid FROM TargetInstitutes)) AS TotalTeachers,
        //                (SELECT COUNT(DISTINCT teacherid) FROM hf_view_teacheractivitylog WHERE instituteid IN (SELECT instituteid FROM TargetInstitutes)) AS ActiveTeachers,
        //                (SELECT COUNT(DISTINCT instituteid) FROM creativityactivitessubmission2021 WHERE instituteid IN (SELECT instituteid FROM TargetInstitutes)) AS TotalSelProjects,
        //                (SELECT COUNT(*) FROM hf_view_studentactivitylog WHERE instituteid IN (SELECT instituteid FROM TargetInstitutes) AND activity_type = 'Family PBL') AS FamilyProjects,
        //                (SELECT COUNT(*) FROM hf_view_studentactivitylog WHERE instituteid IN (SELECT instituteid FROM TargetInstitutes) AND activity_type = 'Individual PBL') AS IndividualProjects,
        //                (SELECT COUNT(*) FROM creativityactivitessubmission2021 WHERE instituteid IN (SELECT instituteid FROM TargetInstitutes)) AS GroupPbl
        //        )
        //        SELECT TotalStudents, ActiveStudents, TotalTeachers, ActiveTeachers, TotalSelProjects, FamilyProjects, IndividualProjects, GroupPbl 
        //        FROM AggregatedCounts;
        //    ";

        //            using (var cmd = new Npgsql.NpgsqlCommand(summarySql, conn))
        //            {
        //                cmd.Parameters.AddWithValue("AspNetUserId", userId);

        //                using (var reader = await cmd.ExecuteReaderAsync())
        //                {
        //                    // First result set: Check if volunteer exists
        //                    bool volunteerExists = await reader.ReadAsync();
        //                    if (!volunteerExists)
        //                    {
        //                        return Ok(new { success = true, data = new { totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
        //                    }

        //                    // Move to the second result set (AggregatedCounts)
        //                    if (await reader.NextResultAsync() && await reader.ReadAsync())
        //                    {
        //                        totalStudents = reader.GetInt32(0);
        //                        activeStudents = reader.GetInt32(1);
        //                        totalTeachers = reader.GetInt32(2);
        //                        activeTeachers = reader.GetInt32(3);
        //                        totalSelProjects = reader.GetInt32(4);
        //                        familyProjects = reader.GetInt32(5);
        //                        individualProjects = reader.GetInt32(6);
        //                        groupPbl = reader.GetInt32(7);
        //                    }
        //                }
        //            }
        //        }

        //        return Ok(new
        //        {
        //            success = true,
        //            data = new
        //            {
        //                totalStudents,
        //                activeStudents,
        //                totalTeachers,
        //                activeTeachers,
        //                totalSelProjects,
        //                familyProjects,
        //                individualProjects,
        //                groupPbl
        //            }
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new
        //        {
        //            success = false,
        //            message = "Error loading volunteer dashboard summary counts.",
        //            error = ex.Message,
        //            innerException = ex.InnerException?.Message
        //        });
        //    }
        //}


        [HttpGet("dashboard-summary-counts1")]
        public async Task<IActionResult> GetDashboardSummaryCounts1()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var volunteer = await _context.Volunteers.FirstOrDefaultAsync(v => v.AspNetUserId == userId && v.IsActive && !v.IsDeleted);

            if (volunteer == null)
            {
                return Ok(new { success = true, data = new { totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
            }

            var instituteIds = await _context.VolunteerInstituteMappings
                .Where(m => m.VolunteerId == volunteer.Id && m.IsActive && !m.IsDeleted)
                .Select(m => (int?)m.InstituteId)
                .Distinct()
                .ToListAsync();

            if (!instituteIds.Any())
            {
                return Ok(new { success = true, data = new { totalStudents = 0, activeStudents = 0, totalTeachers = 0, activeTeachers = 0, totalSelProjects = 0, familyProjects = 0, individualProjects = 0, groupPbl = 0 } });
            }

            var totalStudents = await _context.Set<ViewWjStudentsDetailsInfo>()
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

            var activeStudents = await _context.StudentActivityLogs
                .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType != "Survey") 
                .Select(x => x.StudentID)
                .Distinct()
                .CountAsync();

            var totalTeachers = await _context.Set<ViewHfTeacherDetails>()
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

            var activeTeachers = await _context.TeacherActivityLogs
                .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                .Select(x => x.TeacherID)
                .Distinct()
                .CountAsync();

            var totalSelProjects = await _context.creativityActivitiesSubmission2021s
                .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                .Select(x => x.InstituteID)
                .Distinct()
                .CountAsync();

            var familyProjects = await _context.StudentActivityLogs
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Family PBL");

            var individualProjects = await _context.StudentActivityLogs
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Individual PBL");

            var groupPbl = await _context.creativityActivitiesSubmission2021s
                .CountAsync(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID));

            return Ok(new
            {
                success = true,
                data = new
                {
                    totalStudents,
                    activeStudents,
                    totalTeachers,
                    activeTeachers,
                    totalSelProjects,
                    familyProjects,
                    individualProjects,
                    groupPbl
                }
            });
        }

        [HttpGet("details-list/{type}")]
        public async Task<IActionResult> GetDetailsList(string type)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var volunteer = await _context.Volunteers.FirstOrDefaultAsync(v => v.AspNetUserId == userId && v.IsActive && !v.IsDeleted);

            if (volunteer == null)
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            var instituteIds = await _context.VolunteerInstituteMappings
                .Where(m => m.VolunteerId == volunteer.Id && m.IsActive && !m.IsDeleted)
                .Select(m => (int?)m.InstituteId)
                .Distinct()
                .ToListAsync();

            if (!instituteIds.Any())
            {
                return Ok(new { success = true, data = new List<object>() });
            }

            object result = type switch
            {
                "total-students" => await _context.Set<ViewWjStudentsDetailsInfo>()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                "active-students" => await _context.StudentActivityLogs
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType != "Survey") 
                    .Select(x => new { x.StudentID, x.InstituteID })
                    .Distinct()
                    .ToListAsync(),

                "total-teachers" => await _context.Set<ViewHfTeacherDetails>()
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                "active-teachers" => await _context.TeacherActivityLogs
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .Select(x => new { x.TeacherID, x.InstituteID })
                    .Distinct()
                    .ToListAsync(),

                "sel-projects" => await _context.creativityActivitiesSubmission2021s
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                "family-projects" => await _context.StudentActivityLogs
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Family PBL")
                    .ToListAsync(),

                "individual-projects" => await _context.StudentActivityLogs
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID) && x.ActivityType == "Individual PBL")
                    .ToListAsync(),

                "group-pbl" => await _context.creativityActivitiesSubmission2021s
                    .Where(x => x.InstituteID != null && instituteIds.Contains(x.InstituteID))
                    .ToListAsync(),

                _ => new List<object>()
            };

            return Ok(new { success = true, data = result });
        }

    }

    public class VolunteerMappingDto
    {
        public int VolunteerId { get; set; }
        public List<int> InstituteIds { get; set; } = new List<int>();
    }
    public class VolunteerDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string? Email { get; set; }
        public int InstituteId { get; set; }
    }

    public class VolunteerExcelDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string? Email { get; set; }
    }

    public class BulkSaveRequestDto
    {
        public int InstituteId { get; set; }
        public List<VolunteerExcelDto> ValidData { get; set; } = new List<VolunteerExcelDto>();
    }
}