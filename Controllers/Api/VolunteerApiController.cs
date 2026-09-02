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

        private async Task<Affiliate?> GetCurrentAffiliate()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return await _context.Affiliates.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
        }

        // 1. GET ASSIGNED SCHOOLS (For Dropdown)
        [HttpGet("assigned-schools")]
        public async Task<IActionResult> GetAssignedSchools()
        {
            var aff = await GetCurrentAffiliate();
            if (aff == null) return Unauthorized();

            string affiliateIdStr = aff.UserID.ToString();

            var schools = await _context.Institutes
                .Where(i => i.ReferredByAffiliateId == affiliateIdStr && i.IsActive)
                .Select(i => new { i.InstituteID, Name = i.Name })
                .ToListAsync();

            return Ok(new { success = true, data = schools });
        }

        // 2. GET VOLUNTEERS LIST
        [HttpGet("list")]
        public async Task<IActionResult> GetList()
        {
            var aff = await GetCurrentAffiliate();
            if (aff == null) return Unauthorized();

            var list = await (from v in _context.Volunteers.Where(v => v.AffiliateId == aff.UserID && !v.IsDeleted)
                              join i in _context.Institutes on v.InstituteId equals i.InstituteID
                              orderby v.CreatedDate descending
                              select new
                              {
                                  v.Id,
                                  Name = v.FirstName + " " + v.LastName,
                                  v.MobileNo,
                                  v.Email,
                                  SchoolName = i.Name,
                                  v.IsActive,
                                  v.CreatedDate,
                                  v.InstituteId
                              }).ToListAsync();

            return Ok(new { success = true, data = list });
        }

        // 8. GET VOLUNTEER STATS FOR CARDS
        [HttpGet("stats")]
        public async Task<IActionResult> GetVolunteerStats()
        {
            var aff = await GetCurrentAffiliate();
            if (aff == null) return Unauthorized();

            string affiliateIdStr = aff.UserID.ToString();

            // 1. Active Volunteers Count
            int activeVolunteers = await _context.Volunteers
                .CountAsync(v => v.AffiliateId == aff.UserID && v.IsActive && !v.IsDeleted);

            // 2. Schools Mapped Count (Assigned schools)
            int schoolMapped = await _context.Institutes
                .CountAsync(i => i.ReferredByAffiliateId == affiliateIdStr && i.IsActive);

            // 3. Assigned Tasks Count (Aapke task table ya logic ke anusaar, filhal 0 ya query rakh sakte hain)
            int assignedTasks = 0; // Agar task table ho toh yahan count query likhein

            // 4. Total Reports Count (Aapke reports table ya logic ke anusaar)
            int reportsCount = 0; // Agar reports table ho toh yahan count query likhein

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
            if (aff == null) return Unauthorized();

            if (model.Id > 0)
            {
                var vol = await _context.Volunteers.FirstOrDefaultAsync(v => v.Id == model.Id && v.AffiliateId == aff.UserID);
                if (vol == null) return NotFound(new { success = false, message = "Volunteer not found." });

                vol.FirstName = model.FirstName;
                vol.LastName = model.LastName;
                vol.MobileNo = model.MobileNo;
                vol.Email = model.Email;
                vol.InstituteId = model.InstituteId;
                vol.ModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Volunteer updated successfully." });
            }
            else
            {
                string roleName = "Volunteer";
                if (!await _roleManager.RoleExistsAsync(roleName))
                    await _roleManager.CreateAsync(new IdentityRole(roleName));

                string baseMobile = model.MobileNo.Trim();
                string uniqueUsername = baseMobile;

                int counter = 1;
                while (await _userManager.FindByNameAsync(uniqueUsername) != null)
                {
                    uniqueUsername = $"{baseMobile}_{counter}";
                    counter++;
                }

                var appUser = new ApplicationUser
                {
                    UserName = uniqueUsername,
                    Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                    FullName = $"{model.FirstName} {model.LastName}",
                    PhoneNumber = baseMobile,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(appUser, uniqueUsername);
                if (!result.Succeeded)
                    return BadRequest(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });

                await _userManager.AddToRoleAsync(appUser, roleName);

                var volunteer = new Volunteer
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    MobileNo = baseMobile,
                    Email = model.Email,
                    InstituteId = model.InstituteId,
                    AffiliateId = aff.UserID,
                    AspNetUserId = appUser.Id,
                    Password_ = uniqueUsername,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                _context.Volunteers.Add(volunteer);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Volunteer registered successfully." });
            }
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
        [HttpPost("save-bulk")]
        public async Task<IActionResult> SaveBulk([FromBody] BulkSaveRequestDto request)
        {
            if (request == null || request.ValidData == null || !request.ValidData.Any())
            {
                return BadRequest(new { success = false, message = "Invalid data received." });
            }

            var aff = await GetCurrentAffiliate();
            if (aff == null) return Unauthorized();

            string roleName = "Volunteer";
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));

            int addedCount = 0;

            foreach (var item in request.ValidData)
            {
                string baseMobile = item.MobileNo.Trim();
                string uniqueUsername = baseMobile;
                int counter = 1;

                while (await _userManager.FindByNameAsync(uniqueUsername) != null)
                {
                    uniqueUsername = $"{baseMobile}_{counter}";
                    counter++;
                }

                string? cleanEmail = string.IsNullOrWhiteSpace(item.Email) || !item.Email.Contains("@") ? null : item.Email.Trim();

                var appUser = new ApplicationUser
                {
                    UserName = uniqueUsername,
                    Email = cleanEmail,
                    FullName = $"{item.FirstName} {item.LastName}",
                    PhoneNumber = baseMobile,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(appUser, uniqueUsername);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(appUser, roleName);

                    _context.Volunteers.Add(new Volunteer
                    {
                        FirstName = item.FirstName.Trim(),
                        LastName = item.LastName.Trim(),
                        MobileNo = baseMobile,
                        Email = cleanEmail,
                        InstituteId = request.InstituteId,
                        AffiliateId = aff.UserID,
                        AspNetUserId = appUser.Id,
                        Password_ = uniqueUsername,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow
                    });
                    addedCount++;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = $"{addedCount} Volunteers verified and saved successfully!" });
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
        [HttpGet("mapping-schools/{volunteerId}")]
        public async Task<IActionResult> GetVolunteerSchoolMapping(int volunteerId)
        {
            var aff = await GetCurrentAffiliate();
            if (aff == null) return Unauthorized();

            string affiliateIdStr = aff.UserID.ToString();

            // Affiliate ke saare assigned active schools
            var allAssignedSchools = await _context.Institutes
                .Where(i => i.ReferredByAffiliateId == affiliateIdStr && i.IsActive)
                .Select(i => new { i.InstituteID, Name = i.Name })
                .ToListAsync();

            // Jo schools is volunteer ko already mapped hain
            var mappedSchoolIds = await _context.VolunteerInstituteMappings
                .Where(m => m.VolunteerId == volunteerId && !m.IsDeleted)
                .Select(m => m.InstituteId)
                .ToListAsync();

            var assignedList = allAssignedSchools.Where(s => mappedSchoolIds.Contains(s.InstituteID)).ToList();
            var unassignedList = allAssignedSchools.Where(s => !mappedSchoolIds.Contains(s.InstituteID)).ToList();

            return Ok(new
            {
                success = true,
                assignedSchools = assignedList,
                unassignedSchools = unassignedList
            });
        }

        // 10. SAVE MULTIPLE SCHOOL MAPPINGS FOR VOLUNTEER
        [HttpPost("save-mapping")]
        public async Task<IActionResult> SaveVolunteerMapping([FromBody] VolunteerMappingDto model)
        {
            var aff = await GetCurrentAffiliate();
            if (aff == null) return Unauthorized();

            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? aff.UserID.ToString();

            // 1. Is volunteer ke saare existing mappings hata dein
            var existingMappings = await _context.VolunteerInstituteMappings
                .Where(m => m.VolunteerId == model.VolunteerId)
                .ToListAsync();

            _context.VolunteerInstituteMappings.RemoveRange(existingMappings);

            // 2. Agar naye schools select hue hain
            if (model.InstituteIds != null && model.InstituteIds.Any())
            {
                // Agar rule yeh hai ki ek school sirf ek hi volunteer ke paas ho sakta hai,
                // toh baaki volunteers ke paas se yeh schools pehle remove kar dein:
                var conflictingMappings = await _context.VolunteerInstituteMappings
                    .Where(m => model.InstituteIds.Contains(m.InstituteId) && !m.IsDeleted)
                    .ToListAsync();

                if (conflictingMappings.Any())
                {
                    _context.VolunteerInstituteMappings.RemoveRange(conflictingMappings);
                }

                // Ab naye mappings add karein with History tracking
                foreach (var instId in model.InstituteIds)
                {
                    _context.VolunteerInstituteMappings.Add(new VolunteerInstituteMapping
                    {
                        VolunteerId = model.VolunteerId,
                        InstituteId = instId,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow,
                        LastUpdatedBy = currentUserId,
                        ModifiedDate = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "School mappings and update history saved successfully." });
        }

        [HttpGet("my-assigned-schools")]
        public async Task<IActionResult> GetMyAssignedSchools()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var volunteer = await _context.Volunteers
                .FirstOrDefaultAsync(v =>
                    v.AspNetUserId == userId &&
                    !v.IsDeleted);

            if (volunteer == null)
            {
                return Ok(new
                {
                    success = true,
                    data = new List<object>()
                });
            }

            var schoolList = await (
                from m in _context.VolunteerInstituteMappings

                join i in _context.Institutes
                    on m.InstituteId equals i.InstituteID

                where
                    m.VolunteerId == volunteer.Id &&
                    !m.IsDeleted &&
                    i.IsActive

                select new
                {
                    i.InstituteID,
                    i.Name,

                    AssignedDate = m.CreatedDate,

                    AssignedByUserId = m.LastUpdatedBy
                }
            ).ToListAsync();


            var assignedByIds = schoolList
                .Where(x => !string.IsNullOrEmpty(x.AssignedByUserId))
                .Select(x => x.AssignedByUserId)
                .Distinct()
                .ToList();


            var users = await _context.Users
                .Where(u => assignedByIds.Contains(u.UserID.ToString()))
                .Select(u => new
                {
                    Id = u.AspNetUserID,
                    u.UserID,
                    u.FirstName,
                    u.MobileNo
                })
                .ToDictionaryAsync(
                    x => x.Id,
                    x => string.IsNullOrWhiteSpace(x.UserID.ToString())
                        ? x.FirstName
                        : x.MobileNo
                );


            var result = schoolList.Select(x => new
            {
                x.InstituteID,
                x.Name,
                x.AssignedDate,

                AssignedBy =
                    !string.IsNullOrEmpty(x.AssignedByUserId) &&
                    users.ContainsKey(x.AssignedByUserId)
                        ? users[x.AssignedByUserId]
                        : x.AssignedByUserId
            }).ToList();


            return Ok(new
            {
                success = true,
                data = result
            });
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