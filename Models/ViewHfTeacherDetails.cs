using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("view_hf_teacher_details")]
    public class ViewHfTeacherDetails
    {
        [Key]
        [Column("teacherid")]
        public int TeacherID { get; set; }

        [Column("standard")]
        [StringLength(50)]
        public string? Standard { get; set; }

        [Column("profileurl")]
        [StringLength(255)]
        public string? ProfileUrl { get; set; }

        [Column("firstname")]
        [StringLength(50)]
        public string? FirstName { get; set; }

        [Column("lastname")]
        [StringLength(50)]
        public string? LastName { get; set; }

        [Column("gender")]
        [StringLength(10)]
        public string? Gender { get; set; }

        [Column("dob")]
        public DateTime? DOB { get; set; }

        [Column("mobileno")]
        [StringLength(50)]
        public string? MobileNo { get; set; }

        [Column("emailid")]
        [StringLength(100)]
        public string? EmailID { get; set; }

        [Column("pincode")]
        [StringLength(10)]
        public string? Pincode { get; set; }

        [Column("cityname")]
        [StringLength(50)]
        public string? CityName { get; set; }

        [Column("photourl")]
        [StringLength(255)]
        public string? PhotoUrl { get; set; }

        [Column("instituteid")]
        public int? InstituteID { get; set; }

        [Column("institutename")]
        [StringLength(100)]
        public string? InstituteName { get; set; }

        [Column("isactive")]
        public bool IsActive { get; set; }

        [Column("createdby")]
        public int? CreatedBy { get; set; }

        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }

        [Column("modifiedby")]
        public int? ModifiedBy { get; set; }

        [Column("modifieddate")]
        public DateTime? ModifiedDate { get; set; }

        [Column("username")]
        [StringLength(256)]
        public string? UserName { get; set; }

        [Column("password_")]
        [StringLength(100)]
        public string? Password_ { get; set; }
    }
}