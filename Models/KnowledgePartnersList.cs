using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models // Adjust namespace to match your project structure
{
    [Table("knowlegde_partners_list")]
    public class KnowledgePartnersList
    {
        [Key]
        [Column("kpid")]
        public int KpId { get; set; }

        [Column("instituteid")]
        public int InstituteID { get; set; }

        [Column("institutename")]
        [StringLength(100)]
        public string? InstituteName { get; set; }

        [Column("fileurl")]
        public string? FileUrl { get; set; }
    }
}