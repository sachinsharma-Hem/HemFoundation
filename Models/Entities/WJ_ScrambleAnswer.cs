using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("wj_scrambleanswer")]
    public partial class WJ_ScrambleAnswer
    {
        [Key]
        [Column("ansid")]
        public int AnsId { get; set; }
        [Column("questionid")]
        public int? QuestionId { get; set; }
        [Column("scrambletopicid")]
        public int? ScrambleTopicId { get; set; }
        [Column("answer")]
        public string Answer { get; set; }
        [Column("userid")]
        public int? UserId { get; set; }
        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }
    }
}