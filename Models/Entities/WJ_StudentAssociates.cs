using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("wj_studentassociates")]
    [PrimaryKey(nameof(StudentID), nameof(InstituteID))]
    public partial class WJ_StudentAssociates
    {
        [Key, Column("studentid", Order = 0)]
        public int StudentID { get; set; }

        [Key, Column("instituteid", Order = 1)]
        public int InstituteID { get; set; }

        [Column("isprimaryassociate")]
        public bool? IsPrimaryAssociate { get; set; }

        public virtual WJ_Institutes? WJ_Institutes { get; set; }
        public virtual WJ_Students? WJ_Students { get; set; }
    }
}