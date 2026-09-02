namespace HemFoundation.Models
{
    public class ExamModel
    {
        public int SummerExaminationID { get; set; }
        public int CId { get; set; }
        public int ExaminationID { get; set; }
        public int totalIncurrectAns { get; set; }
        public int totalCurrectAns { get; set; }
        public int TotalQuation { get; set; }
        public int ExamId { get; set; }
        public int TopicID { get; set; }
        public int MarksObtain { get; set; }
        public int TotalMarks { get; set; }
        public int CourseID { get; set; }
        //public int QuizID { get; set; }
        public int CoinsEarned { get; set; }
        public string? CourseName { get; set; }
        public string? LevelName { get; set; }
        public int RightMarksCount { get; set; }
        public int WrongMarksCount { get; set; }
        public string? CompetetionCoins { get; set; }
        public int QuizTime { get; set; }
        public bool IsAttempts { get; set; }
        public int GetExamID { get; set; }
        public int QuizId { get; set; }
        public int LevelID { get; set; }
        public string? IsInstituteQuiz { get; set; }
        public string? Iscertificate { get; set; }
        public string?  TopicName { get; set; }
        public int QuestionCount { get; set; }
        public int LanguageID { get; set; }
    }
}
