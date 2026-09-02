using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class Topics
    {
        public Topics()
        {
            Questions = new HashSet<Questions>();
        }

        [Key]
        [Column("topicid")]
        public int TopicID { get; set; }

        [Column("courseid")]
        public int CourseID { get; set; }

        [Column("parenttopicid")]
        public int? ParentTopicID { get; set; }

        [Column("topicname")]
        public string TopicName { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("trainervideo")]
        public string TrainerVideo { get; set; }

        [Column("studentvideo")]
        public string StudentVideo { get; set; }

        [Column("hindivideo")]
        public string HindiVideo { get; set; }

        [Column("trainernote")]
        public string TrainerNote { get; set; }

        [Column("studentnote")]
        public string StudentNote { get; set; }

        [Column("order")]
        public int? Order { get; set; }

        [Column("isactive")]
        public bool? IsActive { get; set; }

        [Column("createdby")]
        public int CreatedBy { get; set; }

        [Column("createddate")]
        public DateTime CreatedDate { get; set; }

        [Column("modifiedby")]
        public int? ModifiedBy { get; set; }

        [Column("modifieddate")]
        public DateTime? ModifiedDate { get; set; }

        [Column("trainerresource")]
        public string TrainerResource { get; set; }

        [Column("studentresource")] // Matches your lowercase database column
        public string StudentResourse { get; set; }

        [Column("trainerexercise")]
        public string TrainerExercise { get; set; }

        [Column("studentexercise")]
        public string StudentExercise { get; set; }

        [Column("languageid")]
        public int? languageID { get; set; }

        [Column("noofquestion")]
        public int? NoOfQuestion { get; set; }

        public virtual Courses Courses { get; set; }
        public virtual ICollection<Questions> Questions { get; set; }
    }
}