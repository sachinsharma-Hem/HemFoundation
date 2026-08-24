using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class GenderMaster
    {
        [Key]
        public int Id { get; set; } // 1, 2, 3...

        [Required]
        [StringLength(20)]
        public string GenderName { get; set; } = string.Empty; // Male, Female, Other...

        public bool IsActive { get; set; } = true;
    }
}