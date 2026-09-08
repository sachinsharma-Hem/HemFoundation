using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HemFoundation.Models.Entities
{
    // Base class for audit fields
    public class BaseMasterEntity
    {
        [Key]
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
    }

    // 1. Main Course
    public class MainCourse : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // 2. Category
    public class CourseCategory : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // 3. SubCategory
    public class CourseSubCategory : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // 4. Display Order / Order Number
    public class CourseOrderNumber : BaseMasterEntity
    {
        [Required] public int DisplayOrder { get; set; }
    }

    // 5. Years
    public class CourseYear : BaseMasterEntity
    {
        [Required, StringLength(50)] public string Years { get; set; } = string.Empty;
        public string? Remark { get; set; }
    }

    // 6. Author
    public class CourseAuthor : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // 7. Manager
    public class CourseManager : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // 8. Courses For
    public class CourseFor : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // 9. Customer Type
    public class CourseCustomerType : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // 10. Duration Type
    public class CourseDurationType : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // 11. Course Alloted To
    public class CourseAllotedTo : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // 12. MCQ Level
    public class CourseMcqLevel : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // 13. Levels
    public class CourseLevel : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Years { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    // 14. Webinar MCQ Category (Includes Image & Lock/Unlock Status)
    public class WebinarMcqCategory : BaseMasterEntity
    {
        [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsLocked { get; set; } = false; // Status: Lock / Unlock
        // Newly added Display Order Foreign Key Relationship
        public int? OrderNumberId { get; set; }
        [ForeignKey("OrderNumberId")]
        public virtual CourseOrderNumber? OrderNumber { get; set; }
        // --- Yeh line add kar dein taaki EF Core 'IsDeleted' ko SQL result me na dhoonde ---
        [NotMapped]
        public string? IsDeleted { get; set; }
    }
}