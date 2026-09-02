using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{

    [Table("levels")]
    public partial class Levels
    {
        public Levels()
        {
            //Examination = new HashSet<Examination>();
            //QuizCourseLevels = new HashSet<QuizCourseLevels>();
        }

        [Key]
        [Column("levelid")]
        public int LevelID { get; set; }

        [Column("levels")] // Maps back to your original database column name if it differs from the property name
        public string Levels1 { get; set; }

        //public virtual ICollection<Examination> Examination { get; set; }

        //[NotMapped]
        //public virtual ICollection<QuizCourseLevels> QuizCourseLevels { get; set; }
    }
}