namespace HemFoundation.Models.Entities
{
    public class CapacityViewModel
    {
        public int Id { get; set; } // CourseID
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
    }
}