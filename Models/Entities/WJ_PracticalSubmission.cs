using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("wj_practicalsubmission")]
    public partial class WJ_PracticalSubmission
    {
        
        public WJ_PracticalSubmission()
        {
            WJ_PracticalComments = new HashSet<WJ_PracticalComments>();
            //WJ_SummerPracticalSubmission = new HashSet<WJ_SummerPracticalSubmission>();
            //WJ_SummerPracticalSubmission1 = new HashSet<WJ_SummerPracticalSubmission>();
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("submissionid")]
        public int SubmissionID { get; set; }

        [Column("studentid")]
        public int StudentID { get; set; }
        [Column("practicalid")]
        public int PracticalID { get; set; }
        [Column("filename")]
        public string FileName { get; set; }
        [Column("fileextension")]
        public string FileExtension { get; set; }
        [Column("filelink")]
        public string FileLink { get; set; }
        [Column("status")]
        public string Status { get; set; }
        [Column("remarks")]
        public string Remarks { get; set; }
        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }
        [Column("isdelete")]
        public bool? IsDelete { get; set; }
        [Column("isdeletedate")]
        public DateTime? IsDeleteDate { get; set; }
        [Column("creativity")]
        public int Creativity { get; set; }
        [Column("process")]
        public int Process { get; set; }
        [Column("applicability")]
        public int Applicability { get; set; }

        public virtual ICollection<WJ_PracticalComments> WJ_PracticalComments { get; set; }

        [ForeignKey("PracticalID")]
        public virtual WJ_Practicals WJ_Practicals { get; set; }

        [ForeignKey("StudentID")]
        public virtual WJ_Students WJ_Students { get; set; }
        //public virtual ICollection<WJ_SummerPracticalSubmission> WJ_SummerPracticalSubmission { get; set; }
        //public virtual ICollection<WJ_SummerPracticalSubmission> WJ_SummerPracticalSubmission1 { get; set; }
    }
}