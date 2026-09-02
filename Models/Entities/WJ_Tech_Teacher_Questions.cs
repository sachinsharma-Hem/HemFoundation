using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class WJ_Tech_Teacher_Questions
    {
        [Key]
        public int ID { get; set; }

        public int? TeacherID { get; set; }
        public int? QuestionID { get; set; }
        public DateTime? createdDate { get; set; }
        public string Imagepath { get; set; }
        public bool? status { get; set; }
        public bool? isApp { get; set; }
    }
}