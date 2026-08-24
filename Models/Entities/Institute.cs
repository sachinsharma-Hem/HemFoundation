using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class Institute
    {
        [Key]
        public int InstituteID { get; set; }
        public string? InstituteType { get; set; } // e.g., School
        public string? Name { get; set; } // School Name
        public string? BannerImageUrl { get; set; }
        public string? ProfileUrl { get; set; } // PhotoUrl/Logo
        public bool IsActive { get; set; } = true;
        public string? ReferralCode { get; set; }
        public bool IsVerify { get; set; } = false;
        public string? Profession { get; set; }
        public string? SocialOrgName { get; set; }
        public string? TypeOfOrganisation { get; set; }
        public string? CertificateImage { get; set; }

        // Extra mapping fields for complete registration record
        public string? AspNetUserID { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? MobileNo { get; set; }
        public string? EmailID { get; set; }
        public string? Address { get; set; }
        public string? BranchName { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public string? ReferredByAffiliateId { get; set; } // Affiliate ID dropdown selection
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}