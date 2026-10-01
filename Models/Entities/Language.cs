using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
   

    [Table("language")]
    public class Language
    {
        [Key]
        [Column("languageid")]
        public int LanguageID { get; set; }

        [Column("languagename")]
        public string? LanguageName { get; set; }

        [Column("createddate")]
        public DateTime? CreatedDate { get; set; }
    }
}
