using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class AffiliateOtherActivity : BaseMasterEntity
    {
        public long AffiliateId { get; set; }

        [Required, StringLength(200)]
        public string ActivityName { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string SchoolName { get; set; } = string.Empty; // Textbox for school not in DB

        [Required, StringLength(500)]
        public string Address { get; set; } = string.Empty;

        [Required, StringLength(10)]
        public string Pincode { get; set; } = string.Empty;

        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(100)]
        public string State { get; set; } = string.Empty;

        [Required, StringLength(500)]
        public string VisitReason { get; set; } = string.Empty;

        [StringLength(500)]
        public string? InitialRemark { get; set; }

        // Update fields
        [StringLength(500)]
        public string? UpdateRemark { get; set; }

        [StringLength(500)]
        public string? ImagePath { get; set; }

        public DateTime? UpdateDate { get; set; }

        [StringLength(30)]
        public string Status { get; set; } = "Pending"; // "Pending", "In Progress", "Completed"
    }
}