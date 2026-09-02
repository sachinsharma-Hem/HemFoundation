using DocumentFormat.OpenXml.Office.SpreadSheetML.Y2023.MsForms;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    [Table("courses")] // Maps explicitly to your lowercase table name
    public partial class Courses
    {
        public Courses()
        {
            this.CourseCustomerType = new HashSet<CourseCustomerType>();
            this.CourseCustomerType1 = new HashSet<CourseCustomerType>();
            this.Courses1 = new HashSet<Courses>();
            this.Questions = new HashSet<Questions>();
            this.Questions1 = new HashSet<Questions>();
            this.Topics = new HashSet<Topics>();
            this.QuizCourseLevels = new HashSet<QuizCourseLevels>();
            this.WJ_Practicals = new HashSet<WJ_Practicals>();
            this.CourseCategoryLookup = new HashSet<CourseCategoryLookup>();
            this.Quiz = new HashSet<Quiz>();
        }

        [Key]
        [Column("courseid")]
        public int CourseID { get; set; }

        [Column("coursename")]
        public string CourseName { get; set; }

        [Column("noofsessions")]
        public int NoOfSessions { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("fees")]
        public Nullable<decimal> Fees { get; set; }

        [Column("isactive")]
        public bool IsActive { get; set; }

        [Column("createdby")]
        public int CreatedBy { get; set; }

        [Column("createddate")]
        public System.DateTime CreatedDate { get; set; }

        [Column("modifieddate")]
        public Nullable<System.DateTime> ModifiedDate { get; set; }

        [Column("modifiedby")]
        public Nullable<int> ModifiedBy { get; set; }

        [Column("featuredimage")]
        public string FeaturedImage { get; set; }

        [Column("author")]
        public Nullable<int> Author { get; set; }

        [Column("ispublished")]
        public Nullable<bool> isPublished { get; set; }

        [Column("publishdate")]
        public Nullable<System.DateTime> PublishDate { get; set; }

        [Column("expecteddeliverydate")]
        public Nullable<System.DateTime> ExpectedDeliveryDate { get; set; }

        [Column("approvalremark")]
        public string ApprovalRemark { get; set; }

        [Column("approvalstatus")]
        public string ApprovalStatus { get; set; }

        [Column("approvedon")]
        public Nullable<System.DateTime> ApprovedOn { get; set; }

        [Column("approvedby")]
        public Nullable<int> ApprovedBy { get; set; }

        [Column("trainerresource")]
        public string TrainerResource { get; set; }

        [Column("studentresourse")]
        public string StudentResourse { get; set; }

        [Column("trainerexercise")]
        public string TrainerExercise { get; set; }

        [Column("studentexercise")]
        public string StudentExercise { get; set; }

        [Column("manager")]
        public Nullable<int> Manager { get; set; }

        [Column("courseallocatedto")]
        public string CourseAllocatedTo { get; set; }

        [Column("courseparentid")]
        public Nullable<int> CourseParentID { get; set; }

        [Column("fromage")]
        public Nullable<int> FromAge { get; set; }

        [Column("toage")]
        public Nullable<int> ToAge { get; set; }

        [Column("maincategory")]
        public string MainCategory { get; set; }


        public virtual ICollection<CourseCustomerType> CourseCustomerType { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<CourseCustomerType> CourseCustomerType1 { get; set; }
        
       
        public virtual Users Users { get; set; }
        public virtual Users Users1 { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Courses> Courses1 { get; set; }
        public virtual Courses Courses2 { get; set; }
        public virtual Users Users2 { get; set; }

        [NotMapped]
        public virtual ICollection<Questions> Questions { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]

        [NotMapped]
        public virtual ICollection<Questions> Questions1 { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Topics> Topics { get; set; }

        [NotMapped]
        public virtual ICollection<QuizCourseLevels> QuizCourseLevels { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<WJ_Practicals> WJ_Practicals { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<CourseCategoryLookup> CourseCategoryLookup { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Quiz> Quiz { get; set; }
    }
}
