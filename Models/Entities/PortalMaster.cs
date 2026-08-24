using HemFoundation.Helpers;
using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class PortalMaster
    {
        [Key]
        public int Id { get; set; } // 1, 2, 3...

        [Required]
        [StringLength(50)]
        public string PortalName { get; set; } = string.Empty; // Admin, Teacher, Student...

        [StringLength(100)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedOn { get; set; } = DateTimeHelper.UtcNow;
    }
}