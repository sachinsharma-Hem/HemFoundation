using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocumentFormat.OpenXml.Spreadsheet;

namespace HemFoundation.Models.Entities
{
    [Table("wj_teacher")]
    public partial class WJ_Teacher
    {
        public WJ_Teacher()
        {
            WJ_TeacherAssociates = new HashSet<WJ_TeacherAssociates>();
        }

        [Key]
        [Column("teacherid")]
        [ForeignKey("Users")]
        public int TeacherID { get; set; }

        [Column("standard")]
        public string Standard { get; set; }

        [Column("division")]
        public string Division { get; set; }

        [Column("profileurl")]
        public string ProfileUrl { get; set; }

       
        public virtual Users Users { get; set; }

        public virtual ICollection<WJ_TeacherAssociates> WJ_TeacherAssociates { get; set; }
    }
}