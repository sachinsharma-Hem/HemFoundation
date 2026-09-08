namespace HemFoundation.DTOs
{
    public class TeacherQuizHubDto
    {
        public int quizid { get; set; }

        public string quizname { get; set; } = string.Empty;

        public string? imageurl { get; set; }

        public string? name { get; set; }

        public string? description { get; set; }

        public int id { get; set; }

        public bool islocked { get; set; }

        public int? ordernumberid { get; set; }

        public bool? isactive { get; set; }

        public string? isdelete { get; set; }

        public DateTime? createddate { get; set; }

        public DateTime? modifieddate { get; set; }
    }
}
