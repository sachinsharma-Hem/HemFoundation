using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class Quiz
    {
        public Quiz()
        {
           // QuizCourseLevels = new HashSet<QuizCourseLevels>();
            // Courses = new HashSet<Courses>();
        }

        [Key]
        [Column("quizid")]
        public int QuizID { get; set; }

        [Column("Name")]
        public string Name { get; set; }

        [Column("startdatetime")]
        public DateTime? StartDateTime { get; set; }

        [Column("enddatetime")]
        public DateTime? EndDateTime { get; set; }

        [Column("duration")]
        public int? Duration { get; set; }

        [Column("Type")]
        public string Type { get; set; }

        [Column("passmarks")]
        public int? PassMarks { get; set; }

        [Column("totalmarks")]
        public int? TotalMarks { get; set; }

        [Column("attempts")]
        public int? Attempts { get; set; }

        [Column("isactive")]
        public bool? IsActive { get; set; }

        [Column("whizjuniorsyllabus")]
        public bool? WhizJuniorSyllabus { get; set; }

        [Column("courseallocatedto")]
        public string? CourseAllocatedTo { get; set; }

        [Column("mcqlevel")]
        public string? MCQLevel { get; set; }

        [Column("createdby")]
        public int? CreatedBy { get; set; }

        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }

        [Column("modifiedby")]
        public int? ModifiedBy { get; set; }

        [Column("modifieddate")]
        public DateTime? ModifiedDate { get; set; }

        //[NotMapped]
        //public virtual ICollection<QuizCourseLevels> QuizCourseLevels { get; set; }

        //[NotMapped]
        //public virtual ICollection<Courses> Courses { get; set; }
    }
}