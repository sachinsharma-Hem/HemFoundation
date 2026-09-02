using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class ExaminationRecords
    {
        [Key]
        [Column("recordid")]
        public int RecordID { get; set; }

        [Column("examinationid")]
        public int? ExaminationID { get; set; }

        [Column("questionid")]
        public int? QuestionID { get; set; }

        [Column("recordstatus")]
        public string? RecordStatus { get; set; }

        [Column("answer")]
        public int? Answer { get; set; }

        [Column("iscorrect")]
        public bool? isCorrect { get; set; }

        [Column("submittiming")]
        public DateTime? SubmitTiming { get; set; }

        [Column("createdby")]
        public int? CreatedBy { get; set; }

        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }

        [Column("modifiedby")]
        public int? ModifiedBy { get; set; }

        [Column("modifieddate")]
        public DateTime? ModifiedDate { get; set; }

        [Column("orderno")]
        public int? OrderNo { get; set; }

        public virtual Examination Examination { get; set; }
        public virtual Questions Questions { get; set; }
    }
}