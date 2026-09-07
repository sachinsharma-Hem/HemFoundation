using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("vw_valueitemmasters")] // Maps directly to the PostgreSQL database view or table
    public class vw_valueitemmasters
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("grade")]
        public string? GradeId { get; set; } // FK to ValueGradeMaster

        [Column("capacityname")]
        public string CapacityName { get; set; } = string.Empty; // Cognitive, Social, etc.

        [Column("valuetitleeng")]
        public string ValueTitleEng { get; set; } = string.Empty; // e.g., Curiosity

        [Column("valuetitlehin")]
        public string ValueTitleHin { get; set; } = string.Empty; // e.g., Jigyassa

        [Column("valueimageurl")]
        public string? ValueImageUrl { get; set; }

        [Column("activitypdfurl")]
        public string? ActivityPdfUrl { get; set; }

        //[Column("isactive")]
        //public bool IsActive { get; set; } = true;

        //[Column("createddate")]
        //public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}