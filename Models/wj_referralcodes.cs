using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models
{
    [Table("wj_referralcodes")]
    public partial class WJ_ReferralCodes
    {
        [Key]
        [Column("referralid")]
        public int ReferralID { get; set; }

        [Column("referralcode")]
        public string? ReferralCode { get; set; }


        [Column("isactive")]
        public Nullable<bool> IsActive { get; set; }

        [Column("userid")]
        public Nullable<int> UserID { get; set; }

        //[ForeignKey("UserID")]
        //public virtual Users Users { get; set; }
    }
}