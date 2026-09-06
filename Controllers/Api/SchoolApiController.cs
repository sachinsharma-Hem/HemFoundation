using HemFoundation.Data;
using HemFoundation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Route("api/school")]
    [ApiController]
    public class SchoolApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SchoolApiController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetSchoolStats()
        {

            // 1. Retrieve it as a nullable integer directly from the session
            int? userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null || userId == 0)
            {
                // Fall back to claims if session is missing
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                userId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }

            if (userId == null || userId == 0)
            {
                return Unauthorized(new { success = false, message = "User not logged in." });
            }
            if (userId==0)
                return Unauthorized(new { success = false, message = "User not logged in." });
            var institute = await (from i in _context.SchoolProfileDetails
                                   join u in _context.Users on i.instituteid equals u.UserID
                                   where u.UserID == userId
                                   select i).FirstOrDefaultAsync();

            if (institute == null)
                return NotFound(new { success = false, message = "Institute profile not found for this user." });

           

            return Ok(new
            {
                success = true,
                instituteName = institute.institute_name,
                isVerified = institute.isverify,
                studentsCount = institute.associatedstudents
            });
        }
        [HttpPost("Dashboard/UploadPartnershipLetter")]
        public async Task<IActionResult> UploadKnowledgePartnership([FromForm(Name = "letterFile")] IFormFile UploadDoc)
        {
            // ============================================
            // 1. Get logged-in User ID (Session or Claims)
            // ============================================
            // 1. Retrieve it as a nullable integer directly from the session
            int? userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null || userId == 0)
            {
                // Fall back to claims if session is missing
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                userId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }

            if (userId == null || userId == 0)
            {
                return Unauthorized(new { success = false, message = "User not logged in." });
            }

            // ============================================
            // 2. Find Institute from WJ_Institutes using AspNetUserID or UserID
            // ============================================
            var institute = await _context.WJ_Institutes
                .FirstOrDefaultAsync(i =>  i.InstituteID == userId);

            if (institute == null)
            {
                return NotFound(new { success = false, message = "Institute profile not found." });
            }

            int instituteId = institute.InstituteID;

            // ============================================
            // 3. Check if Partnership Letter is already uploaded
            // ============================================
            // Checking via Entity Framework instead of raw SQL injection-prone queries
            bool alreadyUploaded = await _context.KnowledgePartnersList
                .AnyAsync(k => k.InstituteID == instituteId && k.FileUrl != null);

            if (alreadyUploaded)
            {
                return BadRequest(new { success = false, message = "You have already uploaded Partnership Letter." });
            }

            // ============================================
            // 4. Validate file presence and extension
            // ============================================
            if (UploadDoc == null || UploadDoc.Length == 0)
            {
                return BadRequest(new { success = false, message = "Please select a valid file." });
            }

            var extension = Path.GetExtension(UploadDoc.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".txt", ".pdf" };

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Only .jpg, .jpeg, .png, .txt and .pdf extension files are allowed to upload."
                });
            }

            // Optional 10MB size limit check
            if (UploadDoc.Length > 10 * 1024 * 1024)
            {
                return BadRequest(new { success = false, message = "File size must not exceed 10 MB." });
            }

            // ============================================
            // 5. Prepare Directory & Unique File Path
            // ============================================
            string rawInstituteName = string.IsNullOrWhiteSpace(institute.Name) ? "School" : institute.Name.Trim();
            string sanitizedInstituteName = string.Concat(rawInstituteName.Where(char.IsLetterOrDigit));

            // Match legacy folder pattern: {InstituteID}_{InstituteName}
            string folderName = $"{instituteId}_{sanitizedInstituteName}";
            var uploadsFolder = Path.Combine(_env.WebRootPath, "Uploads", "KnowledgePartnerships", folderName);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string originalFileNameWithoutExt = Path.GetFileNameWithoutExtension(UploadDoc.FileName);
            string safeFileName = string.Concat(originalFileNameWithoutExt.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));
            string uniqueFileName = $"{safeFileName}_{Guid.NewGuid():N}{extension}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // ============================================
            // 6. Save File to Disk
            // ============================================
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await UploadDoc.CopyToAsync(stream);
            }

            // Relative file path structure for database storage
            string saveFileName = $"/Uploads/KnowledgePartnerships/{folderName}/{uniqueFileName}";

            // ============================================
            // 7. Insert Record into Database 
            // ============================================
            // Recommended: Use your EF Core Context DbData model instead of raw SqlCommand for PostgreSQL compatibility
            var partnerRecord = new KnowledgePartnersList
            {
                InstituteID = instituteId,
                InstituteName = rawInstituteName,
                FileUrl = saveFileName
                //CreatedOn = DateTime.UtcNow
            };

            _context.KnowledgePartnersList.Add(partnerRecord);
            await _context.SaveChangesAsync();

            // ============================================
            // 8. Return Success Response
            // ============================================
            return Ok(new
            {
                success = true,
                message = extension == ".jpg" || extension == ".jpeg" || extension == ".png"
                    ? "Image Uploaded Successfully!!!!...."
                    : "File Uploaded Successfully!!!!....",
                fileUrl = saveFileName
            });
        }
    }
}