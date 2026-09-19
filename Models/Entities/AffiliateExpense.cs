using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class AffiliateExpense : BaseMasterEntity
    {
        public long AffiliateId { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime ExpenseDate { get; set; }

        [Required, StringLength(500)]
        public string ExpenseDetail { get; set; } = string.Empty;

        [Required]
        public decimal Amount { get; set; }

        [StringLength(30)]
        public string Status { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"

        [StringLength(50)]
        public string? RelatedType { get; set; }

        public long? RelatedId { get; set; }
        [StringLength(500)]
        public string? ReceiptPath { get; set; }
    }
}