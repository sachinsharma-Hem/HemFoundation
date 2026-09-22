namespace HemFoundation.DTOs
{
    public class SchoolReportViewModel
    {
        public InstituteDetails Header { get; set; }
        public StudentStats studentStats { get; set; }
        public TeacherStats teacherStats { get; set; }
        public List<StandardSummary> standardSummaries { get; set; }
        public List<TeacherCourseSummary> teacherCourseSummary { get; set; }
        public List<Top10Performer> top10Performer { get; set; }
        public List<TopStd3Performer> Top3StdPerformers { get; set; }
        public List<ProjectStats> ProjectSummaries { get; set; }
        public List<StudentsCertifiedStats> studentsCertifiedStats { get; set; }
    }
    public class InstituteDetails
    {
        public string InstituteName { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Pincode { get; set; }
        public string Email { get; set; }
        public string MobileNo { get; set; }
        /*...*/
    }
    public class StudentStats
    {
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int InactiveStudents { get; set; }
    }
    public class TeacherStats
    {
        public int TotalTeacher { get; set; }
        public int ActiveTeacher { get; set; }
        public int InActiveTeacher { get; set; }
    }

    public class StandardSummary
    {
        public string Standard { get; set; }
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int InactiveStudents { get; set; }
    }
    public class TeacherCourseSummary
    {
        public string Teacher_Name { get; set; }
        public string CourseCompleted { get; set; }
        public string CoursePending { get; set; }
        public int NoOFCourseCompleted { get; set; }
        public int NoofCoursePending { get; set; }
        public int CertifiedinMainCourseCount { get; set; }
        public int TotalObtainedMarks { get; set; }
    }


    public class Top10Performer
    {
        public string Standard { get; set; }
        public Int64 InstRank { get; set; }
        public string Student_Name { get; set; }
        public int TotalPoints { get; set; }
    }
    public class TopStd3Performer
    {
        public string Standard { get; set; }
        public Int64 StdRank { get; set; }
        public string Student_Name { get; set; }
        public int TotalPoints { get; set; }
    }
    public class ProjectStats
    {
        public string Standard { get; set; }
        public int TotalStudents { get; set; }
        public int IndividualPBLCompleted { get; set; }
        public int FamilyPBLCompleted { get; set; }
        public int GroupPBLCompleted { get; set; }
    }
    public class StudentsCertifiedStats
    {
        public string Standard_ { get; set; }
        public string Student_Name { get; set; }
        public int No_of_Courses_Completed { get; set; }
        public int No_of_Courses_Pending { get; set; }
        public string Courses_Completed { get; set; }
        public string Courses_Pending { get; set; }
        public int MarksObtain { get; set; }
    }

}
