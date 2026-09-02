using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class WJ_Students
    {
        public WJ_Students()
        {
           
            WJ_PracticalSubmission = new HashSet<WJ_PracticalSubmission>();
            WJ_StudentAssociates = new HashSet<WJ_StudentAssociates>();
           
        }

        [Key]
        public int StudentID { get; set; }

        public string Standard { get; set; }
        public string Division { get; set; }
        public string GardianName { get; set; }
        public string ReferralCode { get; set; }
        public decimal RegistrationAmount { get; set; }
        public string ProfileUrl { get; set; }
        public bool? IsActive { get; set; }
        public bool IsPaid { get; set; }
        public DateTime? TrialPeriodTill { get; set; }
        public string RollNo { get; set; }
        public bool? SharingPermission { get; set; }
        public string LevelPath { get; set; }
        public string GuardianEmailID { get; set; }
        public string GuardianMobileNo { get; set; }

        public virtual Users Users { get; set; }
   
        public virtual ICollection<WJ_PracticalSubmission> WJ_PracticalSubmission { get; set; }
        public virtual ICollection<WJ_StudentAssociates> WJ_StudentAssociates { get; set; }
        
    }
}