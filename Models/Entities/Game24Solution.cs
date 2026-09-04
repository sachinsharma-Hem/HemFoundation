using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models
{
    [Table("game24solution")]
    public class Game24Solution
    {
        [Key]
        [Column("sid")] // Updated to match the actual database column name "sid"
        public int SolutionID { get; set; }

        [Column("game24id")]
        public int Game24ID { get; set; }

        [Column("entrydate")]
        public DateTime EntryDate { get; set; }

        [Column("solutionanswers")]
        public string SolutionAnswers { get; set; }

        [Column("iscreatedby")]
        public int IsCreatedBy { get; set; }

        [Column("courseid")]
        public int CourseID { get; set; }
    }
}