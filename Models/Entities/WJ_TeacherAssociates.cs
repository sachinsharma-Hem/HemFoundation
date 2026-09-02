using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [PrimaryKey(nameof(TeacherID), nameof(InstituteID))]
    public partial class WJ_TeacherAssociates
    {
        
        public int TeacherID { get; set; }

       
        public int InstituteID { get; set; }

        public bool? IsPrimaryAssociate { get; set; }

        public virtual WJ_Institutes WJ_Institutes { get; set; }
        public virtual WJ_Teacher WJ_Teacher { get; set; }
    }
}