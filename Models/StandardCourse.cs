using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("standard_courses")]
    public partial class StandardCourse
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("standard")]
        public string Standard { get; set; } = null!;

        [Column("coursename")]
        public string CourseName { get; set; } = null!;

        [Column("pdfsrc")]
        public string PdfSrc { get; set; } = null!;

        [Column("coursevalue")]
        public string CourseValue { get; set; } = null!;
    }
}