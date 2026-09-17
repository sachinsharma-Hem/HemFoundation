using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("hf_tbl_affliatefocusedlist")]
    public class HfTblAffliateFocusedList
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("institute_id")]
        public int? InstituteId { get; set; }

        [Column("affliate_id")]
        public int? AffliateId { get; set; }

        [Column("ent_id")]
        public int? EntId { get; set; }

        [Column("ent_date")]
        public DateTime? EntDate { get; set; }
    }
}