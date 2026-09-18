namespace HemFoundation.DTOs
{

    public class StundetSummaryReportModel
    {
        public int StudentID { get; set; }
        public int InstituteID { get; set; }
        public string SchoolName { get; set; }
        public string StudentName { get; set; }
        public string Division { get; set; }
        public string Stanadard { get; set; }
        public int ToTalMCQ { get; set; }
        public int TotalPractical { get; set; }
        public int GrandTotalMCQPractical { get; set; }
        public List<StudentDetailSummary> StudentDetailList { get; set; }
    }
    public class StudentDetailSummary
    {
        public string CourseName { get; set; }
        public string TopicName { get; set; }
        public int MaxMCQ_Points { get; set; }
        public int MaxPractical_Point { get; set; }
        public int TotalGrandMCQ { get; set; }
        public int TotalGrandTPractical { get; set; }
    }
}
