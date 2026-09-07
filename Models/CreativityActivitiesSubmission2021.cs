using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("creativityactivitessubmission2021")]
    public partial class CreativityActivitiesSubmission2021
    {
        [Key]
        [Column("submissionid")]
        public int SubmissionID { get; set; }

        [Column("instituteid")]
        public int? InstituteID { get; set; }

        [Column("teammemberslist")]
        public string? TeamMembersList { get; set; }

        [Column("imageactivity")]
        public string? ImageActivity { get; set; }

        [Column("videoactivity")]
        public string? VideoActivity { get; set; }

        [Column("shortdescription")]
        public string? ShortDescription { get; set; }

        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }

        [Column("modifieddate")]
        public DateTime? ModifiedDate { get; set; } // Fixed typo if needed, or keep standard

        [Column("maincourseid")]
        public int? MainCourseID { get; set; }

        [Column("activitytype")]
        public string? ActivityType { get; set; }
    }
}