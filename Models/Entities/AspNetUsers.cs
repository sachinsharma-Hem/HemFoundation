using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("AspNetUsers", Schema = "public")]
    public partial class AspNetUsers
    {
        public AspNetUsers()
        {
            
            this.Users = new HashSet<Users>();
            this.AspNetRoles = new HashSet<AspNetRoles>();
        }

        [Key]
        [Column("id")]
        public string Id { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("emailconfirmed")]
        public bool EmailConfirmed { get; set; }

        [Column("passwordhash")]
        public string PasswordHash { get; set; }

        [Column("securitystamp")]
        public string SecurityStamp { get; set; }

        [Column("phonenumber")]
        public string PhoneNumber { get; set; }

        [Column("phonenumberconfirmed")]
        public bool PhoneNumberConfirmed { get; set; }

        [Column("twofactorenabled")]
        public bool TwoFactorEnabled { get; set; }

        [Column("lockoutenddateutc")]
        public DateTime? LockoutEndDateUtc { get; set; }

        [Column("lockoutenabled")]
        public bool LockoutEnabled { get; set; }

        [Column("accessfailedcount")]
        public int AccessFailedCount { get; set; }

        [Column("username")]
        public string UserName { get; set; }

        // Navigation Properties
       // public virtual ICollection<AspNetUserClaims> AspNetUserClaims { get; set; }
      
        public virtual ICollection<Users> Users { get; set; }
        public virtual ICollection<AspNetRoles> AspNetRoles { get; set; }
    }
}