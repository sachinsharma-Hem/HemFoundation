using HemFoundation.Models.Entities;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser> 
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Admin> Admins { get; set; }
        public DbSet<PortalMaster> PortalMasters { get; set; }
        public DbSet<GenderMaster> GenderMasters { get; set; } // Added Gender Master Table
        public DbSet<Menu> Menus { get; set; }
        public DbSet<RoleMenuRight> RoleMenuRights { get; set; }
        public DbSet<RoleDashboardMapping> RoleDashboardMappings { get; set; }
        public DbSet<Affiliate> Affiliates { get; set; }
        public DbSet<Institute> Institutes { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<StandardMaster> StandardMasters { get; set; }

        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<Parent> Parents { get; set; }
        public DbSet<MainCourse> MainCourses { get; set; }
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
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RoleMenuRight>().HasIndex(x => new { x.RoleId, x.MenuId }).IsUnique();

            var seedDate = DateTime.SpecifyKind(
    new DateTime(2026, 1, 1),
    DateTimeKind.Utc);

            builder.Entity<PortalMaster>().HasData(
                new PortalMaster { Id = 1, PortalName = "Admin", Description = "System Administrator Portal", IsActive = true, CreatedOn = seedDate },
                new PortalMaster { Id = 2, PortalName = "Teacher", Description = "Teacher / Educator Portal", IsActive = true, CreatedOn = seedDate },
                new PortalMaster { Id = 3, PortalName = "Student", Description = "Student Learning Portal", IsActive = true, CreatedOn = seedDate },
                new PortalMaster { Id = 4, PortalName = "School", Description = "School Management Portal", IsActive = true, CreatedOn = seedDate },
                new PortalMaster { Id = 5, PortalName = "Parent", Description = "Parent Monitoring Portal", IsActive = true, CreatedOn = seedDate },
                new PortalMaster { Id = 6, PortalName = "Affiliate", Description = "Affiliate Partner Portal", IsActive = true, CreatedOn = seedDate },
                new PortalMaster { Id = 7, PortalName = "Volunteer", Description = "Volunteer Activity Portal", IsActive = true, CreatedOn = seedDate },
                new PortalMaster { Id = 8, PortalName = "TesterEmployee", Description = "Internal Testing & Employee Portal", IsActive = true, CreatedOn = seedDate }
            );

            builder.Entity<RoleDashboardMapping>().HasData(
                new RoleDashboardMapping { Id = 1, RoleName = "Admin", RedirectUrl = "/Admin/Dashboard", IsActive = true },
                new RoleDashboardMapping { Id = 2, RoleName = "Teacher", RedirectUrl = "/Teacher/Dashboard", IsActive = true },
                new RoleDashboardMapping { Id = 3, RoleName = "Student", RedirectUrl = "/Student/Dashboard", IsActive = true },
                new RoleDashboardMapping { Id = 4, RoleName = "TesterEmployee", RedirectUrl = "/TesterEmployee/Dashboard", IsActive = true },
                new RoleDashboardMapping { Id = 5, RoleName = "School", RedirectUrl = "/School/Dashboard", IsActive = true },
                new RoleDashboardMapping { Id = 6, RoleName = "Parent", RedirectUrl = "/Parent/Dashboard", IsActive = true },
                new RoleDashboardMapping { Id = 7, RoleName = "Affiliate", RedirectUrl = "/Affiliate/Dashboard", IsActive = true },
                new RoleDashboardMapping { Id = 8, RoleName = "Volunteer", RedirectUrl = "/Volunteer/Dashboard", IsActive = true }
            );

            // Seed Data for Standards (3rd to 15th)
            builder.Entity<StandardMaster>().HasData(
                new StandardMaster { Id = 1, StandardName = "3rd Standard", DisplayOrder = 1 },
                new StandardMaster { Id = 2, StandardName = "4th Standard", DisplayOrder = 2 },
                new StandardMaster { Id = 3, StandardName = "5th Standard", DisplayOrder = 3 },
                new StandardMaster { Id = 4, StandardName = "6th Standard", DisplayOrder = 4 },
                new StandardMaster { Id = 5, StandardName = "7th Standard", DisplayOrder = 5 },
                new StandardMaster { Id = 6, StandardName = "8th Standard", DisplayOrder = 6 },
                new StandardMaster { Id = 7, StandardName = "9th Standard", DisplayOrder = 7 },
                new StandardMaster { Id = 8, StandardName = "10th Standard", DisplayOrder = 8 },
                new StandardMaster { Id = 9, StandardName = "11th Standard", DisplayOrder = 9 },
                new StandardMaster { Id = 10, StandardName = "12th Standard", DisplayOrder = 10 },
                new StandardMaster { Id = 11, StandardName = "13th Level", DisplayOrder = 11 },
                new StandardMaster { Id = 12, StandardName = "14th Level", DisplayOrder = 12 },
                new StandardMaster { Id = 13, StandardName = "15th Level", DisplayOrder = 13 }
            );
            // 2. SEED DATA FOR GENDERS
            builder.Entity<GenderMaster>().HasData(
                new GenderMaster { Id = 1, GenderName = "Male", IsActive = true },
                new GenderMaster { Id = 2, GenderName = "Female", IsActive = true },
                new GenderMaster { Id = 3, GenderName = "Other", IsActive = true }
            );

        }


    }
}