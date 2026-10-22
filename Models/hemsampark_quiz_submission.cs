using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models
{
    [Table("hemsampark_quiz_submissions")]
    public class hemsampark_quiz_submission
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }

        [Column("daily_entry_id")]
        public int DailyEntryId { get; set; }

        [Column("score")]
        public int Score { get; set; }

        [Column("total_questions")]
        public int TotalQuestions { get; set; }

        [Column("submitted_at")]
        public DateTime SubmittedAt { get; set; }
    }
}