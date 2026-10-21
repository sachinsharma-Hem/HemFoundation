using System.ComponentModel.DataAnnotations;

namespace HemFoundation.DTOs
{
    public class VolunteerWithUsernameDto
    {
        public Int32 ID { get; set; }
        public string FirstName { get; set; } = string.Empty;

       
        public string LastName { get; set; } = string.Empty;

        public string MobileNo { get; set; } = string.Empty;

      
        public string? Email { get; set; }

        public int InstituteId { get; set; } // The school assigned to this volunteer

        public long AffiliateId { get; set; } // The affiliate who created this volunteer

      
        public string AspNetUserId { get; set; } = string.Empty;

        public string Password_ { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
    }
}
