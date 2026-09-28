using System;

namespace HemFoundation.Models
{
    public class VwUserRoleDetails
    {
        public string? AspnetUserId { get; set; }
        public string? Username { get; set; }
        public string? LoginEmail { get; set; }

        public int? Userid { get; set; }
        public string? Usercode { get; set; }
        public string? Title { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public int? Maritalstatus { get; set; }
        public DateTime? Dob { get; set; }
        public string? Mobileno { get; set; }
        public string? Phoneno { get; set; }
        public string? Emailid { get; set; }
        public string? Addressline1 { get; set; }
        public string? Addressline2 { get; set; }
        public string? Addressline3 { get; set; }
        public int? City { get; set; }
        public string? Cityname { get; set; }
        public string? Pincode { get; set; }
        public string? Photourl { get; set; }

        public bool? UserIsActive { get; set; }
        public bool? Isuserdeactivated { get; set; }
        public bool? Ismobileverified { get; set; }
        public bool? Isemailverified { get; set; }

        public string? Languagepreference { get; set; }
        public DateTime? UserCreatedDate { get; set; }
        public DateTime? UserModifiedDate { get; set; }
        public string? Registerfrom { get; set; }

        public string? RoleId { get; set; }
        public string? RoleName { get; set; }

        public int? Studentid { get; set; }
        public string? StudentStandard { get; set; }
        public string? StudentDivision { get; set; }
        public string? Gardianname { get; set; }
        public string? StudentReferralcode { get; set; }
        public decimal? Registrationamount { get; set; }
        public string? StudentProfileurl { get; set; }

        public bool? StudentIsActive { get; set; }
        public bool? Ispaid { get; set; }

        public DateTime? Trialperiodtill { get; set; }
        public string? Rollno { get; set; }

        public bool? Sharingpermission { get; set; }

        public string? Levelpath { get; set; }
        public string? Guardianemailid { get; set; }
        public string? Guardianmobileno { get; set; }

        public int? Teacherid { get; set; }
        public string? TeacherStandard { get; set; }
        public string? TeacherDivision { get; set; }
        public string? TeacherProfileurl { get; set; }

        public int? Instituteid { get; set; }
        public string? Institutetype { get; set; }
        public string? InstituteName { get; set; }
        public string? Bannerimageurl { get; set; }
        public string? InstituteProfileurl { get; set; }

        public bool? InstituteIsActive { get; set; }

        public string? InstituteReferralcode { get; set; }

        public bool? Isverify { get; set; }

        public string? Profession { get; set; }
        public string? Socialorgname { get; set; }
        public string? Typeoforganisation { get; set; }

        public int? VolunteerId { get; set; }
        public string? VolunteerFirstName { get; set; }
        public string? VolunteerLastName { get; set; }
        public string? VolunteerMobile { get; set; }
        public string? VolunteerEmail { get; set; }
        public int? VolunteerInstituteId { get; set; }
        public long? VolunteerAffiliateId { get; set; }
        public DateTime? VolunteerCreatedDate { get; set; }
        public DateTime? VolunteerModifiedDate { get; set; }

        public bool? VolunteerIsActive { get; set; }
        public bool? VolunteerIsDeleted { get; set; }
    }
}