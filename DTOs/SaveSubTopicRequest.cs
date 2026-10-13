namespace HemFoundation.DTOs
{
    public class SaveSubTopicRequest
    {
        public int Id { get; set; }
        public int CourseRegistrationId { get; set; }
        public int CourseTopicId { get; set; }
        public string SubTopicName { get; set; } = string.Empty;
        public int DisplayOrderId { get; set; }
        public string? VideoLink { get; set; }
        public int? LanguageID { get; set; }
        public int NumberOfQuestions { get; set; }
        public IFormFile? McqThumbFile { get; set; }
        public IFormFile? VideoThumbFile { get; set; }
    }
    public class SaveMcqRequest
    {
        public int QuestionId { get; set; }
        public int CourseId { get; set; }
        public int TopicId { get; set; }
        public int SubTopicId { get; set; }
        public string QuestionName { get; set; } = string.Empty;
        public string? QuestionType { get; set; }
        public string? EditorType { get; set; }
        public string? ComplexityLevel { get; set; }
        public int? LanguageId { get; set; }
        public string? AnswerDescription { get; set; }
        public List<McqOptionRequest> Options { get; set; } = new();
    }

    public class McqOptionRequest
    {
        public string OptionText { get; set; } = string.Empty;
        public bool IsAnswer { get; set; }
    }
}
