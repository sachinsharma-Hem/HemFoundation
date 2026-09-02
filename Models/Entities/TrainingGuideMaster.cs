using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class TrainingGuideMaster
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string MainCategory { get; set; } = string.Empty; // "Demo" ya "PBL"

        [Required]
        public string SubCategory { get; set; } = string.Empty; // e.g., "Cognitive PBL", "Student Demo Video"

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string FileType { get; set; } = string.Empty; // "Video" ya "Document"

        [Required]
        public string FileUrl { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}