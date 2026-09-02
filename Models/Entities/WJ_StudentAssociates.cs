using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Models.Entities
{
    // Configure composite primary key in EF Core context if needed using: modelBuilder.Entity<WJ_StudentAssociates>().HasKey(s => new { s.StudentID, s.InstituteID });
    [PrimaryKey(nameof(StudentID), nameof(InstituteID))]
    public partial class WJ_StudentAssociates
    {
        [Key, Column(Order = 0)]
        public int StudentID { get; set; }

        [Key, Column(Order = 1)]
        public int InstituteID { get; set; }

        public bool? IsPrimaryAssociate { get; set; }

        public virtual WJ_Institutes WJ_Institutes { get; set; }
        public virtual WJ_Students WJ_Students { get; set; }
    }
}