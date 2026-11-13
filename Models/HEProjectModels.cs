namespace HemFoundation.Models
{
    public class StudentSelectionDto
    {
        public int StudentID { get; set; }
        public string StudentName { get; set; }
        public string Email { get; set; }
    }

    public class ProjectSubmissionDto
    {
        public int SubmissionID { get; set; }
        public string ProjectName { get; set; }
        public string VideoActivity { get; set; }
    }

    public class InvitationStatusDto
    {
        public int StudentID { get; set; }
        public string StudentsName { get; set; }
        public string Email { get; set; }
        public bool IsAccepted { get; set; }
    }
}