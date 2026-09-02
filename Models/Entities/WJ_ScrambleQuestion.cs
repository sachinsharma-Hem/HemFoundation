using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("wj_scramblequestion")]
    public partial class WJ_ScrambleQuestion
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("topicid")]
        public int TopicId { get; set; }
        [Column("scrambletype")]
        public string ScrambleType { get; set; }
        [Column("question")]
        public string Question { get; set; }
        [Column("answer")]
        public string Answer { get; set; }
        [Column("jumbledword")]
        public string JumbledWord { get; set; }
        [Column("isactive")]
        public bool IsActive { get; set; }
        [Column("createdby")]
        public int? CreatedBy { get; set; }
        [Column("createdon")]
        public DateTime? CreatedOn { get; set; }
        [Column("modifiedby")]
        public int? ModifiedBy { get; set; }
        [Column("modifiedon")]
        public DateTime? ModifiedOn { get; set; }
    }
}