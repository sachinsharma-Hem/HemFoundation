using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Models.Entities
{
    [Keyless]
    public partial class View_ExamResult
    {
        public long RoWNo { get; set; }
        public int? RecordID { get; set; }
        public int? ExaminationID { get; set; }
        public string Question { get; set; }
        public string YourAnswer { get; set; }
        public string CorrectAnswer { get; set; }
    }
}