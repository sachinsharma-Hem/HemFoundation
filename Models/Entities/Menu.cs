using HemFoundation.Helpers;
using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class Menu
    {
        [Key]
        public long Id { get; set; }

        public long? ParentId { get; set; }

        [Required]
        [StringLength(150)]
        public string MenuName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Area { get; set; }

        [StringLength(100)]
        public string? Controller { get; set; }

        [StringLength(100)]
        public string? Action { get; set; }

        [StringLength(250)]
        public string? RouteUrl { get; set; }

        [StringLength(100)]
        public string? Icon { get; set; }

        public int DisplayOrder { get; set; } = 0;

        // 0 = Menu, 1 = Sub Menu, 2 = Header, 3 = Divider
        public int MenuType { get; set; } = 0;

        public int PortalType { get; set; } = 1;

        public bool IsVisible { get; set; } = true;

        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedOn { get; set; } = DateTimeHelper.UtcNow;

        public DateTime? UpdatedOn { get; set; }
    }
}