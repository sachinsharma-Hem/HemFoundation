using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("hf_view_users")]
    public class HfViewUser
    {
        [Key]
        public long userid { get; set; }

        public string? firstname { get; set; }
        public string? lastname { get; set; }
        public string? gender { get; set; }
        public string? mobileno { get; set; }
        public string? emailid { get; set; }
        public string? addressline1 { get; set; }
        public string? addressline2 { get; set; }
        public string? addressline3 { get; set; }
        public string? pincode { get; set; }
        public string? photourl { get; set; }

        public bool isactive { get; set; }

        public long? createdby { get; set; }
        public DateTime? createddate { get; set; }

        public long? modifiedby { get; set; }
        public DateTime? modifieddate { get; set; }

        public string? aspnetuserid { get; set; }
        public string? registerfrom { get; set; }
        public string? username { get; set; }
        public string? password_ { get; set; }
        public string? languagepreference { get; set; }

        public string? UserRoles { get; set; }

        // IMPORTANT: PostgreSQL string_agg returns text
        public string? UserRolesId { get; set; }
    }
}