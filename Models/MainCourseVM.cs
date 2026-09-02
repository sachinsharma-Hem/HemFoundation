namespace HemFoundation.Models
{
    public class MainCourseVM
    {
        public int MainCourseID { get; set; }
        public string? MainCourseName { get; set; }
        public int OrderNo { get; set; }
        public string? Status { get; set; } // Start, In-Progress, Completed
        public string? FeaturedImage { get; set; }
        public int MainCourseMarks { get; set; }
        // Global Header Scorecard metrics
        public int GlobalAssigned { get; set; }
        public int GlobalCompleted { get; set; }
        public int GlobalTotalMarks { get; set; }
        public int CertifiedCount { get; set; }
    }
    public class CourseDetailVM
    {
        public int CourseID { get; set; }
        public string CourseName { get; set; }
        public bool IsLocked { get; set; }
        public string LockReasonMessage { get; set; }
        public string Status { get; set; }
        public int CourseMarks { get; set; }

        // Flat rows mapped from the query
        public List<SqlRawRowVM> SqlRawRows { get; set; } = new List<SqlRawRowVM>();
    }

    public class SqlRawRowVM
    {
        public int TopicID { get; set; }
        public string SubTopicName { get; set; }
        public int? ParentTopicID { get; set; }
        public string TopicName { get; set; }
        public string Description { get; set; }
        public string TrainerVideo { get; set; }
        public int Order { get; set; }
        public string TrainerExercise { get; set; }
        public int LanguageID { get; set; }
        public int NoOfQuestion { get; set; }
        public int QuizID { get; set; }
        public string PracticalCode { get; set; }
        public int ItemScore { get; set; }
        public int PracticalID { get; set; }
        public bool IsAttempted { get; set; }
        public int ExaminationID { get; set; }
        public string Resources { get; set; }
        public int CourseID { get; set; }
        public string CourseName { get; set; }
        public string TrainerResource { get; set; }
    }
}
