using DocumentFormat.OpenXml.Spreadsheet;
using HemFoundation.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class Questions
    {
        public Questions()
        {
            ExaminationRecords = new HashSet<ExaminationRecords>();
            QuestionOption = new HashSet<QuestionOption>();
        }

        [Key]
        [Column("questionid")]
        public int QuestionID { get; set; }

        [Column("questionname")]
        public string QuestionName { get; set; }

        [Column("marks")]
        public int? Marks { get; set; }

        [Column("questiontype")]
        public string Type { get; set; }

        [Column("status")]
        public bool Status { get; set; }

        [Column("createdby")]
        public int? CreatedBy { get; set; }

        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }

        [Column("modifiedby")]
        public int? ModifiedBy { get; set; }

        [Column("modifieddate")]
        public DateTime? ModifiedDate { get; set; }

        [Column("complexitylevel")]
        public string ComplexityLevel { get; set; }

        [Column("answerdescription")] // Change this to your table's true lowercase column name
        public string AnswerDescription { get; set; }// Ensure this matches your exact database column name for answer description
        

        [Column("courseid")]
        public int? CourseID { get; set; }

        [Column("topicid")]
        public int? TopicID { get; set; }

        [Column("subtopicid")]
        public int? SubTopicID { get; set; }

        [Column("questionlevel")]
        public int? QuestionLevel { get; set; }

        [Column("isadminstatus")]
        public bool? isAdminStatus { get; set; }

        [Column("isdeleted")]
        public bool? isDeleted { get; set; }

        [Column("languageid")]
        public int? languageID { get; set; }

        public virtual Courses Courses { get; set; }
        public virtual Courses Courses1 { get; set; }
        public virtual ICollection<ExaminationRecords> ExaminationRecords { get; set; }
        public virtual ICollection<QuestionOption> QuestionOption { get; set; }
        public virtual Topics Topics { get; set; }
    }
}