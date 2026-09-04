using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{

    [Table("View_WJ_Course_Topics", Schema = "public")]
    public partial class View_WJ_Course_Topics
    {
        public int TopicID { get; set; }

        [Column("courseid")]
        public int CourseID { get; set; }
        public string CourseName { get; set; }
        public string ParentTopicName { get; set; }
        public string TopicName { get; set; }
        public string ParentTopicDescription { get; set; }
        public string VideoLink { get; set; }
        public Nullable<int> ParentTopicOrder { get; set; }
        public Nullable<int> Order { get; set; }
    }
}
