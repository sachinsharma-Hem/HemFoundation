using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("hf_rel_manage_group_list")]
    public partial class HfRelManageGroupList
    {
        [Key]
        [Column("mgslid")]
        public int MgslId { get; set; }

        [Column("groupname")]
        public string? GroupName { get; set; }

        [Column("studentid")]
        public int? StudentID { get; set; }

        [Column("entrydate")]
        public DateTime? EntryDate { get; set; }

        [Column("isallocated", TypeName = "bit")]
        public bool? IsAllocated { get; set; }

        [Column("isallocatedby")]
        public int? IsAllocatedBy { get; set; }

        [Column("activitytype")]
        public string? ActivityType { get; set; }

        [Column("maincourseid")]
        public int? MainCourseID { get; set; }
    }
}