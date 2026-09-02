using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    // 1. Group ka Master
    public class ActivityGroup
    {
        [Key] public int Id { get; set; }
        public int InstituteId { get; set; }
        public int ValueItemId { get; set; } // Capacity Card ID
        public string GroupName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    // 2. Group mein kon kon se Students hain
    public class ActivityGroupStudent
    {
        [Key] public int Id { get; set; }
        public int GroupId { get; set; }
        public int StudentId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }

    // 3. Final Submission Data (Image, Video, Comment)
    public class ActivitySubmission
    {
        [Key] public int Id { get; set; }
        public int InstituteId { get; set; }
        public int ValueItemId { get; set; }
        public int GroupId { get; set; } // Kis group ne submit kiya
        public string? ImageUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? Comment { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}