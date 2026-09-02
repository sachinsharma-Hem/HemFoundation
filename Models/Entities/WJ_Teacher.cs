using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class WJ_Teacher
    {
        public WJ_Teacher()
        {
           
            WJ_TeacherAssociates = new HashSet<WJ_TeacherAssociates>();
        }

        [Key]
        public int TeacherID { get; set; }

        public string Standard { get; set; }
        public string Division { get; set; }
        public string ProfileUrl { get; set; }

        public virtual Users Users { get; set; }
       
        public virtual ICollection<WJ_TeacherAssociates> WJ_TeacherAssociates { get; set; }
    }
}