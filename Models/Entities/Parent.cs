using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class Parent
    {
        [Key]
        public int ParentID { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? MobileNo { get; set; }
        public string? EmailID { get; set; }
        public string? RelationWithStudent { get; set; } // Father, Mother, Guardian
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public string? PhotoUrl { get; set; }
        public string? AspNetUserID { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}