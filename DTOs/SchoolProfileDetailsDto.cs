namespace HemFoundation.Models.DTOs
{
    public class SchoolProfileDetailsDto
    {
        public int instituteid { get; set; }
        public string? institutetype { get; set; }
        public string? institute_name { get; set; }
        public string? profileurl { get; set; }
        public bool? institute_isactive { get; set; }
        public string? referralcode { get; set; }
        public bool isverify { get; set; }
        public string? firstname { get; set; }
        public string? lastname { get; set; }
        public string? mobileno { get; set; }
        public string? emailid { get; set; }
        public string? addressline1 { get; set; }
        public string? addressline2 { get; set; }
        public string? addressline3 { get; set; }
        public string? cityname { get; set; }
        public string? pincode { get; set; }
        public string? photourl { get; set; }
        public bool? user_isactive { get; set; }
        public int createdby { get; set; }
        public DateTime createddate { get; set; }
        public int? modifiedby { get; set; }
        public DateTime? modifieddate { get; set; }
        public string? aspnetuserid { get; set; }
        public string? password_ { get; set; }
        public int? associatedstudents { get; set; }
        public int? activestudents { get; set; }
        public int? totalteachers { get; set; }
        public int? activeteachers { get; set; }
    }
}