namespace HemFoundation.Models
{
    public class PblReportTeacherDto
    {
        public int StudentID { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Standard { get; set; }
        public string? InstituteName { get; set; }
        public string? CourseName { get; set; }
        public string? Title { get; set; }
        public string? FileName { get; set; }
        public string? FileExtension { get; set; }
        public string? FileLink { get; set; }
        public DateTime? SubmissionDate { get; set; }
    }
}
