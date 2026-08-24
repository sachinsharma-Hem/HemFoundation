using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class ActivityTypeMaster : BaseMasterEntity
    {
        [Required, StringLength(100)]
        public string ActivityName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}