using System.ComponentModel.DataAnnotations;

namespace HemFoundation.DTOs
{
    public class UserCreateDto
    {
        [Required(ErrorMessage = "Full Name is required.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required.")]
        public string RoleName { get; set; } = string.Empty; // e.g., "Teacher", "Admin", etc.

        public string? MobileNo { get; set; }
        public int? GenderId { get; set; }
        public int? PortalId { get; set; }
        public string? PinCode { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
    }
}