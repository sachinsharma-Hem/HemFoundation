using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class AffiliateBankDetail : BaseMasterEntity
    {
        public long AffiliateId { get; set; }

        [StringLength(150)]
        public string AccountName { get; set; } = string.Empty;

        [StringLength(50)]
        public string AccountNumber { get; set; } = string.Empty;

        [StringLength(150)]
        public string BankName { get; set; } = string.Empty;

        [StringLength(20)]
        public string IFSC { get; set; } = string.Empty;

        [StringLength(20)]
        public string PAN { get; set; } = string.Empty;
    }
}