using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("wj_teacherassociates")] // Matches your exact PostgreSQL table name
    [PrimaryKey(nameof(TeacherID), nameof(InstituteID))]
    public partial class WJ_TeacherAssociates
    {
        [Column("teacherid")]
        public int TeacherID { get; set; }

        [Column("instituteid")]
        public int InstituteID { get; set; }

        [Column("isprimaryassociate", TypeName = "bit")]
        public bool? IsPrimaryAssociate { get; set; }

        public virtual WJ_Institutes WJ_Institutes { get; set; }
        public virtual WJ_Teacher WJ_Teacher { get; set; }
    }
}