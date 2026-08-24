using System.ComponentModel.DataAnnotations;

namespace HemFoundation.DTOs
{
    public class MenuDto
    {
        public long Id { get; set; }
        public long? ParentId { get; set; }

        [Required(ErrorMessage = "Menu Name is required.")]
        public string MenuName { get; set; } = string.Empty;

        public string? Area { get; set; }
        public string? Controller { get; set; }
        public string? Action { get; set; }
        public string? RouteUrl { get; set; }
        public string? Icon { get; set; }

        public int DisplayOrder { get; set; } = 1;
        public int MenuType { get; set; } = 0;

        // Plain integer matching PortalMasters table ID
        public int PortalType { get; set; } = 1;

        public bool IsVisible { get; set; } = true;
        public bool IsActive { get; set; } = true;
    }
}