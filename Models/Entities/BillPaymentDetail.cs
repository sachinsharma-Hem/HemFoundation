using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class BillPaymentDetail : BaseMasterEntity
    {
        public int MonthlyBillId { get; set; }

        [StringLength(100)]
        public string ApprovedBy { get; set; } = string.Empty;

        public DateTime PaymentDate { get; set; }
        public decimal AmountPaid { get; set; }

        [StringLength(100)]
        public string TransactionNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string PaymentBy { get; set; } = string.Empty;

        [StringLength(100)]
        public string EntryBy { get; set; } = string.Empty;
    }
}