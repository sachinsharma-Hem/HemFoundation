using HemFoundation.Models.Entities;

namespace HemFoundation.Models
{
    public class QuizModel
    {
        public QuizModel()
        {
            this.QuizCourseLevels = new HashSet<QuizCourseLevels>();
            this.Courses = new HashSet<Courses>();
            this.Valueofyears = new List<valueofyear>();
            this.pracs = new List<WJ_Practical_ThumbnailView>();
           // this.MainCoursesLanguage = new List<MainCoursesLanguage>();
            this.UserTopic = new List<UserTopicModel>();
        }

        public WJ_Syllabus Quiz { get; set; }
        public List<Levels> Levels { get; set; }
        public Courses CourseOrderBy { get; set; }

        public List<valueofyear> Valueofyears { get; set; }

        public List<WJ_Practical_ThumbnailView> pracs { get; set; }
        public string CourseName1 { get; set; }
        public int LanguageID { get; set; }
        public int Gems { get; set; }
        public int InstituteId { get; set; }
        public int CertificationQuizID { get; set; }
        public string Wait { get; set; }
        public int StudentID { get; set; }
        public int CourseID { get; set; }
        public int Credit { get; set; }
        public string Free { get; set; }
        public string UseCredit { get; set; }
        public string Error { get; set; }
        public string CourseName { get; set; }
        public string Video { get; set; }
        public bool IsStarted { get; set; }
        public string IsStartMessage { get; set; }
        public int Stage { get; set; }
        public string IsStartCertification { get; set; }
        public string IsExistCertification { get; set; }
        public int LevelID { get; set; }
        public string LevelName { get; set; }
        public string IsInstituteQuiz { get; set; }
        public int Getcup1 { get; set; }
        public int Getcup2 { get; set; }
        public int Getcup3 { get; set; }
        public int StdInstituteID { get; set; }
        public int PracticalID { get; set; }
        public int QuizID { get; set; }
        public string Name { get; set; }

        public string Standard { get; set; }
        public DateTime? StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }
        public int? Duration { get; set; }
        public string Type { get; set; }
        public int? PassMarks { get; set; }
        public int? TotalMarks { get; set; }
        public int? Attempts { get; set; }
        public bool? IsActive { get; set; }

        public bool? IsPractical { get; set; }
        public bool? WhizJuniorSyllabus { get; set; }
        public string CourseAllocatedTo { get; set; }
        public string MCQLevel { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        /// <summary>
        /// Gets or sets the student certification.
        /// </summary>
        //public WJ_Value_Certification StudentCertification { get; set; }
      //  public List<MainCoursesLanguage> MainCoursesLanguage { get; set; }

        public List<UserTopicModel> UserTopic { get; set; }

        public virtual ICollection<QuizCourseLevels> QuizCourseLevels { get; set; }
        public virtual ICollection<Courses> Courses { get; set; }

        public MainCourse MainCourses { get; set; }

        public WJ_Syllabus MainCourseID { get; set; }
    }

    public class SurveyResponse
    {
        public int QuestionNumber { get; set; }
        public string QuestionText { get; set; }
        public string SelectedOption { get; set; }


    }

    public class UserTopicModel
    {
        public int TopicID { get; set; }
        public int CourseID { get; set; }
        public int? ParentTopicID { get; set; }
        public string TopicName { get; set; }
        public string Description { get; set; }
        public string TrainerVideo { get; set; }
        public string StudentVideo { get; set; }
        public string HindiVideo { get; set; }
        public string TrainerNote { get; set; }
        public string StudentNote { get; set; }
        public int? Order { get; set; }
        public bool? IsActive { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string TrainerResource { get; set; }
        public string StudentResourse { get; set; }
        public string TrainerExercise { get; set; }
        public string StudentExercise { get; set; }
        public int? LanguageID { get; set; }
        public int? NoOfQuestion { get; set; }
        public string ParentTopicName { get; set; }
        public int? SubTopics { get; set; }
        public string CourseName { get; set; }
        public int? NoOfSessions { get; set; }
        public string CourseDescription { get; set; }
        public string CourseFeaturedImage { get; set; }
        public string CourseAllocatedTo { get; set; }
        public string CourseApprovalStatus { get; set; }
        public string ParentTopicDescription { get; set; }
        public int? ParentTopicOrder { get; set; }
        public int Marks_Obtain { get; set; }
        public int TotalMarks { get; set; }
        public bool IsAttempted { get; set; }
        public int ExamID { get; set; }
    }

    public class Course
    {
        public int ID { get; set; }
        public string CourseName { get; set; }
        public List<string> Weeks { get; set; } = new List<string>();
        public List<string> CompletedWeeks { get; set; } = new List<string>();
        public string PdfSrc { get; set; }
        public string MainCourseID { get; set; }
        public string CourseValue { get; set; }
        public string Std { get; set; }
        public DateTime? CourseStartDate { get; set; }
        public int CompletionStatus { get; set; }
    }

    public class valueofyear
    {
        public string Imgurl { get; set; }
        public string Url_Function { get; set; }
        public string Std { get; set; }
        public string CompletionStatus { get; set; }
    }
}