using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class MonthlyBillItem : BaseMasterEntity
    {
        public int MonthlyBillId { get; set; } // Foreign Key

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }
    }
}