namespace HemFoundation.Models.Admin
{
    public class PasswordMismatchUserDto
    {
        public string UserId { get; set; } = string.Empty;

        public string? UserName { get; set; }

        public string? Email { get; set; }

        public string Status { get; set; } = "Mismatch";
    }

    public class FixPasswordRequest
    {
        public List<string> UserIds { get; set; } = new();
    }

    public class PasswordFixResultDto
    {
        public string UserId { get; set; } = string.Empty;

        public string? UserName { get; set; }

        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}