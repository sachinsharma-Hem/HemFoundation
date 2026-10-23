using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("wj_syllabus")]
    public partial class Syllabus
    {
        [Key]
        [Column("syllabusid")]
        public int SyllabusId { get; set; }

        [Column("competitionyear")]
        public string? CompetitionYear { get; set; }

        [Column("standard_")]
        public string? Standard { get; set; }

        [Column("courseid")]
        public int CourseId { get; set; }

        [Column("quizid")]
        public int QuizId { get; set; }

        [Column("instituteid")]
        public int? InstituteId { get; set; }

        [Column("categoryid")]
        public int CategoryId { get; set; }

        [Column("subcategoryid")]
        public int SubCategoryId { get; set; }

        [Column("orderno")]
        public int? OrderNo { get; set; }

        [Column("maincourseid")]
        public int? MainCourseId { get; set; }
    }
}