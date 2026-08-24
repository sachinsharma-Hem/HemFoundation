using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class AffiliateVisitPlan : BaseMasterEntity
    {
        public long AffiliateId { get; set; }
        public int InstituteID { get; set; } // Kis school me visit hai

        [Required]
        public DateTime ScheduleDate { get; set; }

        [StringLength(500)]
        public string? Purpose { get; set; } // Visit ka main goal

        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Active, Cancelled, Completed

        [StringLength(1000)]
        public string? Remarks { get; set; } // Agar cancel ya complete kiya to reason/notes
    }
}