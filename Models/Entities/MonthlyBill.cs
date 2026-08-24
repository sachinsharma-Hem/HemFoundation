using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class MonthlyBill : BaseMasterEntity
    {
        [StringLength(50)]
        public string BillNumber { get; set; } = string.Empty; // Format: 0001, 0002...

        public long AffiliateId { get; set; }
        public int OrganizationId { get; set; } // Billed To

        public DateTime BillDate { get; set; }

        [StringLength(255)]
        public string Particulars { get; set; } = string.Empty; // Main heading

        public decimal TotalAmount { get; set; }

        [StringLength(500)]
        public string AmountInWords { get; set; } = string.Empty;

        // URLs
        [StringLength(500)]
        public string? GeneratedPdfUrl { get; set; } // System generated PDF
        [StringLength(500)]
        public string? SignedPdfUrl { get; set; } // Uploaded by Affiliate

        // Status: Draft, Generated, Uploaded, In Process, Approved, Paid, Rejected
        [StringLength(50)]
        public string Status { get; set; } = "Draft";

        [StringLength(1000)]
        public string? AdminRemarks { get; set; } // Corrections or rejections
    }
}