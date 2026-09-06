using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;


namespace HemFoundation.Models
{
    public class StudentRegistration
    {
        public StudentRegistration()
        {
            StandardList = new Dictionary<string, string>();
            StandardList.Add("", "-- Standard --");
            //StandardList.Add("1st", "1st");
            //StandardList.Add("2nd", "2nd");
            StandardList.Add("3rd", "3rd");
            StandardList.Add("4th", "4th");
            StandardList.Add("5th", "5th");
            StandardList.Add("6th", "6th");
            StandardList.Add("7th", "7th");
            StandardList.Add("8th", "8th");
            StandardList.Add("9th", "9th");
            StandardList.Add("10th", "10th");
            StandardList.Add("11th", "11th");
            StandardList.Add("12th", "12th");
            StandardList.Add("13th", "13th");
            StandardList.Add("14th", "14th");
            StandardList.Add("15th", "15th");
            StandardList.Add("All", "All");

            
            
          
        }
       
       
       
        public int? StudentID { get; set; }

        [Display(Name = "First Name")]
        //[RegularExpression(@"^[a-zA-Z]+[ a-zA-Z-_]*$", ErrorMessage = "Use Characters only")]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        //[RegularExpression(@"^[a-zA-Z]+[ a-zA-Z-_]*$", ErrorMessage = "Use Characters only")]
        public string LastName { get; set; }

        [Display(Name = "Gender", Description = "")]
        public string Gender { get; set; }

        [Display(Name = "Standard", Description = "")]
        public string Standard { get; set; }

        [Display(Name = "Division", Description = "")]
        public string Division { get; set; }

        [Display(Name = "Roll No", Description = "")]
        public string RollNo { get; set; }


        [RegularExpression(@"[0-9]*\.?[0-9]+", ErrorMessage = "{0}, Use Number Only")]
        [MaxLength(10)]
        [MinLength(10)]
        [Required]
        [Display(Name = "MobileNo", Description = "")]
        public string MobileNo { get; set; }
        public int CountryCode { get; set; }

        [DataType(DataType.EmailAddress)]
        [EmailAddress]
        [Display(Name = "Email ID", Description = "")]
        public string EmailID { get; set; }

        [Display(Name = "CityName")]
        //   [RegularExpression(@"^[a-zA-Z]+[ a-zA-Z-_]*$", ErrorMessage = "Use Characters only")]
        public string CityName { get; set; }

        [Display(Name = "Parent Name")]
        public string GardianName { get; set; }

        [Display(Name = "Parent Email Id")]
        public string GuardianEmailID { get; set; }

        [Display(Name = "Parent Mobile No.")]
        public string GuardianMobileNo { get; set; }

        [Display(Name = "Referral Code")]
        public string ReferralCode { get; set; }

        [Display(Name = "User Name")]
        //[Required]
        //[RegularExpression(@"^\S*$", ErrorMessage = "No white space allowed in user name. Please try it without space")]
        //[Remote("IsUserNameExist", "Account", ErrorMessage = "User name already exist")]
        public string UserName { get; set; }

        //  [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        //  [RegularExpression(@"^\S*$", ErrorMessage = "No white space allowed")]
        ///   [DataType(DataType.Password)]
        [Display(Name = "Password")]
        //[Required]
        public string Password { get; set; }
        public string isWhatsapp { get; set; }
        public Dictionary<string, string> StandardList;
        public Dictionary<string, string> CityList;
        public Dictionary<string, string> DateDayList;
        public Dictionary<string, string> DateMonthList;
        public Dictionary<string, string> DateYearList;
        public Dictionary<string, string> City;
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? DateOfBirth { get; set; }
        //
        DateTime? _DOB = null;
        [Display(Name = "Date of Birth")]
        public Nullable<System.DateTime> DOB
        {
            get
            {
                return _DOB;
            }
            set
            {
                if (value != null)
                {
                    DateTime dt = Convert.ToDateTime(value);
                    _DOB = dt;
                    DateDay = dt.Day.ToString("D2");
                    DateMonth = dt.Month.ToString("D2");
                    DateYear = dt.Year.ToString();
                }
            }
        }

        public void SetDate(int year, int month, int day)
        {
            DateTime dt = new DateTime(year, month, day);
            DOB = dt;
        }

        string _DateDay = "";
        [Display(Name = "Day")]
        public string DateDay
        {
            get
            {
                return _DateDay;
            }
            set { _DateDay = value; }
        }

        string _DateMonth = "";
        [Display(Name = "Month")]
        public string DateMonth
        {
            get
            {
                return _DateMonth;
            }
            set
            {
                _DateMonth = value;
            }
        }

        string _DateYear = "";
        [Display(Name = "Year")]
        public string DateYear
        {
            get
            {
                return _DateYear;
            }
            set
            {
                _DateYear = value;
            }
        }

        [Display(Name = "Address")]
        public string AddressLine1 { get; set; }

        [Display(Name = "Near by Landmark")]
        public string AddressLine2 { get; set; }

        [Display(Name = "Area Name")]
        public string AddressLine3 { get; set; }

        [Display(Name = "Pincode")]
        public string Pincode { get; set; }

        [Display(Name = "Phone No")]
        public string PhoneNo { get; set; }

        [Display(Name = "Phone No")]
        public string PhotoUrl { get; set; }

        [Display(Name = "Profile Url")]
        public string ProfileUrl { get; set; }

        [Display(Name = "Course Language Preference")]
        public string LanguagePreference { get; set; }

        public int? ReferralInstituteID { get; set; }
        public string TempInstituteID { get; set; }
        public bool IsPaid { get; set; }

        public string RegistrationType { get; set; }

    
       
        public int InstituteID { get; set; }

    }
}