using HemFoundation.Data;
using HemFoundation.Models.Entities;
using HemFoundation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Route("api/course-hub")]
    [ApiController]
    public class CourseHubApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly IFileStorageService _fileStorage;
        public CourseHubApiController(ApplicationDbContext context, IFileStorageService fileStorage)
        {
            _context = context;
            _fileStorage = fileStorage;
        }

        [HttpGet("dropdowns")]
        public async Task<IActionResult> GetDropdowns()
        {
            try
            {
                return Ok(new
                {
                    success = true,
                    years = await _context.CourseYears.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    mainCourses = await _context.MainCourses.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    categories = await _context.CourseCategories.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    subCategories = await _context.CourseSubCategories.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    orderNumbers = await _context.CourseOrderNumbers.Where(x => !x.IsDeleted && x.IsActive).OrderBy(x => x.DisplayOrder).ToListAsync(),
                    standards = await _context.StandardMasters.Where(x => x.IsActive).ToListAsync(),
                    courses = await _context.Courses.Where(x =>  x.IsActive).ToListAsync(),
                    quizzes = await _context.CourseQuizzes.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    authors = await _context.CourseAuthors.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    managers = await _context.CourseManagers.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    coursesFor = await _context.CourseFors.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    customerTypes = await _context.CourseCustomerTypes.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    durationTypes = await _context.CourseDurationTypes.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    allotedTos = await _context.CourseAllotedTos.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    mcqLevels = await _context.CourseMcqLevels.Where(x => !x.IsDeleted && x.IsActive).ToListAsync(),
                    levels = await _context.CourseLevels.Where(x => !x.IsDeleted && x.IsActive).ToListAsync()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error loading dropdowns: " + ex.Message });
            }
        }

        // --- 1. SYLLABUS ---
        [HttpGet("syllabus/list")]
        public async Task<IActionResult> GetSyllabusList() => Ok(new { success = true, data = await _context.CourseSyllabuses.ToListAsync() });

        //[HttpPost("syllabus/save")]
        //public async Task<IActionResult> SaveSyllabus([FromBody] CourseSyllabus model)
        //{
        //    if (model == null) return BadRequest(new { success = false, message = "Invalid payload." });
        //    try
        //    {
        //        if (model.Id == 0) { model.CreatedDate = DateTime.UtcNow; _context.CourseSyllabuses.Add(model); }
        //        else
        //        {
        //            var item = await _context.CourseSyllabuses.FindAsync(model.Id);
        //            if (item == null) return NotFound(new { success = false, message = "Record not found." });
        //            item.CourseYearId = model.CourseYearId; item.MainCourseId = model.MainCourseId; item.CourseCategoryId = model.CourseCategoryId; item.CourseSubCategoryId = model.CourseSubCategoryId; item.DisplayOrderId = model.DisplayOrderId; item.CourseId = model.CourseId; item.QuizId = model.QuizId; item.StandardMasterId = model.StandardMasterId; item.IsForTeachers = model.IsForTeachers; item.IsForAll = model.IsForAll; item.Remarks = model.Remarks; item.ModifiedDate = DateTime.UtcNow;
        //        }
        //        await _context.SaveChangesAsync();
        //        return Ok(new { success = true, message = "Syllabus saved successfully!" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, message = "Error: " + ex.Message });
        //    }
        //}

        //[HttpPatch("syllabus/toggle/{id}")]
        //public async Task<IActionResult> ToggleSyllabus(int id)
        //{
        //    var item = await _context.CourseSyllabuses.FindAsync(id);
        //    if (item != null) { item.IsActive = !item.IsActive; await _context.SaveChangesAsync(); }
        //    return Ok(new { success = true });
        //}

        //[HttpDelete("syllabus/delete/{id}")]
        //public async Task<IActionResult> DeleteSyllabus(int id)
        //{
        //    var item = await _context.CourseSyllabuses.FindAsync(id);
        //    if (item != null) { item.IsDeleted = true; await _context.SaveChangesAsync(); }
        //    return Ok(new { success = true, message = "Deleted successfully!" });
        //}

        // --- 2. COURSES ---
        [HttpGet("course/list")]
        public async Task<IActionResult> GetCourseList() => Ok(new { success = true, data = await _context.Courses.Where(x => x.IsActive).OrderByDescending(x =>x.CourseID).ToListAsync() });

        //[HttpPost("course/save1")]
        //public async Task<IActionResult> SaveCourse1([FromBody] CourseRegistrationRequest model)
        //{
        //    if (model == null) return BadRequest(new { success = false, message = "Invalid payload." });
        //    try
        //    {
        //        if (model.ExpectedDeliveryDate.Kind == DateTimeKind.Unspecified) model.ExpectedDeliveryDate = DateTime.SpecifyKind(model.ExpectedDeliveryDate, DateTimeKind.Utc);
        //        else model.ExpectedDeliveryDate = model.ExpectedDeliveryDate.ToUniversalTime();

        //        if (model.Id == 0) { model.CreatedDate = DateTime.UtcNow; _context.CourseRegistrations.Add(model); }
        //        else
        //        {
        //            var item = await _context.CourseRegistrations.FindAsync(model.Id);
        //            if (item == null) return NotFound(new { success = false, message = "Record not found." });
        //            item.CourseName = model.CourseName; item.ExpectedDeliveryDate = model.ExpectedDeliveryDate; item.FromAge = model.FromAge; item.ToAge = model.ToAge; item.NumberOfSessions = model.NumberOfSessions; item.CourseAuthorId = model.CourseAuthorId; item.CourseManagerId = model.CourseManagerId; item.MainCourseId = model.MainCourseId; item.CourseForId = model.CourseForId; item.CourseCustomerTypeId = model.CourseCustomerTypeId; item.Description = model.Description; item.ModifiedDate = DateTime.UtcNow;
        //        }
        //        await _context.SaveChangesAsync();
        //        return Ok(new { success = true, message = "Course registered successfully!" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, message = "Error: " + ex.Message });
        //    }
        //}
        [HttpPost("course/save")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> SaveCourse(
    [FromForm] CourseRegistrationRequest model)
        {
            if (model == null)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid payload."
                });

            try
            {
                if (string.IsNullOrWhiteSpace(model.CourseName))
                    return BadRequest(new
                    {
                        success = false,
                        message = "Course name is required."
                    });

                if (model.FeatureImage != null)
                {
                    if (model.FeatureImage.Length == 0)
                        return BadRequest(new
                        {
                            success = false,
                            message = "The selected image is empty."
                        });

                    if (model.FeatureImage.Length > 5 * 1024 * 1024)
                        return BadRequest(new
                        {
                            success = false,
                            message = "Feature image must not exceed 5 MB."
                        });

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var extension = Path.GetExtension(model.FeatureImage.FileName)
                        .ToLowerInvariant();

                    if (!allowedExtensions.Contains(extension))
                        return BadRequest(new
                        {
                            success = false,
                            message = "Only JPG, JPEG, PNG and WEBP images are allowed."
                        });
                }

                Courses item = model.CourseID == 0
                    ? new Courses()
                    : await _context.Courses.FindAsync(model.CourseID);

                if (item == null)
                    return NotFound(new
                    {
                        success = false,
                        message = "Course not found."
                    });

                var isNew = model.CourseID == 0;

                // Normalize the expected delivery date to UTC.
                var deliveryDate = model.ExpectedDeliveryDate.Kind ==
                                   DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(
                        model.ExpectedDeliveryDate, DateTimeKind.Utc)
                    : model.ExpectedDeliveryDate.ToUniversalTime();
                int? userId = HttpContext?.Session?.GetInt32("UserID");
                item.CourseName = model.CourseName.Trim();
                item.ExpectedDeliveryDate = deliveryDate;
                item.FromAge = 3;
                item.ToAge = 60;
                item.NoOfSessions = 1000;
                item.Author = 37;
                item.Manager = 40;
                item.IsActive = true;
                item.ApprovalRemark = "ok";
                item.ApprovalStatus = "Approved";
                item.ApprovedBy = userId;
                item.ApprovedOn = DateTime.Now;

                //item.MainCourseId = model.MainCourseId;
                item.CourseAllocatedTo = model.courseAllocatedTo??"BothST";
               // item.CourseCustomerType= model.CourseCustomerTypeId;
                item.Description = model.Description ?? "NA";

                // Save the checkbox independently of the image.
                ///item.ShowFeatureImage = model.ShowFeatureImage;
                if (model.FeatureImage != null &&
                       model.FeatureImage.Length > 0)
                            {
                                var uploadResult = await _fileStorage.UploadAsync(
                                    model.FeatureImage,
                                    "Uploads/Courses/FeaturedImage");

                                if (uploadResult == null ||
                                    !uploadResult.success ||
                                    string.IsNullOrWhiteSpace(uploadResult.filePath))
                                {
                                    return StatusCode(500, new
                                    {
                                        success = false,
                                        message = uploadResult?.message
                                            ?? "Feature image upload failed."
                                    });
                                }

                                // Store the path returned by the file server.
                                item.FeaturedImage = uploadResult.filePath;
                            }
                //if (model.FeatureImage != null &&
                //    model.FeatureImage.Length > 0)
                //{
                //    var webRoot = _environment.WebRootPath;

                //    if (string.IsNullOrWhiteSpace(webRoot))
                //        webRoot = Path.Combine(
                //            _environment.ContentRootPath, "wwwroot");

                //    var folderName = Guid.NewGuid().ToString();
                //    var relativeFolder = Path.Combine(
                //        "Uploads", "Courses", "FeaturedImage", folderName);

                //    var physicalFolder = Path.Combine(
                //        webRoot, relativeFolder);

                //    Directory.CreateDirectory(physicalFolder);

                //    // Generate a safe filename; never trust the uploaded filename.
                //    var fileName = "feature" +
                //        Path.GetExtension(model.FeatureImage.FileName)
                //            .ToLowerInvariant();

                //    var physicalPath = Path.Combine(physicalFolder, fileName);

                //    await using (var stream = new FileStream(
                //        physicalPath, FileMode.Create))
                //    {
                //        await model.FeatureImage.CopyToAsync(stream);
                //    }

                //    // Preserve the existing database path convention.
                //    item.FeaturedImage = "~/" +
                //        relativeFolder.Replace('\\', '/') + "/" + fileName;
                //}
                // If no new image was selected, keep item.FeaturedImage unchanged.

                if (isNew)
                {
                    item.CreatedBy = userId??0;
                    item.CreatedDate = DateTime.Now;
                    _context.Courses.Add(item);
                }
                else
                {
                    item.ModifiedDate = DateTime.Now;
                    item.ModifiedBy = userId;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = isNew
                        ? "Course registered successfully!"
                        : "Course updated successfully!",
                    featuredImage = item.FeaturedImage,
                    //showFeatureImage = item.ShowFeatureImage
                });
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                var innerExceptionMessage = ex.InnerException?.Message ?? "NA";
                //ExceptionHandlerExtensions.UseExceptionHandler(ex);
                // Log the exception server-side in production.
                return StatusCode(500, new
                {
                    success = false,
                    message = $"An error occurred while saving the course. Error: {errorMessage}. Inner Exception: {innerExceptionMessage}"
                });
            }
        }
        //[HttpPatch("course/toggle/{id}")]
        //public async Task<IActionResult> ToggleCourse(int id)
        //{
        //    var item = await _context.CourseRegistrations.FindAsync(id);
        //    if (item != null) { item.IsActive = !item.IsActive; await _context.SaveChangesAsync(); }
        //    return Ok(new { success = true });
        //}

        //[HttpDelete("course/delete/{id}")]
        //public async Task<IActionResult> DeleteCourse(int id)
        //{
        //    var item = await _context.CourseRegistrations.FindAsync(id);
        //    if (item != null) { item.IsDeleted = true; await _context.SaveChangesAsync(); }
        //    return Ok(new { success = true, message = "Deleted successfully!" });
        //}

        // --- 3. QUIZZES ---
        [HttpGet("quiz/list")]
        public async Task<IActionResult> GetQuizList() => Ok(new { success = true, data = await _context.Quiz.ToListAsync() });

        [HttpPost("quiz/save")]
        public async Task<IActionResult> SaveQuiz([FromBody] CourseQuiz model)
        {
            if (model == null) return BadRequest(new { success = false, message = "Invalid payload." });
            try
            {
                if (model.StartDate.Kind == DateTimeKind.Unspecified) model.StartDate = DateTime.SpecifyKind(model.StartDate, DateTimeKind.Utc);
                else model.StartDate = model.StartDate.ToUniversalTime();
                if (model.EndDate.Kind == DateTimeKind.Unspecified) model.EndDate = DateTime.SpecifyKind(model.EndDate, DateTimeKind.Utc);
                else model.EndDate = model.EndDate.ToUniversalTime();

                if (model.Id == 0) { model.CreatedDate = DateTime.UtcNow; _context.CourseQuizzes.Add(model); }
                else
                {
                    var item = await _context.CourseQuizzes.FindAsync(model.Id);
                    if (item == null) return NotFound(new { success = false, message = "Record not found." });
                    item.QuizName = model.QuizName; item.CourseId = model.CourseId; item.StartDate = model.StartDate; item.EndDate = model.EndDate; item.StartTime = model.StartTime; item.EndTime = model.EndTime; item.CourseDurationTypeId = model.CourseDurationTypeId; item.PassingMarks = model.PassingMarks; item.TotalMarks = model.TotalMarks; item.NoOfAttemptsAllowed = model.NoOfAttemptsAllowed; item.CourseAllotedToId = model.CourseAllotedToId; item.CourseMcqLevelId = model.CourseMcqLevelId; item.CourseLevelId = model.CourseLevelId; item.ExamActive = model.ExamActive; item.ModifiedDate = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Quiz created successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPatch("quiz/toggle/{id}")]
        public async Task<IActionResult> ToggleQuiz(int id)
        {
            var item = await _context.CourseQuizzes.FindAsync(id);
            if (item != null) { item.IsActive = !item.IsActive; await _context.SaveChangesAsync(); }
            return Ok(new { success = true });
        }

        [HttpDelete("quiz/delete/{id}")]
        public async Task<IActionResult> DeleteQuiz(int id)
        {
            var item = await _context.CourseQuizzes.FindAsync(id);
            if (item != null) { item.IsDeleted = true; await _context.SaveChangesAsync(); }
            return Ok(new { success = true, message = "Deleted successfully!" });
        }
    }
}