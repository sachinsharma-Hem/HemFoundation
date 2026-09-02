using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class WJ_Syllabus
    {
        [Key]
        public int SyllabusID { get; set; }

        public string CompetitionYear { get; set; }
        public string Standard_ { get; set; }
        public int? CourseID { get; set; }
        public int? QuizID { get; set; }
        public int? InstituteID { get; set; }
        public int? CategoryId { get; set; }
        public int? SubCategoryId { get; set; }
        public int? OrderNo { get; set; }
        public int? MainCourseID { get; set; }

        public virtual WJ_Institutes WJ_Institutes { get; set; }
    }
}