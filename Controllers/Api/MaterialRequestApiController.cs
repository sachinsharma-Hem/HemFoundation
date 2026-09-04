using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Authorize]
    [Route("api/material-request")]
    [ApiController]
    public class MaterialRequestApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MaterialRequestApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<long> GetAffiliateId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return 0;

            return await _context.Users
                .Where(x => x.AspNetUserID == userId)
                .Select(x => x.UserID)
                .FirstOrDefaultAsync();
        }
        // ================= AFFILIATE API =================

        // Affiliate ke dropdown ke liye (Sirf Active items aayenge)
        [HttpGet("items")]
        public async Task<IActionResult> GetItems()
        {
            var items = await _context.RequisitionItems.Where(i => i.IsActive && !i.IsDeleted).ToListAsync();
            return Ok(new { success = true, data = items });
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateRequest([FromBody] MaterialRequest model)
        {
            model.AffiliateId = await GetAffiliateId();
            model.Status = "Pending";
            model.CreatedDate = DateTime.UtcNow;
            _context.MaterialRequests.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Material request submitted successfully." });
        }

        [HttpGet("my-requests")]
        public async Task<IActionResult> GetMyRequests()
        {
            long affId = await GetAffiliateId();

            var requests = await _context.MaterialRequests
                .Where(r => r.AffiliateId == affId && !r.IsDeleted)
                .Join(
                    _context.RequisitionItems,
                    r => r.RequisitionItemId,
                    i => i.Id,
                    (r, i) => new
                    {
                        r.Id,
                        ItemName = i.ItemName,
                        r.RequestedQuantity,
                        r.ApprovedQuantity,
                        r.NeedDate,
                        r.Priority,
                        r.Status,
                        r.ExpectedDate,
                        r.AdminRemarks,
                        r.CreatedDate
                    })
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = requests
            });
        }

        // ================= ADMIN API =================

        [HttpGet("admin/all-requests")]
        public async Task<IActionResult> GetAllRequests(
    [FromQuery] string? status,
    [FromQuery] long? affiliateId)
        {
            var query = _context.MaterialRequests
                .Where(r => !r.IsDeleted);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            if (affiliateId.HasValue && affiliateId > 0)
                query = query.Where(r => r.AffiliateId == affiliateId);

            var requests = await query
                .Join(
                    _context.Users,
                    r => r.AffiliateId,
                    u => u.UserID,
                    (r, u) => new { r, u }
                )
                .Join(
                    _context.RequisitionItems,
                    x => x.r.RequisitionItemId,
                    i => i.Id,
                    (x, i) => new
                    {
                        x.r.Id,
                        AffiliateName = x.u.FirstName + " " + x.u.LastName,
                        ItemName = i.ItemName,
                        x.r.RequestedQuantity,
                        x.r.ApprovedQuantity,
                        x.r.ShippingAddress,
                        x.r.NeedDate,
                        x.r.Priority,
                        x.r.UserRemarks,
                        x.r.Status,
                        x.r.ExpectedDate,
                        x.r.AdminRemarks,
                        x.r.CreatedDate
                    }
                )
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = requests
            });
        }

        [HttpPost("admin/process")]
        public async Task<IActionResult> ProcessRequest([FromBody] AdminProcessRequestDto model)
        {
            var request = await _context.MaterialRequests.FindAsync(model.RequestId);
            if (request == null) return NotFound(new { success = false, message = "Request not found" });

            request.Status = model.Status;
            request.ApprovedQuantity = model.ApprovedQuantity;
            request.ExpectedDate = model.ExpectedDate;
            request.AdminRemarks = model.AdminRemarks;
            request.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Request processed successfully." });
        }

        // Admin item management ke liye (Saare items aayenge, chahe inactive kyun na ho)
        [HttpGet("admin/items")]
        public async Task<IActionResult> GetAllRequisitionItems()
        {
            var items = await _context.RequisitionItems
                .Where(i => !i.IsDeleted)
                .OrderByDescending(i => i.Id)
                .ToListAsync();
            return Ok(new { success = true, data = items });
        }

        [HttpPost("admin/items")]
        public async Task<IActionResult> SaveRequisitionItem([FromBody] RequisitionItem model)
        {
            if (string.IsNullOrEmpty(model.ItemName))
                return BadRequest(new { success = false, message = "Item name is required." });

            if (model.Id > 0)
            {
                var existingItem = await _context.RequisitionItems.FindAsync(model.Id);
                if (existingItem == null) return NotFound(new { success = false, message = "Item not found." });

                existingItem.ItemName = model.ItemName;
                existingItem.IsActive = model.IsActive;
                existingItem.ModifiedDate = DateTime.UtcNow;
            }
            else
            {
                model.CreatedDate = DateTime.UtcNow;
                _context.RequisitionItems.Add(model);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Item saved successfully." });
        }

        [HttpDelete("admin/items/{id}")]
        public async Task<IActionResult> DeleteRequisitionItem(int id)
        {
            var item = await _context.RequisitionItems.FindAsync(id);
            if (item == null) return NotFound(new { success = false, message = "Item not found." });

            item.IsDeleted = true; // Soft Delete
            item.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Item deleted successfully." });
        }
    }

    public class AdminProcessRequestDto
    {
        public int RequestId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ApprovedQuantity { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string? AdminRemarks { get; set; }
    }
}