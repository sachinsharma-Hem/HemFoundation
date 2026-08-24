using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class OrganizationMaster : BaseMasterEntity
    {
        [Required, StringLength(200)]
        public string OrgName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        [StringLength(255)]
        public string? LogoUrl { get; set; } // Bill ke To: section aur logo ke liye

        public bool IsActive { get; set; } = true;
    }
}