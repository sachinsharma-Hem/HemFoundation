namespace HemFoundation.Models.Entities
{
    public class CapacityViewModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }

        // Quiz ID
        public int? QuizId { get; set; }

        // Optional - agar view me quiz name bhi chahiye
        public string? QuizName { get; set; }
    }
}