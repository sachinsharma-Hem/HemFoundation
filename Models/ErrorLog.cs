using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models
{
    [Table("error_logs")]
    public class ErrorLog
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("request_id")]
        [MaxLength(100)]
        public string RequestId { get; set; } = string.Empty;

        [Column("request_path")]
        [MaxLength(250)]
        public string? RequestPath { get; set; }

        [Column("http_method")]
        [MaxLength(10)]
        public string? HttpMethod { get; set; }

        [Column("message")]
        [MaxLength(500)]
        public string? Message { get; set; }

        [Column("exception_type")]
        [MaxLength(250)]
        public string? ExceptionType { get; set; }

        [Column("stack_trace")]
        public string? StackTrace { get; set; }

        [Column("inner_exception")]
        public string? InnerException { get; set; }

        [Column("user_identifier")]
        [MaxLength(100)]
        public string? UserIdentifier { get; set; }

        [Column("created_at_utc")]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
