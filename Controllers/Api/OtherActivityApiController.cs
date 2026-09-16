using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Authorize]
    [Route("api/other-activity")]
    [ApiController]
    public class OtherActivityApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public OtherActivityApiController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private async Task<long> GetCurrentAffiliateIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return 0;

            return await _context.Users
                .Where(x => x.AspNetUserID == userId)
                .Select(x => x.UserID)
                .FirstOrDefaultAsync();
        }

        // ================= ACTIVITY ENDPOINTS =================

        [HttpGet("list")]
        public async Task<IActionResult> GetActivities()
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var list = await _context.AffiliateOtherActivities
                .Where(x => x.AffiliateId == affId && !x.IsDeleted)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return Ok(new { success = true, data = list });
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateActivity([FromBody] ActivityCreateModel model)
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var activity = new AffiliateOtherActivity
            {
                AffiliateId = affId,
                ActivityName = model.ActivityName,
                SchoolName = model.SchoolName,
                Address = model.Address,
                Pincode = model.Pincode,
                City = model.City,
                State = model.State,
                VisitReason = model.VisitReason,
                InitialRemark = model.InitialRemark,
                Status = "Pending",
                CreatedDate = DateTime.UtcNow
            };

            _context.AffiliateOtherActivities.Add(activity);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Activity created successfully." });
        }

        [HttpPost("update-progress")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateProgress(
      [FromForm] long id,
      [FromForm] string? updateRemark,
      [FromForm] string? status,
      [FromForm] IFormFile? activityImage)
        {
            try
            {
                long affId = await GetCurrentAffiliateIdAsync();

                if (affId == 0)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Unauthorized. Affiliate not found."
                    });
                }

                // Validate ID
                if (id <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid activity ID."
                    });
                }

                // Find activity only for logged-in affiliate
                var item = await _context.AffiliateOtherActivities
                    .FirstOrDefaultAsync(x =>
                        x.Id == id &&
                        x.AffiliateId == affId &&
                        !x.IsDeleted);

                if (item == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Activity not found or you do not have permission to update it."
                    });
                }

                // Clean values
                updateRemark = updateRemark?.Trim() ?? string.Empty;
                status = status?.Trim() ?? "Pending";

                // Allowed statuses
                var allowedStatuses = new[]
                {
            "Pending",
            "InProgress",
            "Completed",
            "Cancelled"
        };

                if (!allowedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Invalid status '{status}'. Allowed values: Pending, InProgress, Completed, Cancelled."
                    });
                }

                // Update basic information
                item.UpdateRemark = updateRemark;
                item.Status = status;
                item.UpdateDate = DateTime.UtcNow;
                item.ModifiedDate = DateTime.UtcNow;

                // ================= IMAGE UPLOAD =================

                if (activityImage != null && activityImage.Length > 0)
                {
                    // Optional file size limit: 10 MB
                    if (activityImage.Length > 10 * 1024 * 1024)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Image size cannot exceed 10 MB."
                        });
                    }

                    var uploadsFolder = Path.Combine(
                        _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
                        "uploads",
                        "activities"
                    );

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Safe extension
                    var extension = Path.GetExtension(activityImage.FileName);

                    var allowedExtensions = new[]
                    {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

                    if (!allowedExtensions.Contains(
                            extension,
                            StringComparer.OrdinalIgnoreCase))
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Only JPG, JPEG, PNG and WEBP images are allowed."
                        });
                    }

                    var uniqueFileName =
                        $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

                    var filePath = Path.Combine(
                        uploadsFolder,
                        uniqueFileName
                    );

                    await using (var stream = new FileStream(
                        filePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        await activityImage.CopyToAsync(stream);
                    }

                    item.ImagePath =
                        $"/uploads/activities/{uniqueFileName}";
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Activity updated successfully.",
                    data = new
                    {
                        item.Id,
                        item.Status,
                        item.UpdateRemark,
                        item.ImagePath,
                        item.UpdateDate
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while updating the activity.",
                    error = ex.Message
                });
            }
        }

        // ================= EXPENSE ENDPOINTS =================

        [HttpGet("expense/list")]
        public async Task<IActionResult> GetExpenses()
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var list = await _context.AffiliateExpenses
                .Where(x => x.AffiliateId == affId && !x.IsDeleted)
                .OrderByDescending(x => x.ExpenseDate)
                .ToListAsync();

            return Ok(new { success = true, data = list });
        }

        [HttpPost("expense/create")]
        public async Task<IActionResult> CreateExpense([FromBody] ExpenseCreateModel model)
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            // Auto-prepend related information to ExpenseDetail for clarity
            string prefix = model.RelatedType == "SchoolVisit" ? "[School Visit] " : (model.RelatedType == "OtherActivity" ? "[Other Activity] " : "");
            string finalDetail = prefix + model.ExpenseDetail;

            var expense = new AffiliateExpense
            {
                AffiliateId = affId,
                ExpenseDate = model.ExpenseDate.Date,
                ExpenseDetail = finalDetail,
                Amount = model.Amount,
                RelatedType = model.RelatedType,
                RelatedId = model.RelatedId,
                Status = "Pending",
                CreatedDate = DateTime.UtcNow
            };

            _context.AffiliateExpenses.Add(expense);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Expense submitted successfully." });
        }

        // Get list of completed/active school visits for dropdown
        [HttpGet("expense/school-visits")]
        public async Task<IActionResult> GetSchoolVisitsForExpense()
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var visits = await _context.AffiliateVisitPlans
                .Where(v => v.AffiliateId == affId && !v.IsDeleted)
                .Join(_context.SchoolProfileDetails, v => v.InstituteID, s => s.instituteid, (v, s) => new
                {
                    id = v.Id,
                    name = s.institute_name + (!string.IsNullOrEmpty(s.cityname) ? $" ({s.cityname})" : "")
                })
                .ToListAsync();

            return Ok(new { success = true, data = visits });
        }

        // Get list of activities for dropdown
        [HttpGet("expense/activities-list")]
        public async Task<IActionResult> GetActivitiesForExpense()
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var activities = await _context.AffiliateOtherActivities
                .Where(x => x.AffiliateId == affId && !x.IsDeleted)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.ActivityName + " - " + x.SchoolName
                })
                .ToListAsync();

            return Ok(new { success = true, data = activities });
        }
    }


    public class ActivityCreateModel
    {
        public string ActivityName { get; set; } = string.Empty;
        public string SchoolName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string VisitReason { get; set; } = string.Empty;
        public string? InitialRemark { get; set; }
    }

    public class ActivityUpdateModel
    {
        public long Id { get; set; }
        public string UpdateRemark { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class ExpenseCreateModel
    {
        public DateTime ExpenseDate { get; set; }
        public string ExpenseDetail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? RelatedType { get; set; }
        public long? RelatedId { get; set; }
    }
}