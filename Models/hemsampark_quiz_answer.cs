using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models
{
    [Table("hemsampark_quiz_answers")]
    public class hemsampark_quiz_answer
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("submission_id")]
        public int SubmissionId { get; set; }

        [Column("question_id")]
        public int QuestionId { get; set; }

        [Column("selected_option")]
        public int SelectedOption { get; set; } 

        [Column("is_correct")]
        public bool IsCorrect { get; set; }     
    }
}