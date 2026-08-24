using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class MaterialRequest : BaseMasterEntity
    {
        public long AffiliateId { get; set; }
        public int RequisitionItemId { get; set; }

        public int RequestedQuantity { get; set; }
        public int? ApprovedQuantity { get; set; } // Admin can change this

        [StringLength(500)]
        public string ShippingAddress { get; set; } = string.Empty;

        public DateTime NeedDate { get; set; }

        [StringLength(50)]
        public string Priority { get; set; } = "Medium"; // Low, Medium, High

        [StringLength(1000)]
        public string? UserRemarks { get; set; }

        // Admin Section
        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Dispatched

        public DateTime? ExpectedDate { get; set; }

        [StringLength(1000)]
        public string? AdminRemarks { get; set; }
    }
}