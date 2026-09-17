using HemFoundation.Models.Entities;
using PuppeteerSharp;

namespace HemFoundation.DTOs
{
    public class StudentProfileModel
    {
        public StudentProfileModel()
        {
          
           
        }

        public string StudentName { get; set; }
        public int Points { get; set; }
        public string SchoolName { get; set; }
        public int StudentID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string CityName { get; set; }
        public int InstituteID { get; set; }
        public string InstituteName { get; set; }
        public string CourseName { get; set; }
        public string Standard { get; set; }
        public string Division { get; set; }
        public string PhotoUrl { get; set; }
        public int Coins { get; set; }
        public int Rank { get; set; }
        public string StudentGroup { get; set; }
        public string Pincode { get; set; }
        public string StudentLevel { get; set; }
        public int Gems { get; set; }
        public int Credit { get; set; }
        public int NationalRank { get; set; }
        public int WeeklyRank { get; set; }
        public int MonthlyRank { get; set; }
        public string Gender { get; set; }
        public DateTime? DOB { get; set; }
        public string MobileNo { get; set; }
        public string EmailID { get; set; }
        public string ProfileUrl { get; set; }
        public string ReferralCode { get; set; }
        public string ExpiryDate { get; set; }
        public string RegisteredDate { get; set; }
        public string AddressLine1 { get; set; }
        public string GardianName { get; set; }

        public bool IsPaid { get; set; }
        public bool IsExpired { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsStudent { get; set; }
        public bool IsMyPage { get; set; }
        public bool IsTrialOrDailySpin { get; set; }
        public bool IsCyberSafety { get; set; }
        public bool IsWorkshop { get; set; }
        public bool IsIntraSchool { get; set; }
        public bool IsTeamMember { get; set; }
        public bool IsProductPurchaed { get; set; }
        public bool IsDailySpin { get; set; }
        public bool? IsMobileVerified { get; set; }
        public bool? IsEmailVerified { get; set; }
        public bool IsCaptain { get; set; }
        public bool IsFirstLogin { get; set; }
        public bool IsNewUser { get; set; }
        public bool IsProfileUpdated { get; set; }
        public bool IsDailyQuizSubmitted { get; set; }
        public bool IsPaymentCheck { get; set; }
        public bool IsShareWeekly { get; set; }
        public bool IsShareMonthly { get; set; }
        public string TempInstituteID { get; set; }
        public string LanguagePreference { get; set; }
        public int CertificationCount { get; set; }
        public int IsWhiztalkExist { get; set; }
        public bool IsWhizValued { get; set; }
        public int TrialDayCount { get; set; }
        public string IntraSchoolStatus { get; set; }
        public string IntraEndDate { get; set; }
        public string LoginCount { get; set; }
        public int UsersFollower { get; set; }
        public int UserFollowing { get; set; }

        public int ProfileUpdateCount { get; set; }

        public int?[] FreeCourses { get; set; }
        public int?[] StudentCertificationCourseID { get; set; }
        public bool IsTreasureBox { get; set; }
       

        public bool Week1Completed { get; set; }
        public bool Week2Completed { get; set; }
        public bool Week3Completed { get; set; }
        public bool Week4Completed { get; set; }
        public bool CourageCompleted { get; set; }
        public bool ResilienceCompleted { get; set; }
        public bool DignityofLabourCompleted { get; set; }
        public bool IntegrityCompleted { get; set; }
        public bool SelfEsteemCompleted { get; set; }

        public class WJ_Referral_List
        {
            public int UserID { get; set; }
            public int ReferralID { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? CityName { get; set; }
            public string? Standard { get; set; }
            public string? InstituteName { get; set; }
            public DateTime RegistrationDate { get; set; }
            public bool IsPaid { get; set; }
            public int Coins { get; set; }
            public int TotalGems { get; set; }
            public int Gems { get; set; }
            public int GemsCertificate { get; set; }
        }
        //
        public Nullable<bool> IsGSuit1Passed { get; set; }
        public Nullable<bool> IsGSuit2Passed { get; set; }
        public Nullable<bool> IsGSuitGroup1Passed { get; set; }
        public Nullable<bool> IsGSuitGroup2Passed { get; set; }

        //Daily Quiz
        public int QuestionID { get; set; }
        public int CorrectOption { get; set; }
        public int DailyCoins { get; set; }
        public string QuestionName { get; set; }
       
       
     
        

        public int Skills { get; set; }
        public int Activities { get; set; }
    }

}
