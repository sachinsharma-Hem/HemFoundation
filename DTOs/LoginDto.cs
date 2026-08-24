using System.ComponentModel.DataAnnotations;

namespace HemFoundation.DTOs
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Email or Mobile Number is required")]
        public string Email { get; set; } = string.Empty; // Acts as both Email or Mobile No

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
    }
}