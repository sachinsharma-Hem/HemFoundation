using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class StandardMaster
    {
        [Key]
        public int Id { get; set; }
        public string StandardName { get; set; } = string.Empty; // e.g., "3rd Standard", "4th Standard"... up to "15th Level"
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}