using HemFoundation.Common;
using System;
using System.Collections.Generic;

namespace HemFoundation.Models.Entities // Or your chosen ViewModel namespace
{
    [Serializable]
    public class QuestionModel
    {
        public QuestionModel()
        {
            QuestionOption = new List<QuestionOptionViewModel>();
        }

        public int? QuestionID { get; set; }
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
    public class ExamResultModel
    {
        public ExamResultModel()
        {
            examList = new List<ExamViewModel>();
        }
        public int ExamID { get; set; }
        public int QuizID { get; set; }
        public string TopicName { get; set; }
        public int TopicID { get; set; }
        public int CorrectQuestion { get; set; }
        public int InCorrectQuestion { get; set; }
        public int UnattemptedQuestion { get; set; }
        public string ExamTime { get; set; }
        public int Coins { get; set; }
        public int Rank { get; set; }
        public int QuizCoins { get; set; }
        public int QuizRank { get; set; }
        public int QuizGems { get; set; }
        public int QuestionCount { get; set; }
        public string LevelName { get; set; }
        public int CoinsEarned { get; set; }
        public string CourseName { get; set; }
        public int StudentID { get; set; }
        public string QuizName { get; set; }
        public int UserID { get; set; }
        public int LevelID { get; set; }
        public int CourseIDV { get; set; }
        public List<int> CourseID { get; set; }

        public string PassStatus { get; set; }
        public int TotalMarks { get; set; }
        public int PassMarks { get; set; }
        public int MarksObtain { get; set; }
        public int percentage { get; set; }
        public int TotalAttempt { get; set; }

        public DateTime AppearedDate { get; set; }

        string _DOBDisplay = "";
        public string DOBDisplay
        {
            get { return AppearedDate.ConvertDateToString(); }
        }

        public List<ExamViewModel> examList { get; set; }
    }
}