using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class Admin
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public string IdentityUserId { get; set; } = string.Empty; // Maps to AspNetUsers.Id (Guid string)

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? LastName { get; set; }

        [StringLength(200)]
        public string? Email { get; set; }

        [StringLength(20)]
        public string? MobileNo { get; set; }

        // Extra Profile Fields for Internal Users / Employees
        public int? GenderId { get; set; } // Linked with GenderMasters
        public int? PortalId { get; set; } // Linked with PortalMasters
        public string? PinCode { get; set; }


        [StringLength(250)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? State { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [StringLength(500)]
        public string? ProfileImage { get; set; }

        public bool IsSuperAdmin { get; set; } = false;
        [StringLength(100)]
        public string? PlainPassword { get; set; } 
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedOn { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        public DateTime? UpdatedOn { get; set; }
    }
}