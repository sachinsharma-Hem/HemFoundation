namespace HemFoundation.DTOs
{
    public class PrintableScorecardViewModel
    {
        // Student Metadata
        public string StudentName { get; set; }
        public string Standard { get; set; }
        public string AcademicYear { get; set; }
        public string InstituteName { get; set; }
        public string SchoolLogoUrl { get; set; }

        // Evaluation Component Data Lists
        public List<AcademicScoreLine> AcademicScores { get; set; }
        public List<ValueCourseScoreLine> ValueScores { get; set; }
    }

    public class AcademicScoreLine
    {
        public string SubjectName { get; set; }
        public string TermName { get; set; }
        public decimal MaxMarks { get; set; }
        public decimal MarksObtained { get; set; }
        public decimal PassingMarks { get; set; }
        public string Status { get; set; }
    }

    public class ValueCourseScoreLine
    {
        public string CourseName { get; set; }
        public int McqMarks { get; set; }
        public int PracticalMarks { get; set; }
        public int PblMarks { get; set; }
        public int GrandTotal { get; set; }
    }
}
