using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class Affiliate
    {
        [Key]
        public long UserID { get; set; }

        [StringLength(50)]
        public string? UserCode { get; set; }

        [StringLength(200)]
        public string? OrganisationName { get; set; } // Social Org / Corporate Name

        [StringLength(100)]
        public string? TypeOfOrganisation { get; set; } // Social Organisation / Corporate

        [StringLength(150)]
        public string? FirstName { get; set; } // Coordinator First Name

        [StringLength(150)]
        public string? LastName { get; set; } // Coordinator Last Name

        [StringLength(20)]
        public string? MobileNo { get; set; } // Acts as Username & Password

        [StringLength(100)]
        public string? EmailID { get; set; }

        [StringLength(500)]
        public string? AddressLine1 { get; set; } // Address

        [StringLength(100)]
        public string? BranchName { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? State { get; set; }

        [StringLength(10)]
        public string? Pincode { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(450)]
        public string? AspNetUserID { get; set; }

        [StringLength(100)]
        public string? RegisterFrom { get; set; }

        [StringLength(100)]
        public string? Password_ { get; set; } // Plaintext password

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        [StringLength(500)]
        public string? PhotoUrl { get; set; } // <--- Yeh line add kar dein
    }
}