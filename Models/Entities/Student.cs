using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class Student
    {
        [Key]
        public int StudentID { get; set; }
        public int? InstituteID { get; set; } // Mapping to School/Institute
        public string? Standard { get; set; } // 3rd to 15th
        public string? Division { get; set; }
        public string? GardianName { get; set; }
        public string? ReferralCode { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsPaid { get; set; } = false;
        public DateTime? TrialPeriodTill { get; set; }
        public string? RollNo { get; set; }
        public bool SharingPermission { get; set; } = false;
        public string? LevelPath { get; set; }
        public string? GuardianEmailID { get; set; }
        public string? GuardianMobileNo { get; set; }

        // Extra details for profile binding
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? AspNetUserID { get; set; }
        public string? PhotoUrl { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}