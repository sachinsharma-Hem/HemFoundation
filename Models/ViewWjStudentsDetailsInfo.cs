using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("view_wj_students_details_info")]
    public class ViewWjStudentsDetailsInfo
    {
        [Key]
        [Column("studentid")]
        public int StudentId { get; set; }

        [Column("standard")]
        [StringLength(5)]
        public string? Standard { get; set; }

        [Column("division")]
        [StringLength(50)]
        public string? Division { get; set; }

        [Column("gardianname")]
        [StringLength(50)]
        public string? GardianName { get; set; }

        [Column("referralcode")]
        [StringLength(20)]
        public string? ReferralCode { get; set; }

        [Column("registrationamount", TypeName = "numeric(18,2)")]
        public decimal RegistrationAmount { get; set; }

        [Column("profileurl")]
        [StringLength(50)]
        public string? ProfileUrl { get; set; }

        [Column("isactive")]
        public bool? IsActive { get; set; }

        [Column("ispaid")]
        public bool? IsPaid { get; set; }

        [Column("trialperiodtill")]
        public DateTime? TrialPeriodTill { get; set; }

        [Column("rollno")]
        [StringLength(20)]
        public string? RollNo { get; set; }

        [Column("sharingpermission")]
        public bool? SharingPermission { get; set; }

        [Column("levelpath")]
        [StringLength(100)]
        public string? LevelPath { get; set; }

        [Column("guardianemailid")]
        [StringLength(512)]
        public string? GuardianEmailId { get; set; }

        [Column("guardianmobileno")]
        [StringLength(256)]
        public string? GuardianMobileNo { get; set; }

        [Column("instituteid")]
        public int? InstituteID { get; set; }

        [Column("institutename")]
        [StringLength(100)]
        public string? InstituteName { get; set; }

        [Column("firstname")]
        [StringLength(50)]
        public string? FirstName { get; set; }

        [Column("lastname")]
        [StringLength(50)]
        public string? LastName { get; set; }

        [Column("cityname")]
        [StringLength(50)]
        public string? CityName { get; set; }

        [Column("coins")]
        public long? Coins { get; set; }

        [Column("rank")]
        public long? Rank { get; set; }

        [Column("gender")]
        [StringLength(1)]
        public string? Gender { get; set; }

        [Column("dob")]
        public DateTime? Dob { get; set; }

        [Column("addressline1")]
        [StringLength(250)]
        public string? AddressLine1 { get; set; }

        [Column("pincode")]
        [StringLength(6)]
        public string? Pincode { get; set; }

        [Column("mobileno")]
        [StringLength(50)]
        public string? MobileNo { get; set; }

        [Column("photourl")]
        [StringLength(500)]
        public string? PhotoUrl { get; set; }

        [Column("ismobileverified")]
        public bool? IsMobileVerified { get; set; }

        [Column("isemailverified")]
        public bool? IsEmailVerified { get; set; }

        [Column("isuserdeactivated")]
        public bool? IsUserDeactivated { get; set; }

        [Column("emailid")]
        [StringLength(50)]
        public string? EmailId { get; set; }

        [Column("registrationdate")]
        public DateTime? RegistrationDate { get; set; }

        [Column("registrationdisplaydate")]
        public string? RegistrationDisplayDate { get; set; }

        [Column("username")]
        [StringLength(256)]
        public string? Username { get; set; }

        [Column("password_")]
        [StringLength(50)]
        public string? Password { get; set; }

        [Column("gems")]
        public long? Gems { get; set; }

        [Column("credit")]
        public int? Credit { get; set; }

        [Column("logincount")]
        public int? LoginCount { get; set; }

        [Column("iswhizvalued")]
        public int? IsWhizValued { get; set; }

        [Column("certificationcount")]
        public long? CertificationCount { get; set; }
    }
}