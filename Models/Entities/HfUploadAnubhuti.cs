using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("hf_uploadanubhuti")]
    public class HfUploadAnubhuti
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("courseid")]
        public int? CourseId { get; set; }
        [Column("practicalid")]
        public int? PracticalId { get; set; }
        [Column("uploadedfiles")]
        public string? UploadedFiles { get; set; }
        [Column("iscreateddate")]
        public DateTime? IsCreatedDate { get; set; }
        [Column("iscreatedby")]
        public int? iscreatedby { get; set; }
        [Column("filename")]
        public string? FileName { get; set; }
        [Column("filelink")]
        public string? FileLink { get; set; }
        [Column("status")]
        public string? Status { get; set; }
        [Column("firstname")]
        public string? FirstName { get; set; }
        [Column("lastname")]
        public string? LastName { get; set; }
        [Column("coursename")]
        public string? CourseName { get; set; }
        [Column("title")]
        public string? Title { get; set; }
    }

    [Table("hf_tbl_uploadindividualproject")]
    public class HfTblUploadIndividualProject
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("maincourseid")]
        public int? MainCourseId { get; set; }
        [Column("practicalid")]
        public int? PracticalId { get; set; }
        [Column("iscreateddate")]
        public DateTime? IsCreatedDate { get; set; }
        [Column("filename")]
        public string? FileName { get; set; }
        [Column("filelink")]
        public string? FileLink { get; set; }
        [Column("status")]
        public string? Status { get; set; }
        [Column("firstname")]
        public string? FirstName { get; set; }
        [Column("lastname")]
        public string? LastName { get; set; }
        [Column("coursename")]
        public string? CourseName { get; set; }
        [Column("title")]
        public string? Title { get; set; }
        [Column("student_id")]
        public int? Student_Id { get; set; }
    }
}