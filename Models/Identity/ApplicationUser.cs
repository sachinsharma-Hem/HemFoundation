using Microsoft.AspNetCore.Identity;

namespace HemFoundation.Models.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? UserCode { get; set; }
        public string? PortalName { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow; 

    }
}