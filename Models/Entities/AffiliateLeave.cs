using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public class AffiliateLeave : BaseMasterEntity
    {
        public long AffiliateId { get; set; } // Matches Affiliate.UserID (long)

        [Required, DataType(DataType.Date)]
        public DateTime FromDate { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime ToDate { get; set; }

        [Required, StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"

        [StringLength(500)]
        public string? AdminRemarks { get; set; }

        public DateTime? ActionDate { get; set; }
    }
}