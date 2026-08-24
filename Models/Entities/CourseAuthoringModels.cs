using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    // 1. Topic Master
    public class CourseTopic : BaseMasterEntity
    {
        public int CourseRegistrationId { get; set; } // Selected Course ID
        [Required, StringLength(200)] public string TopicName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrderId { get; set; } // From CourseOrderNumber
        [Required, StringLength(50)] public string Language { get; set; } = "English"; // English / Hindi
    }

    // 2. Sub-Topic Master
    public class CourseSubTopic : BaseMasterEntity
    {
        public int CourseRegistrationId { get; set; }
        public int CourseTopicId { get; set; } // Linked Topic ID
        [Required, StringLength(200)] public string SubTopicName { get; set; } = string.Empty;
        public int DisplayOrderId { get; set; }
        public string? VideoLink { get; set; }
        public int NumberOfQuestions { get; set; }
        public string? McqThumbnail { get; set; }
        public string? VideoThumbnail { get; set; }
    }

    // 3. MCQ Master
    public class CourseMcqQuestion : BaseMasterEntity
    {
        public int CourseRegistrationId { get; set; }
        public int CourseTopicId { get; set; }
        public int CourseSubTopicId { get; set; }
        public int CourseMcqLevelId { get; set; } // Complexity Level ID
        [Required] public string Question { get; set; } = string.Empty;

        [Required] public string OptionA { get; set; } = string.Empty;
        [Required] public string OptionB { get; set; } = string.Empty;
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }

        [Required, StringLength(10)] public string RightAnswer { get; set; } = "A"; // "A", "B", "C", or "D"
    }

    // 4. Practical Lab Master
    public class CoursePracticalLab : BaseMasterEntity
    {
        public int CourseRegistrationId { get; set; }
        [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
        public string? ThumbnailImageUrl { get; set; }
        public string? Description { get; set; }
        public int Coins { get; set; }
        public int Gems { get; set; }
        public string? ResourceFileUrl { get; set; }
        [Required, StringLength(200)] public string PracticalTopic { get; set; } = string.Empty;
        public int StandardMasterId { get; set; }
        public int DisplayOrderId { get; set; }
        public string? VideoLink { get; set; }
    }
}