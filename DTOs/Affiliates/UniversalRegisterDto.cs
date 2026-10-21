namespace HemFoundation.DTOs
{
    public class UniversalRegisterDto
    {
        public int PortalId { get; set; } // Database PortalMasters ID (2, 3, 4, 5, 6)
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? OrganisationName { get; set; } = string.Empty;
        public string? TypeOfOrganisation { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty; // Acts as Username & Password
        public string? EmailID { get; set; } = string.Empty;
        public string? Address { get; set; } = string.Empty;
        public string? BranchName { get; set; } = string.Empty;
        public string? Pincode { get; set; } = string.Empty;
        public string? City { get; set; } = string.Empty;
        public string? State { get; set; } = string.Empty;
        public IFormFile? ProfilePhoto { get; set; } // Uploaded Photo
    }
}