namespace HemFoundation.Models.Entities
{
    public class MonthlyExpenseBill
    {
        public long Id { get; set; }
        public long AffiliateId { get; set; }
        public string MonthYear { get; set; } = string.Empty; 
        public decimal TotalAmount { get; set; }
        public string? GeneratedPdfUrl { get; set; }
        public string? SignedPdfUrl { get; set; }
        public string Status { get; set; } = "Generated"; 
        public string? AdminRemarks { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
    }
}