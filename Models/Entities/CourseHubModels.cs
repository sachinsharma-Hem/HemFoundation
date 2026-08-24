using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    // 1. Syllabus Master
    public class CourseSyllabus : BaseMasterEntity
    {
        public int CourseYearId { get; set; }
        public int MainCourseId { get; set; }
        public int CourseCategoryId { get; set; }
        public int CourseSubCategoryId { get; set; }
        public int DisplayOrderId { get; set; }

        // Target Selection (Standard, Teacher Checkbox, or All Checkbox)
        public int? StandardMasterId { get; set; }
        public bool IsForTeachers { get; set; } = false;
        public bool IsForAll { get; set; } = false;

        public int CourseId { get; set; } // Linked with Course Registration
        public int QuizId { get; set; }   // Linked with Quiz Creation
        public string? Remarks { get; set; }
    }

    // 2. Course Registration Master
    public class CourseRegistration : BaseMasterEntity
    {
        [Required, StringLength(200)] public string CourseName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime ExpectedDeliveryDate { get; set; }
        public int FromAge { get; set; }
        public int ToAge { get; set; }
        public int NumberOfSessions { get; set; }

        public int CourseAuthorId { get; set; }
        public int CourseManagerId { get; set; }
        public int MainCourseId { get; set; }
        public int CourseForId { get; set; }
        public int CourseCustomerTypeId { get; set; }
    }

    // 3. Quiz Creation Master
    public class CourseQuiz : BaseMasterEntity
    {
        [Required, StringLength(200)] public string QuizName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public int CourseDurationTypeId { get; set; }
        public int PassingMarks { get; set; }
        public int TotalMarks { get; set; }
        public int NoOfAttemptsAllowed { get; set; }
        public bool ExamActive { get; set; } = true;

        public int CourseId { get; set; } // Linked Course Dropdown
        public int CourseAllotedToId { get; set; }
        public int CourseMcqLevelId { get; set; }
        public int CourseLevelId { get; set; }
    }
}