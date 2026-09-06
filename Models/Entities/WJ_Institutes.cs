using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("wj_institutes")]
    public partial class WJ_Institutes
    {
       
        [Key]
        [Column("instituteid")]

        public int InstituteID { get; set; }
        [Column("institutetype")]

        public string? InstituteType { get; set; }
        [Column("Name")]

        public string? Name { get; set; }
        [Column("bannerimageurl")]

        public string? BannerImageUrl { get; set; }
        [Column("profileurl")]

        public string? ProfileUrl { get; set; }
        [Column("isactive")]

        public bool? IsActive { get; set; }
        [Column("referralcode")]

        public string? ReferralCode { get; set; }
        [Column("isverify")]

        public bool IsVerify { get; set; }
        [Column("profession")]

        public string? Profession { get; set; }
        [Column("socialorgname")]

        public string? SocialOrgName { get; set; }
        [Column("typeoforganisation")]

        public string? TypeOfOrganisation { get; set; }


       
    }
}