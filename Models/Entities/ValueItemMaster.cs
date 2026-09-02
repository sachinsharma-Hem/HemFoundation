using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class ValueItemMaster
    {
        [Key]
        public int Id { get; set; }
        public int GradeId { get; set; } // FK to ValueGradeMaster
        public string CapacityName { get; set; } = string.Empty; // Cognitive, Social, etc.
        public string ValueTitleEng { get; set; } = string.Empty; // e.g., Curiosity
        public string ValueTitleHin { get; set; } = string.Empty; // e.g., Jigyassa
        public string? ValueImageUrl { get; set; }
        public string? ActivityPdfUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}