using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class RequisitionItem : BaseMasterEntity
    {
        [Required, StringLength(200)]
        public string ItemName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}