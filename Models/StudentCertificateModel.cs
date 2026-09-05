using System;
using System.Collections.Generic;

namespace HemFoundation.Models
{
    public class StudentCertificateModel
    {
        public int Id { get; set; }
        public string? CertificationNo { get; set; }
        public string? Type { get; set; }
        public Nullable<int> UserID { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? StudentName { get; set; }
        public string? Standard { get; set; }
        public string? Division { get; set; }
        public string? EmailId { get; set; }
        public string? MobileNo { get; set; }
        public Nullable<int> InstituteId { get; set; }
        public string? InstituteName { get; set; }
        public string? Lavel { get; set; }
        public Nullable<int> Points { get; set; }
        public Nullable<int> Rank { get; set; }
        public Nullable<int> QuizId { get; set; }
        public Nullable<int> CourseId { get; set; }
        public string? MainCourseName { get; set; }
        public string? CourseName { get; set; }
        public string? CourseImage { get; set; }
        public string? InstituteUrl { get; set; }
        public string? CertificationTemplateUrl { get; set; }
        public Nullable<System.DateTime> ExpiredOn { get; set; }
        public Nullable<System.DateTime> GeneratedOn { get; set; }
        public Nullable<System.DateTime> IssuedOn { get; set; }
        public Nullable<bool> Status { get; set; }
        public Nullable<bool> IsCompleted { get; set; }
        public Nullable<bool> IsActive { get; set; }
        public Nullable<bool> IsAdminStatus { get; set; }
        public Nullable<System.DateTime> CreatedOn { get; set; }
        public Nullable<int> CreatedBy { get; set; }
        public Nullable<System.DateTime> ModifiedOn { get; set; }
        public Nullable<int> ModifiedBy { get; set; }
    }

    public class StudentCertificationModel
    {
        public List<StudentCertificateModel> StudentCertificateModel { get; set; }
        public Dictionary<string, string> Standards = new Dictionary<string, string>();
        public Dictionary<string, string> Institues = new Dictionary<string, string>();
        public StudentCertificateSearchModel StudentCertificateSearchModel { get; set; }
    }

    public class StudentCertificateSearchModel
    {
        public string? Standard { get; set; }
        public string? InstituteName { get; set; }
        public string? StudentName { get; set; }
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
    }
}