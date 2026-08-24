using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class VolunteerInstituteMapping : BaseMasterEntity
    {
        public int VolunteerId { get; set; }
        public int InstituteId { get; set; }

        [StringLength(450)]
        public string? LastUpdatedBy { get; set; } 
    }
}