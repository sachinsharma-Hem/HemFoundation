using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HemFoundation.Common; // Assuming you have extension methods for date formatting

namespace HemFoundation.Models
{
    [Serializable]
    public class QuestionViewModel
    {
        public QuestionViewModel()
        {
            QuestionOption = new List<QuestionOptionViewModel>();
        }

        public int? QuestionID { get; set; }

        // In ASP.NET Core, request validation handles HTML strings automatically or via encoder tools
        public string QuestionName { get; set; }
        public int? Marks { get; set; }
        public string Type { get; set; }
        public string ComplexityLevel { get; set; }

        public string AnswerDescription { get; set; }
        public int? CourseID { get; set; }
        public int? TopicID { get; set; }
        public List<QuestionOptionViewModel> QuestionOption { get; set; }
    }

    public class QuestionOptionViewModel
    {
        public int? OptionID { get; set; }
        public string Options { get; set; }
        public bool IsAnswer { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ExamResultViewModel
    {
        public ExamResultViewModel()
        {
            examList = new List<ExamViewModel>();
        }
        public int ExamID { get; set; }
        public string QuizName { get; set; }
        public int UserID { get; set; }
        public int QuizID { get; set; }
        public int LevelID { get; set; }

        public List<int> CourseID { get; set; }

        public string PassStatus { get; set; }
        public int TotalMarks { get; set; }
        public int PassMarks { get; set; }
        public int MarksObtain { get; set; }
        public int percentage { get; set; }
        public int TotalAttempt { get; set; }

        public DateTime AppearedDate { get; set; }

        public string DOBDisplay
        {
            get { return AppearedDate.ConvertDateToString(); }
        }

        public List<ExamViewModel> examList { get; set; }
    }

    public class ExamViewModel
    {
        [Display(Name = "Question")]
        public string QuestionName { get; set; }

        [Display(Name = "Your Answer")]
        public string YourAnswer { get; set; }

        [Display(Name = "Right Answer")]
        public string RightAnswer { get; set; }
    }

    public class ResultCourseDetailsVM
    {
        public string CourseName { get; set; }

        public int CourseTotalCount { get; set; }

        public int CourseObtainMarks { get; set; }

        public int Percentage
        {
            get
            {
                return CourseTotalCount > 0 ? (CourseObtainMarks * 100) / CourseTotalCount : 0;
            }
        }
    }

    [Serializable]
    public class ExamReviewVM
    {
        public ExamReviewVM()
        {
            Questions = new List<QuestionViewModel>();
            ExaminationRecords = new List<ExaminationRecordVM>();
        }
        public List<QuestionViewModel> Questions { get; set; }
        public List<ExaminationRecordVM> ExaminationRecords { get; set; }
    }

    [Serializable]
    public class ExaminationRecordVM
    {
        public int RecordID { get; set; }
        public int? ExaminationID { get; set; }
        public int? QuestionID { get; set; }
        public string RecordStatus { get; set; }
        public int? Answer { get; set; }
        public bool? isCorrect { get; set; }
        public DateTime? SubmitTiming { get; set; }
        public int? OrderNo { get; set; }
    }
}