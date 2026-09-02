using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class WJ_PracticalComments
    {
        [Key]
        public int PracticalCommentID { get; set; }

        public int? SubmissionID { get; set; }
        public int? UserID { get; set; }
        public string Comments { get; set; }
        public DateTime? CreatedOn { get; set; }
        public int? CreatedBy { get; set; }

        public virtual WJ_PracticalSubmission WJ_PracticalSubmission { get; set; }
    }
}