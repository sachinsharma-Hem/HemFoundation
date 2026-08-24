using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class AffiliateVisitReport : BaseMasterEntity
    {
        public int VisitPlanId { get; set; } // Reference to AffiliateVisitPlan
        public int ActivityTypeId { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? PhotoUrl { get; set; }

        [StringLength(50)]
        public string? Latitude { get; set; }

        [StringLength(50)]
        public string? Longitude { get; set; }

        public DateTime ReportedDate { get; set; } = DateTime.UtcNow;
    }
}