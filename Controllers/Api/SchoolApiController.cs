//using HemFoundation.Data;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using System.Security.Claims;

//namespace HemFoundation.Controllers.Api
//{
//    [Route("api/school")]
//    [ApiController]
//    public class SchoolApiController : ControllerBase
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly IWebHostEnvironment _env;

//        public SchoolApiController(ApplicationDbContext context, IWebHostEnvironment env)
//        {
//            _context = context;
//            _env = env;
//        }
       

//        [HttpGet("stats")]
//        public async Task<IActionResult> GetSchoolStats()
//        {
//            string? userEmail = HttpContext.Session.GetString("UserEmail");
//            if (string.IsNullOrEmpty(userEmail))
//                return Unauthorized(new { success = false, message = "User not logged in." });

//            var institute = await (from i in _context.Institutes
//                                   join u in _context.Users on i.AspNetUserID equals u.Id
//                                   where u.Email == userEmail || u.UserName == userEmail
//                                   select i).FirstOrDefaultAsync();

//            if (institute == null)
//                return NotFound(new { success = false, message = "Institute profile not found for this user." });

//            int studentsCount = await _context.Students
//                .Where(s => s.InstituteID == institute.InstituteID && s.IsActive)
//                .CountAsync();

//            return Ok(new
//            {
//                success = true,
//                instituteName = institute.Name,
//                isVerified = institute.IsVerify,
//                studentsCount = studentsCount
//            });
//        }

//        [HttpPost("Dashboard/UploadPartnershipLetter")]
//        public async Task<IActionResult> UploadPartnershipLetter([FromForm] IFormFile letterFile)
//        {
//            // ============================================
//            // 1. Get logged-in AspNetUser ID
//            // ============================================

//            string? userId = HttpContext.Session.GetString("UserId");

//            if (string.IsNullOrEmpty(userId))
//            {
//                userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
//            }

//            if (string.IsNullOrEmpty(userId))
//            {
//                return Unauthorized(new
//                {
//                    success = false,
//                    message = "User not logged in."
//                });
//            }


//            // ============================================
//            // 2. Find Institute using AspNetUserID
//            // ============================================

//            var institute = await _context.Institutes
//                .FirstOrDefaultAsync(i =>
//                    i.AspNetUserID == userId &&
//                    i.IsActive);

//            if (institute == null)
//            {
//                return NotFound(new
//                {
//                    success = false,
//                    message = "Institute profile not found."
//                });
//            }


//            // ============================================
//            // 3. Validate file
//            // ============================================

//            if (letterFile == null || letterFile.Length == 0)
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Please select a valid PDF file."
//                });
//            }


//            // Only PDF
//            var extension = Path.GetExtension(letterFile.FileName)
//                .ToLowerInvariant();

//            if (extension != ".pdf")
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "Only PDF files are allowed."
//                });
//            }


//            // Optional: 10 MB maximum
//            const long maxFileSize = 10 * 1024 * 1024;

//            if (letterFile.Length > maxFileSize)
//            {
//                return BadRequest(new
//                {
//                    success = false,
//                    message = "File size must not exceed 10 MB."
//                });
//            }


//            // ============================================
//            // 4. Create upload directory
//            // ============================================

//            var uploadsFolder = Path.Combine(
//                _env.WebRootPath,
//                "uploads",
//                "partnershipletters"
//            );

//            if (!Directory.Exists(uploadsFolder))
//            {
//                Directory.CreateDirectory(uploadsFolder);
//            }


//            // ============================================
//            // 5. Generate safe unique file name
//            // ============================================

//            string sanitizedInstituteName =
//                string.IsNullOrWhiteSpace(institute.Name)
//                    ? "School"
//                    : string.Concat(
//                        institute.Name.Where(char.IsLetterOrDigit)
//                      );

//            string dateStr =
//                DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");

//            string uniqueFileName =
//                $"{sanitizedInstituteName}_{dateStr}_{Guid.NewGuid():N}.pdf";

//            string filePath =
//                Path.Combine(uploadsFolder, uniqueFileName);


//            // ============================================
//            // 6. Save file
//            // ============================================

//            using (var stream = new FileStream(
//                filePath,
//                FileMode.Create))
//            {
//                await letterFile.CopyToAsync(stream);
//            }


//            // ============================================
//            // 7. Delete old letter if re-uploading
//            // ============================================

//            if (!string.IsNullOrWhiteSpace(institute.PartnershipLetterPath))
//            {
//                var oldRelativePath =
//                    institute.PartnershipLetterPath
//                        .TrimStart('/')
//                        .Replace('/', Path.DirectorySeparatorChar);

//                var oldFilePath =
//                    Path.Combine(_env.WebRootPath, oldRelativePath);

//                if (System.IO.File.Exists(oldFilePath))
//                {
//                    try
//                    {
//                        System.IO.File.Delete(oldFilePath);
//                    }
//                    catch
//                    {
//                        // Old file deletion failure should not
//                        // prevent new upload from succeeding.
//                    }
//                }
//            }


//            // ============================================
//            // 8. Save database path
//            // ============================================

//            institute.PartnershipLetterPath =
//                "/uploads/partnershipletters/" + uniqueFileName;

//            await _context.SaveChangesAsync();


//            // ============================================
//            // 9. Success
//            // ============================================

//            return Ok(new
//            {
//                success = true,
//                message = "Partnership letter uploaded successfully.",
//                fileUrl = institute.PartnershipLetterPath
//            });
//        }

//    }
//}