using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("wj_students")]
    public partial class WJ_Students
    {
        public WJ_Students()
        {
            WJ_PracticalSubmission = new HashSet<WJ_PracticalSubmission>();
            WJ_StudentAssociates = new HashSet<WJ_StudentAssociates>();
        }

        [Key]
        [Column("studentid")]
        [ForeignKey("Users")]
        public int StudentID { get; set; }

        [Column("standard")]
        public string Standard { get; set; }

        [Column("division")]
        public string Division { get; set; }

        [Column("gardianname")]
        public string GardianName { get; set; }

        [Column("referralcode")]
        public string ReferralCode { get; set; }

        [Column("registrationamount")]
        public decimal RegistrationAmount { get; set; }

        [Column("profileurl")]
        public string ProfileUrl { get; set; }

        [Column("isactive")]
        public bool? IsActive { get; set; }

        [Column("ispaid")]
        public bool IsPaid { get; set; }

        [Column("trialperiodtill")]
        public DateTime? TrialPeriodTill { get; set; }

        [Column("rollno")]
        public string RollNo { get; set; }

        [Column("sharingpermission")]
        public bool? SharingPermission { get; set; }

        [Column("levelpath")]
        public string LevelPath { get; set; }

        [Column("guardianemailid")]
        public string GuardianEmailID { get; set; }

        [Column("guardianmobileno")]
        public string GuardianMobileNo { get; set; }

        public virtual Users Users { get; set; }

        public virtual ICollection<WJ_PracticalSubmission> WJ_PracticalSubmission { get; set; }
        public virtual ICollection<WJ_StudentAssociates> WJ_StudentAssociates { get; set; }
    }
}