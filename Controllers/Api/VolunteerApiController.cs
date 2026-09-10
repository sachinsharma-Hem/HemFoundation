using HemFoundation.Data;
using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using System.Collections;

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

        public VolunteerApiController(
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

        private async Task<Users?> GetCurrentAffiliate()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return await _context.Users.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
        }

        // 1. GET ASSIGNED SCHOOLS (For Dropdown)
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

        // 2. GET VOLUNTEERS LIST
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
                    v.InstituteId
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

        // 3. SINGLE REGISTRATION & UPDATE
        [HttpPost("register")]
        public async Task<IActionResult> RegisterVolunteer([FromBody] VolunteerDto model)
        {
            var aff = await GetCurrentAffiliate();

            if (aff == null)
                return Unauthorized();

            // =====================================================
            // UPDATE VOLUNTEER
            // =====================================================
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

                // Ensure MobileNo is max 10 characters
                string updateMobile = model.MobileNo?.Trim() ?? "";
                if (updateMobile.Length > 10)
                    updateMobile = updateMobile.Substring(updateMobile.Length - 10); // Takes last 10 digits
                vol.MobileNo = updateMobile;

                vol.Email = model.Email;
                vol.InstituteId = model.InstituteId;
                vol.ModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Volunteer updated successfully." });
            }

            // =====================================================
            // NEW REGISTRATION
            // =====================================================

            // Ensure baseMobile is strictly 10 characters (removes country codes like +91)
            string baseMobile = model.MobileNo?.Trim() ?? "";
            if (baseMobile.Length > 10)
            {
                baseMobile = baseMobile.Substring(baseMobile.Length - 10);
            }

            string uniqueUsername = baseMobile;
            int counter = 1;

            // Checking uniqueness
            while (await _context.Set<ApplicationUser>().AnyAsync(x => x.UserName == uniqueUsername))
            {
                // NOTE: Agar username limit 10 hai, toh suffix add karne ke liye aage ke digits hatane padenge
                // Example: "98765432" + "_1" = 10 chars
                string suffix = $"_{counter}";
                int allowedBaseLength = 10 - suffix.Length; // 10 is max DB length

                if (allowedBaseLength > 0)
                {
                    uniqueUsername = $"{baseMobile.Substring(0, allowedBaseLength)}{suffix}";
                }
                else
                {
                    uniqueUsername = $"{baseMobile}_{counter}"; // Agar DB column bada hai toh chal jayega
                }
                counter++;
            }

            // Password_ DB mein bada hona chahiye. Agar 10 ki limit hai to uniqueUsername 10 chars ka hi hona chahiye.
            string password = uniqueUsername;

            // =====================================================
            // ASPNETUSERS
            // =====================================================

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

            // =====================================================
            // ROLE = Volunteer
            // =====================================================

            var role = await _context.AspNetRoles.FirstOrDefaultAsync(x => x.Name == "Volunteer");
            if (role == null)
            {
                return BadRequest(new { success = false, message = "Role 'Volunteer' not found in aspnetroles." });
            }

            // =====================================================
            // ASPNETUSERROLES
            // =====================================================

            _context.Set<IdentityUserRole<string>>().Add(
                new IdentityUserRole<string>
                {
                    UserId = appUser.Id,
                    RoleId = role.Id
                }
            );

            // =====================================================
            // GENERATE SHARED ID
            // =====================================================
            int nextId = 1;
            if (await _context.Users.AnyAsync())
            {
                int maxId = await _context.Users.MaxAsync(u => u.UserID);
                nextId = maxId + 1;
            }

            // =====================================================
            // USERS TABLE
            // =====================================================

            var customUser = new Users
            {
                UserID = nextId,
                UserCode = uniqueUsername,
                FirstName = model.FirstName, // Check DB limits for names too
                LastName = model.LastName,
                MobileNo = baseMobile,
                EmailID = model.Email,
                IsActive = true,
                CreatedBy = aff.UserID,
                CreatedDate = DateTime.UtcNow,
                AspNetUserID = appUser.Id,
                RegisterFrom = "Volunteer", // 9 chars
                Password_ = password,
                IsMobileVerified = false,
                IsEmailVerified = false,
                isUserDeactivated = false
            };

            _context.Users.Add(customUser);

            // =====================================================
            // VOLUNTEERS TABLE
            // =====================================================

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

            // =====================================================
            // SAVE ALL
            // =====================================================

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
        // 4. DOWNLOAD SAMPLE EXCEL
        [HttpGet("download-sample")]
        [AllowAnonymous]
        public IActionResult DownloadSample()
        {
            var folder = Path.Combine(_env.WebRootPath, "uploads", "formats");
            var filePath = Path.Combine(folder, "Volunteer_Sample_Format.xlsx");

            if (!System.IO.File.Exists(filePath))
            {
                // Fallback direct generation if file doesn't exist
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

        // 5. VALIDATE EXCEL DATA (BULK UPLOAD STEP 1)
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

        // 6. SAVE BULK DATA (BULK UPLOAD STEP 2)
        // =========================================================
        // 6. SAVE BULK DATA
        // Same registration process as manual RegisterVolunteer()
        // =========================================================
        [HttpPost("save-bulk")]
        public async Task<IActionResult> SaveBulk([FromBody] BulkSaveRequestDto request)
        {
            try
            {
                // ---------------------------------------------------------
                // 1. BASIC VALIDATION
                // ---------------------------------------------------------
                if (request == null ||
                    request.ValidData == null ||
                    !request.ValidData.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid data received."
                    });
                }

                // ---------------------------------------------------------
                // 2. CURRENT AFFILIATE
                // ---------------------------------------------------------
                var aff = await GetCurrentAffiliate();

                if (aff == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Affiliate not found."
                    });
                }

                // ---------------------------------------------------------
                // 3. GET Volunteer ROLE
                // Same role used in manual registration
                // ---------------------------------------------------------
                var role = await _context.AspNetRoles
                    .FirstOrDefaultAsync(x => x.Name == "Volunteer");

                if (role == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Role 'Volunteer' not found in aspnetroles."
                    });
                }

                // ---------------------------------------------------------
                // 4. GET NEXT SHARED ID
                //
                // Users.UserID == Volunteers.Id
                //
                // Example:
                // Current max = 105
                // First Excel volunteer = 106
                // Second Excel volunteer = 107
                // ---------------------------------------------------------
                int nextId = 1;

                var maxUserId = await _context.Users
                    .Select(x => (int?)x.UserID)
                    .MaxAsync();

                if (maxUserId.HasValue)
                {
                    nextId = maxUserId.Value + 1;
                }

                // ---------------------------------------------------------
                // 5. COUNTERS
                // ---------------------------------------------------------
                int addedCount = 0;
                int failedCount = 0;

                var errors = new List<object>();

                // ---------------------------------------------------------
                // 6. PROCESS EACH EXCEL VOLUNTEER
                // ---------------------------------------------------------
                foreach (var item in request.ValidData)
                {
                    try
                    {
                        string firstName = item.FirstName?.Trim() ?? "";
                        string lastName = item.LastName?.Trim() ?? "";
                        string baseMobile = item.MobileNo?.Trim() ?? "";

                        string? cleanEmail =
                            string.IsNullOrWhiteSpace(item.Email)
                                ? null
                                : item.Email.Trim();

                        // -------------------------------------------------
                        // BASIC VALIDATION
                        // -------------------------------------------------
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

                        // -------------------------------------------------
                        // 7. UNIQUE USERNAME
                        // Same logic as manual registration
                        //
                        // 9876543210
                        // 9876543210_1
                        // 9876543210_2
                        // -------------------------------------------------
                        string uniqueUsername = baseMobile;
                        int counter = 1;

                        while (await _context.Set<ApplicationUser>()
                            .AnyAsync(x => x.UserName == uniqueUsername))
                        {
                            uniqueUsername = $"{baseMobile}_{counter}";
                            counter++;
                        }

                        // Password = Username
                        string password = uniqueUsername;

                        // -------------------------------------------------
                        // 8. ASPNETUSERS
                        // Same as manual registration
                        // -------------------------------------------------
                        var appUser = new ApplicationUser
                        {
                            Id = Guid.NewGuid().ToString(),

                            UserName = uniqueUsername,

                            Email = string.IsNullOrWhiteSpace(cleanEmail)
                                ? null
                                : cleanEmail,

                            PhoneNumber = baseMobile,

                            EmailConfirmed = true,

                            PhoneNumberConfirmed = false,

                            SecurityStamp = Guid.NewGuid().ToString()
                        };

                        // -------------------------------------------------
                        // 9. PASSWORD HASH
                        // Same PasswordHasher as manual registration
                        // -------------------------------------------------
                        var passwordHasher =
                            new PasswordHasher<ApplicationUser>();

                        appUser.PasswordHash =
                            passwordHasher.HashPassword(
                                appUser,
                                password);

                        _context.Set<ApplicationUser>().Add(appUser);

                        // -------------------------------------------------
                        // 10. ASPNETUSERROLES
                        // Direct role mapping
                        // -------------------------------------------------
                        _context.Set<IdentityUserRole<string>>().Add(
                            new IdentityUserRole<string>
                            {
                                UserId = appUser.Id,
                                RoleId = role.Id
                            }
                        );

                        // -------------------------------------------------
                        // 11. USERS TABLE
                        //
                        // IMPORTANT:
                        // PostgreSQL users.isactive etc. are BIT.
                        // So BitArray is used.
                        // -------------------------------------------------
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

                            IsMobileVerified = true,

                            IsEmailVerified = true,

                            isUserDeactivated = true
                        };

                        _context.Users.Add(customUser);

                        // -------------------------------------------------
                        // 12. VOLUNTEERS TABLE
                        //
                        // IMPORTANT:
                        // Volunteers.Id == Users.UserID
                        // -------------------------------------------------
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

                        // -------------------------------------------------
                        // 13. NEXT ID
                        // -------------------------------------------------
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

                // ---------------------------------------------------------
                // 14. SAVE ALL
                // ---------------------------------------------------------
                await _context.SaveChangesAsync();

                // ---------------------------------------------------------
                // 15. RESPONSE
                // ---------------------------------------------------------
                return Ok(new
                {
                    success = true,

                    message =
                        $"{addedCount} Volunteers registered successfully.",

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

                    error =
                        ex.InnerException?.Message
                        ?? ex.Message
                });
            }
        }

        // 7. DELETE VOLUNTEER
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

        // =========================================================
        // ADMIN ENDPOINTS FOR VOLUNTEER MANAGEMENT
        // =========================================================

        [HttpPost("admin/upload-format")]
        public async Task<IActionResult> AdminUploadFormat([FromForm] IFormFile formatFile)
        {
            if (formatFile == null || formatFile.Length == 0)
                return BadRequest(new { success = false, message = "Please select a valid excel file." });

            var folder = Path.Combine(_env.WebRootPath, "uploads", "formats");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            // Save standard format file (Override existing)
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
            var affs = await _context.Affiliates.Where(a => a.IsActive).Select(a => new { a.UserID, a.UserCode, Name = a.FirstName + " " + a.LastName + " (" + a.OrganisationName + ")" }).ToListAsync();
            var schools = await _context.Institutes.Where(i => i.IsActive).Select(i => new { i.InstituteID, i.Name, i.ReferredByAffiliateId }).ToListAsync();

            return Ok(new { success = true, affiliates = affs, schools = schools });
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
                    SchoolName = _context.Institutes.Where(i => i.InstituteID == v.InstituteId).Select(i => i.Name).FirstOrDefault(),
                    AffiliateName = _context.Affiliates.Where(a => a.UserID == v.AffiliateId).Select(a => a.FirstName + " " + a.LastName).FirstOrDefault(),
                    v.IsActive,
                    v.CreatedDate
                }).ToListAsync();

            return Ok(new { success = true, data = list, totalRecords, totalPages, currentPage = page });
        }

        // 9. GET SCHOOLS MAPPING FOR A SPECIFIC VOLUNTEER
        // =========================================================
        // 9. GET SCHOOLS MAPPING FOR A SPECIFIC VOLUNTEER
        // =========================================================
        [HttpGet("mapping-schools/{volunteerId}")]
        public async Task<IActionResult> GetVolunteerSchoolMapping(int volunteerId)
        {
            var aff = await GetCurrentAffiliate();

            if (aff == null)
                return Unauthorized(new
                {
                    success = false,
                    message = "Affiliate not found."
                });

            // ---------------------------------------------------------
            // CHECK VOLUNTEER
            // ---------------------------------------------------------
            var volunteer = await _context.Volunteers
                .FirstOrDefaultAsync(v =>
                    v.Id == volunteerId &&
                    v.AffiliateId == aff.UserID &&
                    v.IsActive &&
                    !v.IsDeleted);

            if (volunteer == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Volunteer not found."
                });
            }

            // ---------------------------------------------------------
            // GET AFFILIATE REFERRAL CODE
            // ---------------------------------------------------------
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

            // ---------------------------------------------------------
            // GET ALL SCHOOLS OF AFFILIATE
            // ---------------------------------------------------------
            var allSchools = await _context.SchoolProfileDetails
                .Where(x => x.referralcode == referralCode)
                .Select(x => new
                {
                    instituteId = x.instituteid,
                    name = x.institute_name
                })
                .OrderBy(x => x.name)
                .ToListAsync();

            // ---------------------------------------------------------
            // GET EXISTING MAPPINGS
            // ---------------------------------------------------------
            var mappedSchoolIds = await _context.VolunteerInstituteMappings
                .Where(m =>
                    m.VolunteerId == volunteerId &&
                    !m.IsDeleted &&
                    m.IsActive)
                .Select(m => m.InstituteId)
                .Distinct()
                .ToListAsync();

            // ---------------------------------------------------------
            // ASSIGNED
            // ---------------------------------------------------------
            var assignedSchools = allSchools
                .Where(s => mappedSchoolIds.Contains(s.instituteId))
                .ToList();

            // ---------------------------------------------------------
            // UNASSIGNED
            // ---------------------------------------------------------
            var unassignedSchools = allSchools
                .Where(s => !mappedSchoolIds.Contains(s.instituteId))
                .ToList();

            return Ok(new
            {
                success = true,
                volunteerId = volunteer.Id,
                assignedSchools,
                unassignedSchools
            });
        }

        // =========================================================
        // 10. SAVE MULTIPLE SCHOOL MAPPINGS FOR VOLUNTEER
        // =========================================================
        [HttpPost("save-mapping")]
        public async Task<IActionResult> SaveVolunteerMapping(
     [FromBody] VolunteerMappingDto model)
        {
            try
            {
                var aff = await GetCurrentAffiliate();

                if (aff == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Affiliate not found."
                    });
                }

                if (model == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid mapping data received."
                    });
                }

                if (model.VolunteerId <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid VolunteerId."
                    });
                }

                var instituteIds = model.InstituteIds?
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList()
                    ?? new List<int>();

                // ---------------------------------------------------------
                // CHECK VOLUNTEER
                // ---------------------------------------------------------
                var volunteer = await _context.Volunteers
                    .FirstOrDefaultAsync(v =>
                        v.Id == model.VolunteerId &&
                        v.AffiliateId == aff.UserID &&
                        v.IsActive &&
                        !v.IsDeleted);

                if (volunteer == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Volunteer not found or does not belong to this affiliate."
                    });
                }

                // ---------------------------------------------------------
                // GET REFERRAL CODE
                // ---------------------------------------------------------
                var referralCode = await _context.WJ_ReferralCodes
                    .Where(x => x.UserID == aff.UserID)
                    .Select(x => x.ReferralCode)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(referralCode))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Referral code not found for current affiliate."
                    });
                }

                // ---------------------------------------------------------
                // GET AFFILIATE SCHOOLS
                // ---------------------------------------------------------
                var affiliateSchoolIds = await _context.SchoolProfileDetails
                    .Where(x => x.referralcode == referralCode)
                    .Select(x => x.instituteid)
                    .Distinct()
                    .ToListAsync();

                // ---------------------------------------------------------
                // CHECK INVALID SCHOOL IDs
                // ---------------------------------------------------------
                var invalidInstituteIds = instituteIds
                    .Except(affiliateSchoolIds)
                    .ToList();

                if (invalidInstituteIds.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "One or more selected schools do not belong to this affiliate.",
                        invalidInstituteIds
                    });
                }

                // ---------------------------------------------------------
                // CURRENT USER
                // ---------------------------------------------------------
                string currentUserId =
                    User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? aff.UserID.ToString();

                // ---------------------------------------------------------
                // EXISTING MAPPINGS
                // ---------------------------------------------------------
                var existingMappings =
                    await _context.VolunteerInstituteMappings
                        .Where(m =>
                            m.VolunteerId == model.VolunteerId)
                        .ToListAsync();

                // ---------------------------------------------------------
                // SOFT DELETE OLD MAPPINGS
                // ---------------------------------------------------------
                foreach (var mapping in existingMappings)
                {
                    mapping.IsDeleted = true;
                    mapping.IsActive = false;
                    mapping.ModifiedDate = DateTime.UtcNow;
                    mapping.LastUpdatedBy = currentUserId;
                }

                // ---------------------------------------------------------
                // ADD / REACTIVATE SELECTED MAPPINGS
                // ---------------------------------------------------------
                foreach (var instituteId in instituteIds)
                {
                    var existingMapping = existingMappings
                        .FirstOrDefault(x =>
                            x.InstituteId == instituteId);

                    if (existingMapping != null)
                    {
                        existingMapping.IsDeleted = false;
                        existingMapping.IsActive = true;
                        existingMapping.ModifiedDate = DateTime.UtcNow;
                        existingMapping.LastUpdatedBy = currentUserId;

                        continue;
                    }

                    // -----------------------------------------------------
                    // SCHOOL ALREADY ASSIGNED TO OTHER VOLUNTEER?
                    // -----------------------------------------------------
                    var assignedToOther =
                        await _context.VolunteerInstituteMappings
                            .AnyAsync(m =>
                                m.InstituteId == instituteId &&
                                m.VolunteerId != model.VolunteerId &&
                                !m.IsDeleted &&
                                m.IsActive);

                    if (assignedToOther)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message =
                                $"School ID {instituteId} is already assigned to another volunteer."
                        });
                    }

                    // -----------------------------------------------------
                    // NEW MAPPING
                    // -----------------------------------------------------
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

                // ---------------------------------------------------------
                // SAVE
                // ---------------------------------------------------------
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
                // =====================================================
                // 1. Current logged-in user
                // =====================================================
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User not authenticated."
                    });


                // =====================================================
                // 2. Current Volunteer find karo
                // =====================================================
                var volunteer = await _context.Volunteers
                    .FirstOrDefaultAsync(v =>
                        v.AspNetUserId == userId &&
                        v.IsActive &&
                        !v.IsDeleted);

                if (volunteer == null)
                {
                    return Ok(new
                    {
                        success = true,
                        data = new List<object>()
                    });
                }


                // =====================================================
                // 3. Volunteer ke active mappings
                // =====================================================
                var mappings = await _context.VolunteerInstituteMappings
                    .Where(m =>
                        m.VolunteerId == volunteer.Id &&
                        m.IsActive &&
                        !m.IsDeleted)
                    .Select(m => new
                    {
                        m.InstituteId,
                        m.CreatedDate,
                        m.LastUpdatedBy
                    })
                    .ToListAsync();

                if (!mappings.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        data = new List<object>()
                    });
                }


                // =====================================================
                // 4. Affiliate ka ReferralCode
                // =====================================================
                var affiliate = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.AspNetUserID == volunteer.AspNetUserId);

                if (affiliate == null)
                {
                    return Ok(new
                    {
                        success = true,
                        data = new List<object>()
                    });
                }

                var referralCode = await _context.WJ_ReferralCodes
                    .Where(x => x.UserID == volunteer.AffiliateId)
                    .Select(x => x.ReferralCode)
                    .FirstOrDefaultAsync();


                // =====================================================
                // 5. SchoolProfileDetails se schools lao
                // =====================================================
                var instituteIds = mappings
                    .Select(x => x.InstituteId)
                    .Distinct()
                    .ToList();

                var schools = await _context.SchoolProfileDetails
                    .Where(x =>
                        instituteIds.Contains(x.instituteid) &&
                        x.referralcode == referralCode)
                    .Select(x => new
                    {
                        InstituteID = x.instituteid,
                        Name = x.institute_name
                    })
                    .ToListAsync();


                // =====================================================
                // 6. Assigned By users
                // =====================================================
                var assignedByIds = mappings
                    .Where(x => !string.IsNullOrWhiteSpace(x.LastUpdatedBy))
                    .Select(x => x.LastUpdatedBy!)
                    .Distinct()
                    .ToList();

                var users = await _context.Users
                    .Where(u => assignedByIds.Contains(u.AspNetUserID))
                    .Select(u => new
                    {
                        u.AspNetUserID,
                        u.UserID,
                        u.FirstName,
                        u.MobileNo
                    })
                    .ToDictionaryAsync(
                        x => x.AspNetUserID,
                        x => !string.IsNullOrWhiteSpace(x.FirstName)
                            ? x.FirstName
                            : x.MobileNo
                    );


                // =====================================================
                // 7. Final result
                // =====================================================
                var result = mappings
                    .Join(
                        schools,
                        m => m.InstituteId,
                        s => s.InstituteID,
                        (m, s) => new
                        {
                            s.InstituteID,
                            s.Name,
                            AssignedDate = m.CreatedDate,

                            AssignedBy =
                                !string.IsNullOrWhiteSpace(m.LastUpdatedBy) &&
                                users.ContainsKey(m.LastUpdatedBy!)
                                    ? users[m.LastUpdatedBy!]
                                    : m.LastUpdatedBy
                        })
                    .OrderBy(x => x.Name)
                    .ToList();


                return Ok(new
                {
                    success = true,
                    data = result
                });
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