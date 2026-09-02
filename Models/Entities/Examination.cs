using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("examination")]
    public partial class Examination
    {
        public Examination()
        {
            ExaminationOptions = new HashSet<ExaminationOptions>();
            ExaminationRecords = new HashSet<ExaminationRecords>();
        }

        [Key]
        [Column("examinationid")]
        public int ExaminationID { get; set; }

        [Column("userid")]
        public int UserID { get; set; }

        [Column("starttime")]
        public DateTime? StartTime { get; set; }

        [Column("endtime")]
        public DateTime? EndTime { get; set; }

        [Column("quizid")]
        public int QuizID { get; set; }

        [Column("levelid")]
        public int? LevelID { get; set; }

        [Column("marksobtain")]
        public int? MarksObtain { get; set; }

        [Column("createdby")]
        public int? CreatedBy { get; set; }

        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }

        [Column("modifiedby")]
        public int? ModifiedBy { get; set; }

        [Column("modifieddate")]
        public DateTime? ModifiedDate { get; set; }

        [Column("ispassed")]
        public bool IsPassed { get; set; }

        [Column("coinsearned")]
        public int? CoinsEarned { get; set; }

        [Column("examtopicid")]
        public int? ExamTopicID { get; set; }

        public virtual ICollection<ExaminationOptions> ExaminationOptions { get; set; }
        public virtual ICollection<ExaminationRecords> ExaminationRecords { get; set; }
    }
}