using System.ComponentModel.DataAnnotations;

namespace HemFoundation.Models.Entities
{
    public class BulkUploadLog
    {
        [Key]
        public int LogID { get; set; }
        public int InstituteID { get; set; }
        public string UploadType { get; set; } // "Student" ya "Teacher"
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public int TotalRecords { get; set; }
        public string Status { get; set; } = "Completed"; // Success/Failed
        public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
    }
}