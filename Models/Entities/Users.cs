using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


    namespace HemFoundation.Models.Entities
    {
        [Table("users", Schema = "public")]
        public partial class Users
        {
           
            [Key]
            [Column("userid")]
            public int UserID { get; set; }

            [Column("usercode")]
            public string? UserCode { get; set; }

            [Column("title")]
            public string? Title { get; set; }

            [Column("firstname")]
            public string FirstName { get; set; }

            [Column("lastname")]
            public string LastName { get; set; }

            [Column("gender")]
            public string? Gender { get; set; }

            [Column("maritalstatus")]
            public int? MaritalStatus { get; set; }

            [Column("dob")]
            public DateTime? DOB { get; set; }

            [Column("mobileno")]
            public string? MobileNo { get; set; }

            [Column("phoneno")]
            public string? PhoneNo { get; set; }

            [Column("emailid")]
            public string? EmailID { get; set; }

            [Column("addressline1")]
            public string AddressLine1 { get; set; }

            [Column("addressline2")]
            public string? AddressLine2 { get; set; }

            [Column("addressline3")]
            public string? AddressLine3 { get; set; }

            [Column("city")]
            public int? City { get; set; }

            [Column("pincode")]
            public string? Pincode { get; set; }

            [Column("photourl")]
            public string? PhotoUrl { get; set; }

            [Column("isactive")]
            public bool? IsActive { get; set; }

            [Column("createdby")]
            public int CreatedBy { get; set; }

            [Column("createddate")]
            public DateTime CreatedDate { get; set; }

            [Column("modifiedby")]
            public int? ModifiedBy { get; set; }

            [Column("modifieddate")]
            public DateTime? ModifiedDate { get; set; }

            [Column("aspnetuserid")]
            public string AspNetUserID { get; set; }

            [Column("registerfrom")]
            public string? RegisterFrom { get; set; }

            [Column("accesstoken")]
            public string? AccessToken { get; set; }

            [Column("cityname")]
            public string? CityName { get; set; }

            [Column("password_")]
            public string Password_ { get; set; }

            [Column("ismobileverified")]
            public bool? IsMobileVerified { get; set; }

            [Column("isemailverified")]
            public bool? IsEmailVerified { get; set; }

            [Column("languagepreference")]
            public string LanguagePreference { get; set; }

            [Column("isuserdeactivated")]
            public bool? isUserDeactivated { get; set; }

            // Navigation Properties
         
            
            
            public virtual AspNetUsers AspNetUsers { get; set; }
            
            //public virtual ICollection<Courses> Courses { get; set; }
            //public virtual ICollection<Courses> Courses1 { get; set; }
            //public virtual ICollection<Courses> Courses2 { get; set; }
           
            
            //public virtual WJ_Institutes WJ_Institutes { get; set; }
            
            //public virtual WJ_Students WJ_Students { get; set; }
           
            //public virtual WJ_Teacher WJ_Teacher { get; set; }
           
        }
    }

