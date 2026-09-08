using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("wj_value_certification")]
    public partial class ValueCertification
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("certificationno")]
        public string? CertificationNo { get; set; }

        [Column("Type")]
        public string? Type { get; set; }

        [Column("userid")]
        public int UserId { get; set; }

        [Column("firstname")]
        public string? FirstName { get; set; }

        [Column("lastname")]
        public string? LastName { get; set; }

        [Column("standard")]
        public string? Standard { get; set; }

        [Column("division")]
        public string? Division { get; set; }

        [Column("emailid")]
        public string? EmailID { get; set; }

        [Column("mobileno")]
        public string? MobileNo { get; set; }

        [Column("instituteid")]
        public int? InstituteID { get; set; }

        [Column("institutename")]
        public string? InstituteName { get; set; }

        [Column("lavel")]
        public string? Lavel { get; set; }

        [Column("points")]
        public int? Points { get; set; }

        [Column("Rank")]
        public int? Rank { get; set; }

        [Column("quizid")]
        public int? QuizID { get; set; }

        [Column("courseid")]
        public int? CourseID { get; set; }

        [Column("coursename")]
        public string? CourseName { get; set; }

        [Column("expiredon")]
        public DateTime? ExpiredOn { get; set; }

        [Column("generatedon")]
        public DateTime? GeneratedOn { get; set; }

        [Column("issuedon")]
        public DateTime? IssuedOn { get; set; }

        [Column("status", TypeName = "bit")]
        public System.Collections.BitArray? Status { get; set; }

        [Column("iscompleted", TypeName = "bit")]
        public System.Collections.BitArray? IsCompleted { get; set; }

        [Column("isactive", TypeName = "bit")]
        public System.Collections.BitArray? IsActive { get; set; }

        [Column("isadminstatus", TypeName = "bit")]
        public System.Collections.BitArray? IsAdminStatus { get; set; }

        [Column("createdon")]
        public DateTime? CreatedOn { get; set; }

        [Column("createdby")]
        public int? CreatedBy { get; set; }

        [Column("modifiedon")]
        public DateTime? ModifiedOn { get; set; }

        [Column("modifiedby")]
        public int? ModifiedBy { get; set; }

        [Column("certificateno")]
        public string? CertificateNo { get; set; }
    }
}