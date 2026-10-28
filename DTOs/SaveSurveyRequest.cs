using System.ComponentModel.DataAnnotations;

namespace HemFoundation.DTOs
{
    public class SaveSurveyRequest
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue)]
        public int CourseId { get; set; }

        [Range(1, int.MaxValue)]
        public int TopicId { get; set; }

        [Required]
        [StringLength(100)]
        public string TopicName { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int SubTopicId { get; set; }

        [Required]
        public string Question { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Option1 { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Option2 { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Option3 { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Option4 { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Option5 { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int LanguageId { get; set; }
    }
}