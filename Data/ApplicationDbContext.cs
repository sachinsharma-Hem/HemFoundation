using HemFoundation.Models;
using HemFoundation.Models.DTOs;
using HemFoundation.Models.Entities;
using HemFoundation.Models.HemSampark;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections;
using System.Reflection.Emit;
namespace HemFoundation.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<PblReportTeacherDto> PblReportTeacherDtos { get; set; }
        public DbSet<Institute> Institutes { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public virtual DbSet<AspNetRoles> AspNetRoles { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<PortalMaster> PortalMasters { get; set; }
        public DbSet<GenderMaster> GenderMasters { get; set; }
        public DbSet<Menu> Menus { get; set; }
        public DbSet<RoleMenuRight> RoleMenuRights { get; set; }
        public DbSet<RoleDashboardMapping> RoleDashboardMappings { get; set; }
        public DbSet<Affiliate> Affiliates { get; set; }
        public DbSet<WJ_Institutes> WJ_Institutes { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<ViewWjStudentsDetailsInfo> ViewWjStudentsDetailsInfo { get; set; }
        public DbSet<StandardMaster> StandardMasters { get; set; }
        public DbSet<WJ_Teacher> WJ_Teachers { get; set; }
        public DbSet<Parent> Parents { get; set; }
        public DbSet<MainCourse> MainCourses { get; set; }

        public DbSet<MainCourseSetup> MainCourseSetups { get; set; }
        public DbSet<CourseCategory> CourseCategories { get; set; }
        public DbSet<CourseSubCategory> CourseSubCategories { get; set; }
        public DbSet<CourseOrderNumber> CourseOrderNumbers { get; set; }
        public DbSet<CourseYear> CourseYears { get; set; }
        public DbSet<CourseAuthor> CourseAuthors { get; set; }
        public DbSet<CourseManager> CourseManagers { get; set; }
        public DbSet<CourseFor> CourseFors { get; set; }
        public DbSet<CourseCustomerType> CourseCustomerTypes { get; set; }
        public DbSet<CourseDurationType> CourseDurationTypes { get; set; }
        public DbSet<CourseAllotedTo> CourseAllotedTos { get; set; }
        public DbSet<CourseMcqLevel> CourseMcqLevels { get; set; }
        public DbSet<CourseLevel> CourseLevels { get; set; }
        public DbSet<WebinarMcqCategory> WebinarMcqCategories { get; set; }
        public DbSet<CourseSyllabus> CourseSyllabuses { get; set; }
        public DbSet<CourseRegistration> CourseRegistrations { get; set; }
        public DbSet<CourseQuiz> CourseQuizzes { get; set; }
        public DbSet<CourseTopic> CourseTopics { get; set; }
        public DbSet<CourseSubTopic> CourseSubTopics { get; set; }
        public DbSet<CourseMcqQuestion> CourseMcqQuestions { get; set; }
        public DbSet<CoursePracticalLab> CoursePracticalLabs { get; set; }
        public DbSet<TeacherQuizAttempt> TeacherQuizAttempts { get; set; }
        public DbSet<AffiliateAttendance> AffiliateAttendances { get; set; }
        public DbSet<AffiliateVisitPlan> AffiliateVisitPlans { get; set; }
        public DbSet<AffiliateSchoolMappingHistory> AffiliateSchoolMappingHistories { get; set; }
        public DbSet<AffiliateVisitReport> AffiliateVisitReports { get; set; }
        public DbSet<ActivityTypeMaster> ActivityTypeMasters { get; set; }
        public DbSet<RequisitionItem> RequisitionItems { get; set; }
        public DbSet<MaterialRequest> MaterialRequests { get; set; }
        public DbSet<BillPaymentDetail> BillPaymentDetails { get; set; }
        public DbSet<MonthlyBillItem> MonthlyBillItems { get; set; }
        public DbSet<MonthlyBill> MonthlyBills { get; set; }
        public DbSet<AffiliateBankDetail> AffiliateBankDetails { get; set; }
        public DbSet<OrganizationMaster> OrganizationMasters { get; set; }
        public DbSet<Volunteer> Volunteers { get; set; }
        public DbSet<AffiliateLeave> AffiliateLeaves { get; set; }
        public DbSet<AffiliateOtherActivity> AffiliateOtherActivities { get; set; }
        public DbSet<AffiliateExpense> AffiliateExpenses { get; set; }
        public DbSet<VolunteerInstituteMapping> VolunteerInstituteMappings { get; set; }

        public DbSet<KnowledgePartnersList> KnowledgePartnersList { get; set; }
        public DbSet<Quiz> Quiz { get; set; }
        public DbSet<Examination> Examination { get; set; }
        public DbSet<ExaminationRecords> ExaminationRecords { get; set; }
        public DbSet<Questions> Questions { get; set; }
        public DbSet<Topics> Topics { get; set; }
        public DbSet<Courses> Courses { get; set; }
        public DbSet<QuizCourseLevels> QuizCourseLevels { get; set; }
        public virtual DbSet<WJ_Tech_Teacher_Questions> WJ_Tech_Teacher_Questions { get; set; }
        public virtual DbSet<Levels> Levels { get; set; }
        public virtual DbSet<View_ExamResult> View_ExamResult { get; set; }
        public virtual DbSet<AnubhutiMaster> AnubhutiMaster { get; set; }
        public virtual DbSet<AnubhutiTransaction> AnubhutiTransaction { get; set; }
        public virtual DbSet<WJ_Coins> WJ_Coins { get; set; }
        public virtual DbSet<WJ_Practicals> WJ_Practicals { get; set; }
        public virtual DbSet<WJ_PracticalSubmission> WJ_PracticalSubmission { get; set; }
        public virtual DbSet<V_HF_Anubhuti_ImageQuestionBind> V_HF_Anubhuti_ImageQuestionBind { get; set; }
        public virtual DbSet<WJ_ScrambleQuestion> WJ_ScrambleQuestion { get; set; }
        public virtual DbSet<WJ_ScrambleAnswer> WJ_ScrambleAnswer { get; set; }
        public virtual DbSet<SP_ScrambleWord_Result> SP_ScrambleWord_Results { get; set; }
        public virtual DbSet<Users> Users { get; set; }
        public virtual DbSet<AspNetUsers> AspNetUsers { get; set; }
        public DbSet<QuestionOption> QuestionOption { get; set; }

        public DbSet<View_WJ_Course_Topics> View_WJ_Course_Topics { get; set; }

        public DbSet<VolunteerVisitPlan> VolunteerVisitPlans { get; set; }
        public DbSet<BulkUploadLog> BulkUploadLogs { get; set; }
        public DbSet<ValueGradeMaster> ValueGradeMasters { get; set; }
        public DbSet<ValueItemMaster> ValueItemMasters { get; set; }
        public DbSet<vw_valueitemmasters> vw_valueitemmasters { get; set; }

        public DbSet<HfRelManageGroupList> HfRelManageGroupList {  get; set; }
        public DbSet<CreativityActivitiesSubmission2021> creativityActivitiesSubmission2021s { get; set; }


        public DbSet<ActivityGroup> ActivityGroups { get; set; }
        public DbSet<ActivityGroupStudent> ActivityGroupStudents { get; set; }
        public DbSet<ActivitySubmission> ActivitySubmissions { get; set; }
        public DbSet<TrainingGuideMaster> TrainingGuideMasters { get; set; }
        public DbSet<StudentCourseMaster> StudentCourseMasters { get; set; }
        public DbSet<StudentCourseModule> StudentCourseModules { get; set; }
        public DbSet<StudentModuleContent> StudentModuleContents { get; set; }

        public DbSet<Game24> WJ_Game24 { get; set; }
        public DbSet<Game24Solution> Game24Solution { get; set; }


        public DbSet<WJ_ReferralCodes> WJ_ReferralCodes { get; set; }
        public DbSet<SchoolProfileDetailsDto> SchoolProfileDetails { get; set; }
        public DbSet<ViewHfTeacherDetails> ViewHfTeacherDetails { get; set; }
        public DbSet<WJ_StudentAssociates> WJ_StudentAssociates { get; set; }
        public DbSet<WJ_Students> WJ_Students { get; set; }
        public DbSet<WJ_TeacherAssociates> WJ_TeacherAssociates { get; set; }
        public DbSet<CertifiedStudentsDto> CertifiedStudentsDtos { get; set; }

        public DbSet<TeacherActivityLog> TeacherActivityLogs { get; set; }
        public DbSet<ValueCertification> ValueCertifications { get; set; }
        public DbSet<StudentActivityLog> StudentActivityLogs { get; set; }

        public DbSet<View_HF_Affiliate> View_HF_Affiliate { get; set; }
        public DbSet<HfViewUser> HfViewUsers { get; set; }
        public DbSet<HfTblAffliateFocusedList> HfTblAffliateFocusedLists { get; set; }

        public DbSet<MasterSetting> mastersettings { get; set; }
        public DbSet<DailySamparkEntry> dailysamparkentries { get; set; }
        public DbSet<DailyQuestion> dailyquestions { get; set; }
        public DbSet<ProjectEvaluation> ProjectEvaluations { get; set; }
        public DbSet<HfUploadAnubhuti> HfUploadAnubhuti { get; set; }
        public DbSet<HfTblUploadIndividualProject> HfTblUploadIndividualProject { get; set; }
        public DbSet<VwUserRoleDetails> VwUserRoleDetails { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<VolunteerVisitPlan>(entity =>
            {
                entity.ToTable("VolunteerVisitPlans", "public"); 

                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.VolunteerId).HasColumnName("VolunteerId");
                entity.Property(e => e.ScheduleDate).HasColumnName("ScheduleDate");

                entity.Property(e => e.visitdate).HasColumnName("visitdate");
            });
            builder.Entity<MasterSetting>()
        .ToTable("mastersettings");

            builder.Entity<DailySamparkEntry>()
                .ToTable("dailysamparkentries");

            builder.Entity<DailyQuestion>()
                .ToTable("dailyquestions");


            // DailySamparkEntry -> DailyQuestions
            builder.Entity<DailyQuestion>()
                .HasOne(q => q.DailySamparkEntry)
                .WithMany(e => e.Questions)
                .HasForeignKey(q => q.DailyEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RoleMenuRight>().HasIndex(x => new { x.RoleId, x.MenuId }).IsUnique();
            builder.Entity<HfViewUser>().HasNoKey().ToView("hf_view_users");
            var seedDate = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc);

            builder.Entity<QuizCourseLevels>().HasNoKey();
            builder.Entity<View_HF_Affiliate>()
    .HasNoKey()
    .ToView("view_hf_affiliate");
            builder.Entity<SchoolProfileDetailsDto>(entity =>
            {
                entity.HasNoKey();

                entity.ToView("vw_school_profile_details", "public");

                entity.Property(e => e.instituteid)
                    .HasColumnName("instituteid");

                entity.Property(e => e.institutetype)
                    .HasColumnName("institutetype");

                entity.Property(e => e.institute_name)
                    .HasColumnName("institute_name");

                entity.Property(e => e.profileurl)
                    .HasColumnName("profileurl");

                entity.Property(e => e.institute_isactive)
                    .HasColumnName("institute_isactive");

                entity.Property(e => e.referralcode)
                    .HasColumnName("referralcode");

                entity.Property(e => e.isverify)
                    .HasColumnName("isverify");

                entity.Property(e => e.firstname)
                    .HasColumnName("firstname");

                entity.Property(e => e.lastname)
                    .HasColumnName("lastname");

                entity.Property(e => e.mobileno)
                    .HasColumnName("mobileno");

                entity.Property(e => e.emailid)
                    .HasColumnName("emailid");

                entity.Property(e => e.addressline1)
                    .HasColumnName("addressline1");

                entity.Property(e => e.addressline2)
                    .HasColumnName("addressline2");

                entity.Property(e => e.addressline3)
                    .HasColumnName("addressline3");

                entity.Property(e => e.cityname)
                    .HasColumnName("cityname");

                entity.Property(e => e.pincode)
                    .HasColumnName("pincode");

                entity.Property(e => e.photourl)
                    .HasColumnName("photourl");

                entity.Property(e => e.user_isactive)
                    .HasColumnName("user_isactive");

                entity.Property(e => e.createdby)
                    .HasColumnName("createdby");

                entity.Property(e => e.createddate)
                    .HasColumnName("createddate");

                entity.Property(e => e.modifiedby)
                    .HasColumnName("modifiedby");

                entity.Property(e => e.modifieddate)
                    .HasColumnName("modifieddate");

                entity.Property(e => e.aspnetuserid)
                    .HasColumnName("aspnetuserid");

                entity.Property(e => e.password_)
                    .HasColumnName("password_");

                entity.Property(e => e.associatedstudents)
                    .HasColumnName("associatedstudents");

                entity.Property(e => e.activestudents)
                    .HasColumnName("activestudents");

                entity.Property(e => e.totalteachers)
                    .HasColumnName("totalteachers");

                entity.Property(e => e.activeteachers)
                    .HasColumnName("activeteachers");
                entity.Property(e => e.emailid)
                    .HasColumnName("emailid");
                entity.Property(e => e.username)
                    .HasColumnName("username");
            });

            // Identity Configurations
            builder.Entity<IdentityRole>(entity =>
            {
                entity.ToTable("aspnetroles", "public");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id");

                entity.Property(e => e.Name)
                    .HasColumnName("Name");
                entity.Ignore(e => e.NormalizedName);
                entity.Ignore(e => e.ConcurrencyStamp);


            });

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.ToTable("aspnetusers", "public");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserName).HasColumnName("username");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.PasswordHash).HasColumnName("passwordhash");
                entity.Property(e => e.SecurityStamp).HasColumnName("securitystamp");
                entity.Property(e => e.PhoneNumber).HasColumnName("phonenumber");
                entity.Property(e => e.LockoutEnd).HasColumnName("lockoutenddateutc");
                entity.Property(e => e.AccessFailedCount).HasColumnName("accessfailedcount");
                entity.Property(e => e.EmailConfirmed).HasColumnName("emailconfirmed");
                entity.Property(e => e.PhoneNumberConfirmed).HasColumnName("phonenumberconfirmed").HasDefaultValue(false);
                entity.Ignore(e => e.TwoFactorEnabled);
                entity.Ignore(e => e.LockoutEnabled);
                entity.Ignore(e => e.NormalizedUserName);
                entity.Ignore(e => e.NormalizedEmail);
                entity.Ignore(e => e.ConcurrencyStamp);
                entity.Ignore(e => e.CreatedDate);
                entity.Ignore(e => e.FullName);
                entity.Ignore(e => e.PortalName);
                entity.Ignore(e => e.UserCode);
            });

            builder.Entity<IdentityUserRole<string>>(entity =>
            {
                entity.ToTable("aspnetuserroles", "public");
                entity.HasKey(r => new { r.UserId, r.RoleId });
                entity.Property(e => e.UserId).HasColumnName("userid");
                entity.Property(e => e.RoleId).HasColumnName("roleid");
            });

            builder.Entity<IdentityUserClaim<string>>(entity =>
            {
                entity.ToTable("aspnetuserclaims", "public");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.UserId).HasColumnName("userid");
                entity.Property(e => e.ClaimType).HasColumnName("claimtype");
                entity.Property(e => e.ClaimValue).HasColumnName("claimvalue");
            });

            builder.Entity<IdentityUserLogin<string>>(entity =>
            {
                entity.ToTable("aspnetuserlogins", "public");
                entity.HasKey(l => new { l.LoginProvider, l.ProviderKey, l.UserId });
                entity.Property(e => e.LoginProvider).HasColumnName("loginprovider");
                entity.Property(e => e.ProviderKey).HasColumnName("providerkey");
                entity.Property(e => e.UserId).HasColumnName("userid");
            });

            builder.Entity<IdentityUserToken<string>>().ToTable("aspnetusertokens", "public");
            builder.Entity<IdentityRoleClaim<string>>().ToTable("aspnetroleclaims", "public");

            builder.Entity<Users>(entity =>
            {
                entity.ToTable("users", "public");
                entity.HasKey(e => e.UserID);
                entity.Property(e => e.AspNetUserID).HasColumnName("aspnetuserid");

                entity.HasOne(e => e.AspNetUsers)
                      .WithMany(u => u.Users)
                      .HasForeignKey(e => e.AspNetUserID);

                // 1. IsActive
                entity.Property(e => e.IsActive)
                      .HasColumnName("isactive")
                      .HasColumnType("bit(1)")
                      .HasConversion(
                          v => v.HasValue ? new System.Collections.BitArray(new bool[] { v.Value }) : null,
                          v => v != null && v.Count > 0 ? v.Get(0) : (bool?)null
                      );

                // 2. IsMobileVerified
                entity.Property(e => e.IsMobileVerified)
                      .HasColumnName("ismobileverified")
                      .HasColumnType("bit(1)")
                      .HasConversion(
                          v => v.HasValue ? new System.Collections.BitArray(new bool[] { v.Value }) : null,
                          v => v != null && v.Count > 0 ? v.Get(0) : (bool?)null
                      );

                // 3. IsEmailVerified
                entity.Property(e => e.IsEmailVerified)
                      .HasColumnName("isemailverified")
                      .HasColumnType("bit(1)")
                      .HasConversion(
                          v => v.HasValue ? new System.Collections.BitArray(new bool[] { v.Value }) : null,
                          v => v != null && v.Count > 0 ? v.Get(0) : (bool?)null
                      );

                // 4. isUserDeactivated
                entity.Property(e => e.isUserDeactivated)
                      .HasColumnName("isuserdeactivated")
                      .HasColumnType("bit(1)")
                      .HasConversion(
                          v => v.HasValue ? new System.Collections.BitArray(new bool[] { v.Value }) : null,
                          v => v != null && v.Count > 0 ? v.Get(0) : (bool?)null
                      );
            });

            builder.Entity<Quiz>(entity =>
            {
                entity.ToTable("quiz");
                entity.Property(e => e.QuizID).HasColumnName("quizid");
            });

            builder.Entity<QuizCourseLevels>(entity =>
            {
                entity.ToTable("quizcourselevels");
                entity.Property(e => e.QuizID).HasColumnName("quizid");
                entity.Property(e => e.CourseID).HasColumnName("courseid");
            });

            builder.Entity<Examination>(entity =>
            {
                entity.ToTable("examination");
                entity.Ignore("Levels");
                entity.Property(e => e.ExaminationID).HasColumnName("examinationid").UseSerialColumn();
                entity.Property(e => e.IsPassed)
                      .HasColumnName("ispassed")
                      .HasConversion(
                          v => new System.Collections.BitArray(new[] { v }),
                          v => v != null && v[0]
                      );
            });

            builder.Entity<Questions>(entity =>
            {
                entity.ToTable("questions");

                entity.Property(e => e.QuestionID).HasColumnName("questionid");
                entity.Property(e => e.QuestionName).HasColumnName("questionname");
                entity.Property(e => e.Marks).HasColumnName("marks");
                entity.Property(e => e.Type).HasColumnName("questiontype");
                entity.Property(e => e.ComplexityLevel).HasColumnName("complexitylevel");
                entity.Property(e => e.AnswerDescription).HasColumnName("answerdescription");
                entity.Property(e => e.CourseID).HasColumnName("courseid");
                entity.Property(e => e.TopicID).HasColumnName("topicid");
                entity.Property(e => e.SubTopicID).HasColumnName("subtopicid");
                entity.Property(e => e.QuestionLevel).HasColumnName("questionlevel");
                entity.Property(e => e.languageID).HasColumnName("languageid");

                entity.Property(e => e.Status)
                      .HasColumnName("status")
                      .HasConversion(
                          v => new System.Collections.BitArray(new[] { v }),
                          v => v != null && v[0]
                      );

                entity.Property(e => e.isAdminStatus)
                      .HasColumnName("isadminstatus")
                      .HasConversion(
                          v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                          v => v != null ? (bool?)v[0] : null
                      );

                entity.Property(e => e.isDeleted)
                      .HasColumnName("isdeleted")
                      .HasConversion(
                          v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                          v => v != null ? (bool?)v[0] : null
                      );

                entity.Ignore(q => q.Courses1);

                entity.HasOne(q => q.Courses)
                      .WithMany()
                      .HasForeignKey(q => q.CourseID);

                entity.HasOne(q => q.Topics)
                      .WithMany(t => t.Questions)
                      .HasForeignKey(q => q.TopicID)
                      .IsRequired(false);

                entity.HasMany(q => q.QuestionOption)
                      .WithOne(o => o.Questions)
                      .HasForeignKey(o => o.QuestionID);
            });

            builder.Entity<ExaminationRecords>(entity =>
            {
                entity.ToTable("examinationrecords");

                entity.Property(e => e.RecordID).HasColumnName("recordid").UseSerialColumn();
                entity.Property(e => e.ExaminationID).HasColumnName("examinationid");
                entity.Property(e => e.QuestionID).HasColumnName("questionid");
                entity.Property(e => e.RecordStatus).HasColumnName("recordstatus");
                entity.Property(e => e.Answer).HasColumnName("answer");
                entity.Property(e => e.SubmitTiming).HasColumnName("submittiming");
                entity.Property(e => e.CreatedBy).HasColumnName("createdby");
                entity.Property(e => e.CreatedDate).HasColumnName("createddate");
                entity.Property(e => e.ModifiedBy).HasColumnName("modifiedby");
                entity.Property(e => e.ModifiedDate).HasColumnName("modifieddate");
                entity.Property(e => e.OrderNo).HasColumnName("orderno");

                entity.Property(e => e.isCorrect)
                      .HasColumnName("iscorrect")
                      .HasConversion(
                          v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                          v => v != null ? (bool?)v[0] : null
                      );

                entity.HasOne(e => e.Examination)
                      .WithMany(ex => ex.ExaminationRecords)
                      .HasForeignKey(e => e.ExaminationID);

                entity.HasOne(e => e.Questions)
                      .WithMany(q => q.ExaminationRecords)
                      .HasForeignKey(e => e.QuestionID);
            });

            builder.Entity<Courses>(entity =>
            {
                entity.ToTable("courses");
                entity.Ignore("Quiz");
            });

            builder.Entity<Levels>().ToTable("levels");

            builder.Entity<QuestionOption>(entity =>
            {
                entity.ToTable("questionoption");
                entity.HasKey(e => e.OptionID);
                entity.Property(e => e.OptionID).HasColumnName("optionid");
                entity.Property(e => e.QuestionID).HasColumnName("questionid");
                entity.Property(e => e.Options).HasColumnName("Options");


                entity.Ignore(e => e.ExaminationOptions);
                entity.Ignore(e => e.ExaminationOptions1);
                entity.Ignore("ExaminationRecords");

                entity.Property(e => e.IsAnswer)
                      .HasColumnName("isanswer")
                      .HasConversion(
                          v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                          v => v != null ? (bool?)v[0] : null
                      );
            });
            builder.Entity<Topics>(entity =>
            {
                entity.ToTable("topics");

                entity.HasKey(e => e.TopicID);

                entity.Property(e => e.TopicID)
                    .HasColumnName("topicid");

                entity.Property(e => e.CourseID)
                    .HasColumnName("courseid");

                entity.Property(e => e.ParentTopicID)
                    .HasColumnName("parenttopicid");

                entity.Property(e => e.TopicName)
                    .HasColumnName("topicname");

                entity.Property(e => e.Description)
                    .HasColumnName("description");

                entity.Property(e => e.TrainerVideo)
                    .HasColumnName("trainervideo");

                entity.Property(e => e.StudentVideo)
                    .HasColumnName("studentvideo");

                entity.Property(e => e.HindiVideo)
                    .HasColumnName("hindivideo");

                entity.Property(e => e.TrainerNote)
                    .HasColumnName("trainernote");

                entity.Property(e => e.StudentNote)
                    .HasColumnName("studentnote");

                entity.Property(e => e.Order)
                    .HasColumnName("Order");

                entity.Property(e => e.IsActive)
                    .HasColumnName("isactive");

                entity.Property(e => e.CreatedBy)
                    .HasColumnName("createdby");

                entity.Property(e => e.CreatedDate)
                    .HasColumnName("createddate");

                entity.Property(e => e.ModifiedBy)
                    .HasColumnName("modifiedby");

                entity.Property(e => e.ModifiedDate)
                    .HasColumnName("modifieddate");

                entity.Property(e => e.TrainerResource)
                    .HasColumnName("trainerresource");

                entity.Property(e => e.StudentResourse)
                    .HasColumnName("studentresourse");

                entity.Property(e => e.TrainerExercise)
                    .HasColumnName("trainerexercise");

                entity.Property(e => e.StudentExercise)
                    .HasColumnName("studentexercise");

                entity.Property(e => e.languageID)
                    .HasColumnName("languageid");

                entity.Property(e => e.NoOfQuestion)
                    .HasColumnName("noofquestion");
            });
           
            builder.Entity<View_WJ_Course_Topics>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("view_wj_course_topics"); // Match your exact lowercase view name in PostgreSQL
            });

            builder.Entity<WJ_PracticalSubmission>(entity =>
            {
                entity.Property(e => e.IsDelete)
                      .HasColumnName("isdelete")
                      .HasColumnType("bit(1)") // Matches the database column type
                      .HasConversion(
                          v => v.HasValue ? new BitArray(new bool[] { v.Value }) : null, // bool? -> BitArray
                          v => v != null && v.Count > 0 ? v.Get(0) : (bool?)null          // BitArray -> bool?
                      );
            });
           

            builder.Entity<ActivityTypeMaster>(entity =>
            {
                entity.ToTable("ActivityTypeMasters", "public");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("Id");

                entity.Property(e => e.ActivityName)
                    .HasColumnName("ActivityName");

                entity.Property(e => e.IsActive)
                    .HasColumnName("IsActive");

                entity.Property(e => e.CreatedDate)
                    .HasColumnName("CreatedDate");

                entity.Property(e => e.ModifiedDate)
                    .HasColumnName("ModifiedDate");

                entity.Property(e => e.IsDeleted)
                    .HasColumnName("IsDeleted");
            });
            builder.Entity<ViewWjStudentsDetailsInfo>(entity =>
            {
                entity.Property(e => e.IsActive).HasColumnType("boolean");
                entity.Property(e => e.IsPaid).HasColumnType("boolean");
                entity.Property(e => e.SharingPermission).HasColumnType("boolean");
                entity.Property(e => e.IsMobileVerified).HasColumnType("boolean");
                entity.Property(e => e.IsEmailVerified).HasColumnType("boolean");
                entity.Property(e => e.IsUserDeactivated).HasColumnType("boolean");
            });
           
            builder.Entity<WJ_Students>(entity =>
            {
               
                entity.Property(e => e.IsActive)
                      .HasColumnType("bit")
                      .HasConversion(
                          v => v.HasValue ? new BitArray(new[] { v.Value }) : null,
                          v => v != null && v.Count > 0 ? v[0] : (bool?)null
                      );

                entity.Property(e => e.IsPaid)
                      .HasColumnType("bit")
                      .HasConversion(
                          v => new BitArray(new[] { v }),
                          v => v != null && v.Count > 0 ? v[0] : false
                      );

                entity.Property(e => e.SharingPermission)
                      .HasColumnType("bit")
                      .HasConversion(
                          v => v.HasValue ? new BitArray(new[] { v.Value }) : null,
                          v => v != null && v.Count > 0 ? v[0] : (bool?)null
                      );
            });
            builder.Entity<WJ_StudentAssociates>(entity =>
            {
                entity.Property(e => e.IsPrimaryAssociate)
                      .HasColumnType("bit")
                      .HasConversion(
                          v => v.HasValue ? new BitArray(new[] { v.Value }) : null,
                          v => v != null && v.Count > 0 ? v[0] : (bool?)null
                      );
            });
            builder.Entity<WJ_TeacherAssociates>(entity =>
            {
                entity.Property(e => e.IsPrimaryAssociate)
                      .HasColumnType("bit")
                      .HasConversion(
                          v => v.HasValue ? new BitArray(new[] { v.Value }) : null,
                          v => v != null && v.Count > 0 ? v[0] : (bool?)null
                      );
            });

            builder.Entity<HfRelManageGroupList>(entity =>
            {
                entity.Property(e => e.IsAllocated)
                      .HasColumnType("bit")
                      .HasConversion(
                          v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                          v => v != null && v.Count > 0 ? v[0] : (bool?)null
                      );
            });
            builder.Entity<PblReportTeacherDto>().HasNoKey();
       
            builder.Entity<TeacherActivityLog>().HasNoKey();
            builder.Entity<ValueCertification>(entity =>
            {
                entity.ToTable("wj_value_certification");
               
            });
            builder.Entity<StudentActivityLog>().HasNoKey();

            builder.Entity<WJ_Institutes>()
                .Property(e => e.IsActive)
                .HasConversion(
                    v => new BitArray(new[] { v }),       
                    v => v != null && v.Count > 0 && v[0]
                );

            builder.Entity<WJ_Institutes>()
                .Property(e => e.IsVerify)
                .HasConversion(
                    v => new BitArray(new[] { v }),       
                    v => v != null && v.Count > 0 && v[0] 
                );
            builder.Entity<VwUserRoleDetails>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("vw_user_role_details", "public");

                entity.Property(e => e.AspnetUserId).HasColumnName("aspnet_user_id");
                entity.Property(e => e.Username).HasColumnName("username");
                entity.Property(e => e.LoginEmail).HasColumnName("login_email");

                entity.Property(e => e.Userid).HasColumnName("userid");
                entity.Property(e => e.Usercode).HasColumnName("usercode");
                entity.Property(e => e.Title).HasColumnName("title");
                entity.Property(e => e.Firstname).HasColumnName("firstname");
                entity.Property(e => e.Lastname).HasColumnName("lastname");
                entity.Property(e => e.FullName).HasColumnName("full_name");
                entity.Property(e => e.Gender).HasColumnName("gender");
                entity.Property(e => e.Maritalstatus).HasColumnName("maritalstatus");
                entity.Property(e => e.Dob).HasColumnName("dob");
                entity.Property(e => e.Mobileno).HasColumnName("mobileno");
                entity.Property(e => e.Phoneno).HasColumnName("phoneno");
                entity.Property(e => e.Emailid).HasColumnName("emailid");

                entity.Property(e => e.Addressline1).HasColumnName("addressline1");
                entity.Property(e => e.Addressline2).HasColumnName("addressline2");
                entity.Property(e => e.Addressline3).HasColumnName("addressline3");

                entity.Property(e => e.City).HasColumnName("city");
                entity.Property(e => e.Cityname).HasColumnName("cityname");
                entity.Property(e => e.Pincode).HasColumnName("pincode");
                entity.Property(e => e.Photourl).HasColumnName("photourl");

                entity.Property(e => e.UserIsActive)
                    .HasColumnName("user_is_active")
                    .HasColumnType("bit(1)")
                    .HasConversion(
                        v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                        v => v != null && v.Count > 0 ? v[0] : null
                    );

                entity.Property(e => e.Isuserdeactivated)
                    .HasColumnName("isuserdeactivated")
                    .HasColumnType("bit(1)")
                    .HasConversion(
                        v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                        v => v != null && v.Count > 0 ? v[0] : null
                    );

                entity.Property(e => e.Ismobileverified)
                    .HasColumnName("ismobileverified")
                    .HasColumnType("bit(1)")
                    .HasConversion(
                        v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                        v => v != null && v.Count > 0 ? v[0] : null
                    );

                entity.Property(e => e.Isemailverified)
                    .HasColumnName("isemailverified")
                    .HasColumnType("bit(1)")
                    .HasConversion(
                        v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                        v => v != null && v.Count > 0 ? v[0] : null
                    );

                entity.Property(e => e.Languagepreference).HasColumnName("languagepreference");
                entity.Property(e => e.UserCreatedDate).HasColumnName("user_created_date");
                entity.Property(e => e.UserModifiedDate).HasColumnName("user_modified_date");
                entity.Property(e => e.Registerfrom).HasColumnName("registerfrom");

                entity.Property(e => e.RoleId).HasColumnName("role_id");
                entity.Property(e => e.RoleName).HasColumnName("role_name");

                entity.Property(e => e.Studentid).HasColumnName("studentid");
                entity.Property(e => e.StudentStandard).HasColumnName("student_standard");
                entity.Property(e => e.StudentDivision).HasColumnName("student_division");
                entity.Property(e => e.Gardianname).HasColumnName("gardianname");
                entity.Property(e => e.StudentReferralcode).HasColumnName("student_referralcode");
                entity.Property(e => e.Registrationamount).HasColumnName("registrationamount");
                entity.Property(e => e.StudentProfileurl).HasColumnName("student_profileurl");

                entity.Property(e => e.StudentIsActive)
                    .HasColumnName("student_is_active")
                    .HasColumnType("bit(1)")
                    .HasConversion(
                        v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                        v => v != null && v.Count > 0 ? v[0] : null
                    );

                entity.Property(e => e.Ispaid)
                    .HasColumnName("ispaid")
                    .HasColumnType("bit(1)")
                    .HasConversion(
                        v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                        v => v != null && v.Count > 0 ? v[0] : null
                    );

                entity.Property(e => e.Trialperiodtill).HasColumnName("trialperiodtill");
                entity.Property(e => e.Rollno).HasColumnName("rollno");

                entity.Property(e => e.Sharingpermission)
                    .HasColumnName("sharingpermission")
                    .HasColumnType("bit(1)")
                    .HasConversion(
                        v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                        v => v != null && v.Count > 0 ? v[0] : null
                    );

                entity.Property(e => e.Levelpath).HasColumnName("levelpath");
                entity.Property(e => e.Guardianemailid).HasColumnName("guardianemailid");
                entity.Property(e => e.Guardianmobileno).HasColumnName("guardianmobileno");

                entity.Property(e => e.Teacherid).HasColumnName("teacherid");
                entity.Property(e => e.TeacherStandard).HasColumnName("teacher_standard");
                entity.Property(e => e.TeacherDivision).HasColumnName("teacher_division");
                entity.Property(e => e.TeacherProfileurl).HasColumnName("teacher_profileurl");

                entity.Property(e => e.Instituteid).HasColumnName("instituteid");
                entity.Property(e => e.Institutetype).HasColumnName("institutetype");
                entity.Property(e => e.InstituteName).HasColumnName("institute_name");
                entity.Property(e => e.Bannerimageurl).HasColumnName("bannerimageurl");
                entity.Property(e => e.InstituteProfileurl).HasColumnName("institute_profileurl");

                entity.Property(e => e.InstituteIsActive)
                    .HasColumnName("institute_is_active")
                    .HasColumnType("bit(1)")
                    .HasConversion(
                        v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                        v => v != null && v.Count > 0 ? v[0] : null
                    );

                entity.Property(e => e.InstituteReferralcode).HasColumnName("institute_referralcode");

                entity.Property(e => e.Isverify)
                    .HasColumnName("isverify")
                    .HasColumnType("bit(1)")
                    .HasConversion(
                        v => v.HasValue ? new System.Collections.BitArray(new[] { v.Value }) : null,
                        v => v != null && v.Count > 0 ? v[0] : null
                    );

                entity.Property(e => e.Profession).HasColumnName("profession");
                entity.Property(e => e.Socialorgname).HasColumnName("socialorgname");
                entity.Property(e => e.Typeoforganisation).HasColumnName("typeoforganisation");

                entity.Property(e => e.VolunteerId).HasColumnName("volunteer_id");
                entity.Property(e => e.VolunteerFirstName).HasColumnName("volunteer_first_name");
                entity.Property(e => e.VolunteerLastName).HasColumnName("volunteer_last_name");
                entity.Property(e => e.VolunteerMobile).HasColumnName("volunteer_mobile");
                entity.Property(e => e.VolunteerEmail).HasColumnName("volunteer_email");
                entity.Property(e => e.VolunteerInstituteId).HasColumnName("volunteer_institute_id");
                entity.Property(e => e.VolunteerAffiliateId).HasColumnName("volunteer_affiliate_id");
                entity.Property(e => e.VolunteerCreatedDate).HasColumnName("volunteer_created_date");
                entity.Property(e => e.VolunteerModifiedDate).HasColumnName("volunteer_modified_date");

                entity.Property(e => e.VolunteerIsActive)
                    .HasColumnName("volunteer_is_active");

                entity.Property(e => e.VolunteerIsDeleted)
                    .HasColumnName("volunteer_is_deleted");
            });

        }


    }
}