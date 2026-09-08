using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities // Match your actual namespace
{
    [Table("maincourses")] // Matches your PostgreSQL table name exactly
    public partial class MainCourseSetup
    {
        [Key]
        [Column("maincourseid")]
        public int MainCourseID { get; set; }

        [Column("maincoursename")]
        public string? MainCourseName { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("isactive", TypeName = "bit")]
        public System.Collections.BitArray? IsActive { get; set; } // Or bool? if using value converters

        [Column("createdby")]
        public int? CreatedBy { get; set; }

        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }

        [Column("modifieddate")]
        public DateTime? ModifiedDate { get; set; }

        [Column("modifiedby")]
        public int? ModifiedBy { get; set; }

        [Column("maincoursetemplateurl")]
        public string? MainCourseTemplateUrl { get; set; }

        [Column("maincourseorderno")]
        public int? MainCourseOrderNo { get; set; }
    }
}