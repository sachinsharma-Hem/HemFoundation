using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class StudentModuleContent
    {
        [Key]
        public int Id { get; set; }
        public int ModuleId { get; set; }
        public string TabCategory { get; set; } = string.Empty;

        // Naye Columns
        public string SectionHeading { get; set; } = string.Empty; // e.g., "BELIEFS SURVEY MCQ"
        public string CardTag { get; set; } = string.Empty; // e.g., "PRESURVEY"

        public string Title { get; set; } = string.Empty; // Card ke niche ka text

        public string? ThumbnailUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? ResourceFileUrl { get; set; }

        public int Points { get; set; } = 0;
        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}