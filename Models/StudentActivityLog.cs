using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("hf_view_studentactivitylog")]
    public partial class StudentActivityLog
    {
        [Column("studentid")]
        public int StudentID { get; set; }

        [Column("student_name")]
        public string? StudentName { get; set; }

        [Column("standard")]
        public string? Standard { get; set; }

        [Column("instituteid")]
        public int? InstituteID { get; set; }

        [Column("institutename")]
        public string? InstituteName { get; set; }

        [Column("maincoursename")]
        public string? MainCourseName { get; set; }

        [Column("coursename")]
        public string? CourseName { get; set; }

        [Column("topicname")]
        public string? TopicName { get; set; }

        [Column("activity_type")]
        public string? ActivityType { get; set; }

        [Column("points")]
        public string? Points { get; set; }

        [Column("activity_date")]
        public DateTime? ActivityDate { get; set; }
    }
}