using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class RoleDashboardMapping
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string RoleName { get; set; } = string.Empty; // e.g., "Admin", "TesterEmployee"

        [Required]
        [StringLength(250)]
        public string RedirectUrl { get; set; } = string.Empty; // e.g., "/Admin/Dashboard", "/TesterEmployee/Dashboard"

        public bool IsActive { get; set; } = true;
    }
}