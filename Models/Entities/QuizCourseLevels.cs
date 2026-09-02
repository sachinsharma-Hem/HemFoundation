namespace HemFoundation.Models.Entities
{
    public partial class QuizCourseLevels
    {
        public int QuizID { get; set; }
        public int LevelID { get; set; }
        public int CourseID { get; set; }

        public virtual Courses Courses { get; set; }
        //public virtual Levels Levels { get; set; }
        public virtual Quiz Quiz { get; set; }
    }
}
