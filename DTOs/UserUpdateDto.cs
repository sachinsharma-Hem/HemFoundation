using System.ComponentModel.DataAnnotations;

namespace HemFoundation.DTOs
{
    public class UserUpdateDto
    {
        [Required]
        public string Id { get; set; } = string.Empty; // Identity User ID (Guid string)

        [Required(ErrorMessage = "Full Name is required.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? MobileNo { get; set; }
        public string? RoleName { get; set; }
        public int? GenderId { get; set; }
        public int? PortalId { get; set; }
        public string? PinCode { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
    }
}