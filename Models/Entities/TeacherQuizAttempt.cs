using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class TeacherQuizAttempt : BaseMasterEntity
    {
        public int TeacherId { get; set; } // Foreign key to Teachers table
        public int CourseRegistrationId { get; set; }
        public int CourseSubTopicId { get; set; }
        public int CourseMcqQuestionId { get; set; }
        [Required, StringLength(10)] public string SelectedOption { get; set; } = string.Empty; // "A", "B", "C", "D"
        public bool IsCorrect { get; set; } = false;
        public int TimeSpentSeconds { get; set; } = 0;
    }
}