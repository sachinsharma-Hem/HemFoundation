using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class ValueGradeMaster
    {
        [Key]
        public int Id { get; set; }
        public int StandardId { get; set; } 
        public string GradeName { get; set; } = string.Empty; 
        public string GroupName { get; set; } = string.Empty; 

        public string ButtonName { get; set; } = "Start";

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}