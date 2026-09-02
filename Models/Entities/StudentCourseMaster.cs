using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class StudentCourseMaster
    {
        [Key] public int Id { get; set; }
        public int StandardId { get; set; } // Mapped to StandardMaster
        public string CourseName { get; set; } = string.Empty; // e.g., "Dutifulness", "Willpower"
        public string ThemeColor { get; set; } = "primary"; // primary, success, warning etc. for UI
        public int TotalPoints { get; set; } = 0;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    public class StudentCourseModule
    {
        [Key] public int Id { get; set; }
        public int CourseId { get; set; } // Mapped to StudentCourseMaster
        public string ModuleName { get; set; } = string.Empty; // e.g., "Dutifulness - Week 1"
        public DateTime UnlockDate { get; set; } // Date jab yeh unlock hoga
        public int Points { get; set; } = 0;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}