using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class ExaminationOptions
    {
        [Key]
        public int ID { get; set; }

        public int ExaminationID { get; set; }
        public int OptionID { get; set; }
        public int? DisplayOrder { get; set; }

        public virtual Examination Examination { get; set; }

        [NotMapped]
        public virtual QuestionOption QuestionOption { get; set; }

        [NotMapped]
        public virtual QuestionOption QuestionOption1 { get; set; }
    }
}