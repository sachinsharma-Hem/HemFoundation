namespace HemFoundation.DTOs
{
    public class SurveyDataDto
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public int TopicId { get; set; }
        public string TopicName { get; set; } = string.Empty;
        public int SubTopicId { get; set; }

        public string Question { get; set; } = string.Empty;
        public string Option1 { get; set; } = string.Empty;
        public string Option2 { get; set; } = string.Empty;
        public string Option3 { get; set; } = string.Empty;
        public string Option4 { get; set; } = string.Empty;
        public string Option5 { get; set; } = string.Empty;

        public int LanguageId { get; set; }
    }
}