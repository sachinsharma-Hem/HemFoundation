using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class Volunteer : BaseMasterEntity // Assuming IsActive, IsDeleted, CreatedDate are here
    {
        [Required, StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string MobileNo { get; set; } = string.Empty;

        [StringLength(150)]
        public string? Email { get; set; }

        public int InstituteId { get; set; } // The school assigned to this volunteer

        public long AffiliateId { get; set; } // The affiliate who created this volunteer

        [StringLength(450)]
        public string AspNetUserId { get; set; } = string.Empty;

        [StringLength(100)]
        public string Password_ { get; set; } = string.Empty; // Store original password pattern as requested
    }
}