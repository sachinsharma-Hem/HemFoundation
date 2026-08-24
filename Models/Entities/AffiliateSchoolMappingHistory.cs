using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class AffiliateSchoolMappingHistory : BaseMasterEntity
    {
        public int InstituteID { get; set; }

        public long? PreviousAffiliateId { get; set; }
        public string? PreviousAffiliateName { get; set; }

        public long NewAffiliateId { get; set; }
        public string? NewAffiliateName { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.UtcNow;
        public string? AssignedBy { get; set; } = "Admin";
    }
}