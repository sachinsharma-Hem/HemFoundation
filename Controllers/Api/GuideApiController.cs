using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Route("api/guide")]
    [ApiController]
    public class GuideApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public GuideApiController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ==========================================
        // DTO for accepting only required form fields
        // ==========================================
        public class GuideUploadDto
        {
            public string MainCategory { get; set; } = string.Empty;
            public string SubCategory { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(104857600)] // Allows up to 100MB per file
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
        public async Task<IActionResult> UploadGuide([FromForm] GuideUploadDto dto, IFormFile? uploadFile, [FromForm] string? VideoLink)
        {
            // Validation
            if ((uploadFile == null || uploadFile.Length == 0) && string.IsNullOrEmpty(VideoLink))
                return BadRequest(new { success = false, message = "Please upload a file or provide a video link." });

            // Initialize actual DB Entity mapping frontend data
            var model = new TrainingGuideMaster
            {
                MainCategory = dto.MainCategory,
                SubCategory = dto.SubCategory,
                Title = dto.Title,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };

            // Set specific path: wwwroot/images/training/
            string uploadsFolder = Path.Combine(_env.WebRootPath, "images", "training");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // Handle Local File Upload
            if (uploadFile != null && uploadFile.Length > 0)
            {
                string fileExtension = Path.GetExtension(uploadFile.FileName).ToLower();
                string uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadFile.CopyToAsync(stream);
                }

                model.FileUrl = $"/images/training/{uniqueFileName}";

                if (fileExtension == ".mp4" || fileExtension == ".mov" || fileExtension == ".webm")
                    model.FileType = "Video";
                else
                    model.FileType = "Document";
            }
            // Handle External Link
            else if (!string.IsNullOrEmpty(VideoLink))
            {
                model.FileUrl = VideoLink;
                model.FileType = "VideoLink";
            }

            _context.TrainingGuideMasters.Add(model);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Guide uploaded successfully." });
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetGuides()
        {
            var guides = await _context.TrainingGuideMasters
                .OrderByDescending(g => g.CreatedDate)
                .ToListAsync();
            return Ok(new { success = true, data = guides });
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteGuide(int id)
        {
            var guide = await _context.TrainingGuideMasters.FindAsync(id);
            if (guide == null) return NotFound(new { success = false, message = "Guide not found." });

            _context.TrainingGuideMasters.Remove(guide);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Guide deleted successfully." });
        }
    }
}