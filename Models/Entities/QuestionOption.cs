using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class QuestionOption
    {
        public QuestionOption()
        {
            ExaminationOptions = new HashSet<ExaminationOptions>();
            ExaminationOptions1 = new HashSet<ExaminationOptions>();
            ExaminationRecords = new HashSet<ExaminationRecords>();
        }

        [Key]
        [Column("optionid")]
        public int OptionID { get; set; }

        [Column("questionid")]
        public int? QuestionID { get; set; }

        [Column("Options")]
        public string Options { get; set; }

        [Column("isanswer")]
        public bool? IsAnswer { get; set; }

        [Column("createdby")]
        public int? CreatedBy { get; set; }

        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }

        [Column("modifiedby")]
        public int? ModifiedBy { get; set; }

        [Column("modifieddate")]
        public DateTime? ModifiedDate { get; set; }

        [NotMapped]
        public virtual ICollection<ExaminationOptions> ExaminationOptions { get; set; }
        [NotMapped]
        public virtual ICollection<ExaminationOptions> ExaminationOptions1 { get; set; }
        [NotMapped]
        public virtual ICollection<ExaminationRecords> ExaminationRecords { get; set; }

        public virtual Questions Questions { get; set; }
    }
}