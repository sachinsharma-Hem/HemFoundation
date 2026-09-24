using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("ProjectEvaluations")]
    public class ProjectEvaluation
    {
        [Key]
        [Column("EvaluationID")]
        public int EvaluationID { get; set; }

        [Column("ProjectID")]
        public int ProjectID { get; set; }

        [Column("ProjectType")]
        public string? ProjectType { get; set; }

        [Column("StudentID")]
        public int? StudentID { get; set; }

        [Column("InstituteID")]
        public int? InstituteID { get; set; }

        [Column("CourseName")]
        public string? CourseName { get; set; }

        [Column("StatusColor")]
        public string? StatusColor { get; set; }

        [Column("Marks")]
        public int Marks { get; set; }

        [Column("Description")]
        public string? Description { get; set; }

        [Column("EvaluatedByUserId")]
        public string? EvaluatedByUserId { get; set; }

        [Column("EvaluatedDate")]
        public DateTime EvaluatedDate { get; set; } = DateTime.UtcNow;
    }
}