using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("wj_practicals")]
    public partial class WJ_Practicals
    {
        public WJ_Practicals()
        {
            WJ_PracticalSubmission = new HashSet<WJ_PracticalSubmission>();
        }

        [Key]
        [Column("practicalid")]
        public int PracticalID { get; set; }

        [Column("courseid")]
        public int CourseID { get; set; }

        [Column("title")]
        public string Title { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("orderno")]
        public int? OrderNo { get; set; }

        [Column("coins")]
        public int? Coins { get; set; }

        [Column("filetype")]
        public string FileType { get; set; }

        [Column("videolink")]
        public string? VideoLink { get; set; }

        [Column("thumbnail")]
        public string Thumbnail { get; set; }

        [Column("practicallevel")]
        public int? PracticalLevel { get; set; }

        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }

        [Column("createdby")]
        public int? CreatedBy { get; set; }

        [Column("modifieddate")]
        public DateTime? ModifiedDate { get; set; }

        [Column("modifiedby")]
        public int? ModifiedBy { get; set; }

        [Column("code")]
        public string Code { get; set; }

        [Column("student_group")]
        public string Student_Group { get; set; }

        [Column("resources")]
        public string? Resources { get; set; }

        [Column("gems")]
        public int? Gems { get; set; }

        [Column("practicaltopicid")]
        public int? PracticalTopicID { get; set; }

        [Column("practicalsubtopicid")]
        public int? PracticalSubtopicID { get; set; }

        [Column("practicalstandard")]
        public string? PracticalStandard { get; set; }

        [Column("istask")]
        public bool? isTask { get; set; }

        [Column("languageid")]
        public int? languageID { get; set; }

        [Column("scrambletopicid")]
        public int? ScrambleTopicID { get; set; }

        [Column("Type")]
        public string Type { get; set; }

        //public virtual Courses Courses { get; set; }
        public virtual ICollection<WJ_PracticalSubmission> WJ_PracticalSubmission { get; set; }
    }
}