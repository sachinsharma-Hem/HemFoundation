using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace HemFoundation.Models.Entities
{
    public class VolunteerVisitPlan : BaseMasterEntity
    {
        public int VolunteerId { get; set; }

        public bool IsOtherActivity { get; set; } = false;

        public int? InstituteId { get; set; }

        [StringLength(200)]
        public string? SchoolName { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(10)]
        public string? Pincode { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? State { get; set; }

        [Required]
        public DateTime ScheduleDate { get; set; }
        [Column("visitdate")]
        public DateTime? visitdate { get; set; }
        [Required, StringLength(500)]
        public string Purpose { get; set; } = string.Empty;

        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        // --- Planning Time Location ---
        [StringLength(50)]
        public string? PlanLatitude { get; set; }

        [StringLength(50)]
        public string? PlanLongitude { get; set; }

        // --- Report / Update Time Location ---
        [StringLength(1000)]
        public string? Remarks { get; set; }

        [StringLength(500)]
        public string? ImagePath { get; set; }

        [StringLength(50)]
        public string? ReportLatitude { get; set; }

        [StringLength(50)]
        public string? ReportLongitude { get; set; }

        public DateTime? ReportedDate { get; set; }
    }
}