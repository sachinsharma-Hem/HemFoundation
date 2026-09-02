using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    public partial class WJ_Institutes
    {
        public WJ_Institutes()
        {
            WJ_StudentAssociates = new HashSet<WJ_StudentAssociates>();
            WJ_Syllabus = new HashSet<WJ_Syllabus>();
            
            WJ_TeacherAssociates = new HashSet<WJ_TeacherAssociates>();
        }

        [Key]
        public int InstituteID { get; set; }

        public string InstituteType { get; set; }
        public string Name { get; set; }
        public string BannerImageUrl { get; set; }
        public string ProfileUrl { get; set; }
        public bool? IsActive { get; set; }
        public string ReferralCode { get; set; }
        public bool IsVerify { get; set; }
        public string Profession { get; set; }
        public string SocialOrgName { get; set; }
        public string TypeOfOrganisation { get; set; }

        public virtual Users Users { get; set; }
        
        public virtual ICollection<WJ_StudentAssociates> WJ_StudentAssociates { get; set; }
        public virtual ICollection<WJ_Syllabus> WJ_Syllabus { get; set; }

        public virtual ICollection<WJ_TeacherAssociates> WJ_TeacherAssociates { get; set; }
    }
}