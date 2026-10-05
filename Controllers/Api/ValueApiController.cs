using HemFoundation.Data;
using HemFoundation.Models.Entities;
using HemFoundation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;

namespace HemFoundation.Controllers.Api
{
    [Route("api/value")]
    [ApiController]
    public class ValueApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IFileStorageService _fileStorage;

        public ValueApiController(
      ApplicationDbContext context,
      IWebHostEnvironment env,
      IFileStorageService fileStorage)
        {
            _context = context;
            _env = env;
            _fileStorage = fileStorage;
        }

        // ================== GRADE MASTER ==================

        [HttpGet("standards")]
        public async Task<IActionResult> GetStandards()
        {
            var standards = await _context.StandardMasters.Where(s => s.IsActive && s.Id >= 1 && s.Id <= 10).OrderBy(s => s.DisplayOrder).ToListAsync();
            return Ok(new { success = true, data = standards });
        }

        [HttpPost("add-grade")]
        public async Task<IActionResult> AddGrade([FromForm] ValueGradeMaster model)
        {
            model.CreatedDate = DateTime.UtcNow;
            _context.ValueGradeMasters.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Grade Master added successfully." });
        }

        [HttpGet("grades-list")]
        public async Task<IActionResult> GetGrades()
        {
            var grades = await _context.ValueGradeMasters.Where(g => g.IsActive).OrderBy(g => g.StandardId).ToListAsync();
            return Ok(new { success = true, data = grades });
        }

        // ================== VALUE ITEM MASTER ==================

        [HttpPost("add-value")]
        public async Task<IActionResult> AddValue(
      [FromForm] int GradeId,
      [FromForm] string CapacityName,
      [FromForm] string ValueTitleEng,
      [FromForm] string ValueTitleHin,
      IFormFile ValueImage,
      IFormFile ActivityPdf)
        {
            var item = new ValueItemMaster
            {
                GradeId = GradeId,
                CapacityName = CapacityName,
                ValueTitleEng = ValueTitleEng,
                ValueTitleHin = ValueTitleHin,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };

            // ================= VALUE IMAGE UPLOAD =================

            if (ValueImage != null && ValueImage.Length > 0)
            {
                string folderPath =
                    "Uploads/ValueOfYear/Calendar";

                var uploadResult =
                    await _fileStorage.UploadAsync(
                        ValueImage,
                        folderPath);

                if (uploadResult == null ||
                    !uploadResult.success ||
                    string.IsNullOrWhiteSpace(uploadResult.filePath))
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = uploadResult?.message
                            ?? "Value image upload failed."
                    });
                }

                item.ValueImageUrl = uploadResult.filePath;
            }


            if (ActivityPdf != null && ActivityPdf.Length > 0)
            {
                string folderPath =
                    "Uploads/ValueOfYear/Calendar";

                var uploadResult =
                    await _fileStorage.UploadAsync(
                        ActivityPdf,
                        folderPath);

                if (uploadResult == null ||
                    !uploadResult.success ||
                    string.IsNullOrWhiteSpace(uploadResult.filePath))
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = uploadResult?.message
                            ?? "Activity PDF upload failed."
                    });
                }

                item.ActivityPdfUrl = uploadResult.filePath;
            }


            _context.ValueItemMasters.Add(item);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Calendar Value Card added successfully."
            });
        }

        [HttpGet("values-list")]
        public async Task<IActionResult> GetValues()
        {
            var values = await (from v in _context.ValueItemMasters
                                join g in _context.ValueGradeMasters on v.GradeId equals g.Id
                                orderby g.StandardId, v.CapacityName
                                select new
                                {
                                    v.Id,
                                    g.GradeName,
                                    g.GroupName,
                                    v.CapacityName,
                                    v.ValueTitleEng,
                                    v.ValueTitleHin,
                                    v.ValueImageUrl,
                                    v.ActivityPdfUrl
                                }).ToListAsync();
            return Ok(new { success = true, data = values });
        }

        [HttpGet("school-calendar")]
        public async Task<IActionResult> GetSchoolCalendar()
        {
            int? currentUserId = HttpContext.Session.GetInt32("UserID");

            if (currentUserId == null || currentUserId == 0)
            {
                // Fall back to claims if session is missing
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                currentUserId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }
            if (string.IsNullOrEmpty(currentUserId.ToString())) return Unauthorized(new { success = false, message = "Not logged in" });

            var institute = await _context.view_wj_instituteslist.FirstOrDefaultAsync(i => i.instituteid == currentUserId);
            if (institute == null) return BadRequest(new { success = false, message = "School profile not found" });

            var schoolStandardNames = await _context.hf_view_studentsdetails
                .Where(s => s.InstituteID == institute.instituteid && s.Standard != null)
                .Select(s => s.Standard).Distinct().ToListAsync();

            var standardIds = await _context.StandardMasters
                .Where(s => schoolStandardNames.Contains(s.StandardName))
                .Select(s => s.Id).ToListAsync();
            string[] excludedStandards = { "13th", "14th", "15th", "All", "Teacher" };

            var grades = await _context.StandardMasters
                .Where(g => !excludedStandards.Contains(g.StandardName))
                .OrderBy(g => g.Id) // Orders by ID instead of StandardName
                .ToListAsync();
            //var grades = await _context.StandardMasters
            //    .OrderBy(g => g.StandardName).ToListAsync();

            var gradeIds = grades.Select(g => g.Id).ToList();
            var gradeNames = grades.Select(g => g.StandardName).ToList();

            var values = await _context.vw_valueitemmasters
            .Where(v =>  v.GradeId != null && gradeNames.Contains(v.GradeId))
            .ToListAsync();
            //var values = await _context.vw_valueitemmasters
            //   .Where(v => v.IsActive && gradeIds.Contains(v.GradeId.ToString())).ToListAsync();

            // NAYA: Check submissions to mark "Completed"
            var submittedActivityIds = await _context.creativityActivitiesSubmission2021s
                .Where(s => s.InstituteID == institute.instituteid)
                .Select(s => s.MainCourseID)
                .Distinct()
                .ToListAsync();

            var result = grades.Select(g => new
            {
                g.Id,
                g.StandardName,               
                Capacities = values.Where(v => v.GradeId == g.StandardName).Select(v => new
                {
                    v.Id, // NAYA: URL ke liye ID zaroori hai
                    v.CapacityName,
                    v.ValueTitleEng,
                    v.ValueTitleHin,
                    v.ValueImageUrl,
                    v.ActivityPdfUrl,
                    IsCompleted = submittedActivityIds.Contains(v.Id) // NAYA: Status Check
                }).ToList()
            });

            return Ok(new { success = true, data = result });
            
        }


        [HttpGet("activity-page-data/{valueItemId}")]
        public async Task<IActionResult> GetActivityPageData(int valueItemId)
        {
            int? currentUserId = HttpContext.Session.GetInt32("UserID");

            if (currentUserId == null || currentUserId == 0)
            {
                // Fall back to claims if session is missing
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                currentUserId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }
            if (string.IsNullOrEmpty(currentUserId.ToString())) return Unauthorized(new { success = false, message = "Not logged in" });

            var institute = await _context.view_wj_instituteslist.FirstOrDefaultAsync(i => i.instituteid == currentUserId);
            if (institute == null) return BadRequest(new { success = false, message = "School not found." });

            var valueItem = await _context.vw_valueitemmasters.FirstOrDefaultAsync(v => v.Id == valueItemId);
            if (valueItem == null) return BadRequest(new { success = false, message = "Activity not found." });

            var grade = await _context.StandardMasters.FirstOrDefaultAsync(g => g.StandardName == valueItem.GradeId);
            var standard = await _context.StandardMasters.FirstOrDefaultAsync(s => s.StandardName == grade.StandardName);

            // 1. Students of that standard
            var students = await _context.hf_view_studentsdetails
                .Where(s => s.InstituteID == institute.instituteid && s.Standard == standard.StandardName)
                .Select(s => new { Id = s.StudentId, Name = s.FirstName + " " + s.LastName })
                .ToListAsync();

            var submissions = await _context.creativityActivitiesSubmission2021s
              .Where(s => s.InstituteID == institute.instituteid && s.MainCourseID == valueItemId)
              .ToListAsync();

            // 2. Fetch all group allocation rows and group them by GroupName to avoid duplicates
            var groupRows = await _context.HfRelManageGroupList
                .Where(g => g.IsAllocatedBy == institute.instituteid && g.MainCourseID == valueItem.Id)
                .ToListAsync();

            var formattedGroups = groupRows
                .GroupBy(g => g.GroupName)
                .Select(g => new {
                    groupName = g.Key,
                    studentIds = g.Select(x => x.StudentID).Where(id => id.HasValue).Select(id => id.Value).ToList()
                })
                .ToList();

            return Ok(new
            {
                success = true,
                activity = new { valueItem.CapacityName, valueItem.ValueTitleEng, grade.StandardName },
                students,
                groups = formattedGroups,
                submissions
            });
        }

        public class SaveGroupDto { public string GroupName { get; set; } public List<int> StudentIds { get; set; } }

        [HttpPost("save-groups/{valueItemId}")]
        public async Task<IActionResult> SaveGroups(int valueItemId, [FromBody] List<SaveGroupDto> requestGroups)
        {
            if (requestGroups == null || !requestGroups.Any())
                return BadRequest(new { success = false, message = "No data received." });

            int? currentUserId = HttpContext.Session.GetInt32("UserID");

            if (currentUserId == null || currentUserId == 0)
            {
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                currentUserId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }
            if (string.IsNullOrEmpty(currentUserId.ToString())) return Unauthorized(new { success = false, message = "Not logged in" });

            var institute = await _context.view_wj_instituteslist.FirstOrDefaultAsync(i => i.instituteid == currentUserId);
            if (institute == null) return BadRequest(new { success = false, message = "School not found." });


            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var oldGroups = await _context.HfRelManageGroupList
                    .Where(g => g.IsAllocatedBy == institute.instituteid && g.MainCourseID == valueItemId)
                    .ToListAsync();

                _context.HfRelManageGroupList.RemoveRange(oldGroups);
                await _context.SaveChangesAsync();

                foreach (var rg in requestGroups)
                {
                    string gName = string.IsNullOrWhiteSpace(rg.GroupName) ? "Group" : rg.GroupName;

                    if (rg.StudentIds != null && rg.StudentIds.Any())
                    {
                        foreach (var sid in rg.StudentIds.Distinct())
                        {
                            _context.HfRelManageGroupList.Add(new HfRelManageGroupList
                            {
                                IsAllocatedBy = institute.instituteid,
                                MainCourseID = valueItemId,
                                GroupName = gName,
                                StudentID = sid,
                                IsAllocated = true
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Teams & Students updated successfully!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        [HttpPost("submit-activity")]
        public async Task<IActionResult> SubmitActivity(
            [FromForm] int ValueItemId,
            [FromForm] string Comment,
            IFormFile? ImageFile,
            IFormFile? VideoFile)
        {
            int? currentUserId =
                HttpContext.Session.GetInt32("UserID");

            if (currentUserId == null || currentUserId == 0)
            {
                // Fall back to claims if session is missing
                string? claimId =
                    User.FindFirstValue(ClaimTypes.NameIdentifier);

                currentUserId =
                    int.TryParse(
                        claimId,
                        out int parsed)
                        ? parsed
                        : 0;
            }

            if (string.IsNullOrEmpty(currentUserId.ToString()))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Not logged in"
                });
            }

            var institute =
                await _context.view_wj_instituteslist
                    .FirstOrDefaultAsync(
                        i => i.instituteid == currentUserId);

            if (institute == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "School not found."
                });
            }


            bool exists =
                await _context.creativityActivitiesSubmission2021s
                    .AnyAsync(s =>
                        s.InstituteID == institute.instituteid &&
                        s.MainCourseID == ValueItemId);

            if (exists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Activity already submitted for this capacity."
                });
            }

            var submission =
                new CreativityActivitiesSubmission2021
                {
                    InstituteID = institute.instituteid,
                    MainCourseID = ValueItemId,

                    ShortDescription = Comment,
                    CreatedDate = System.DateTime.Now
                };



            if (ImageFile != null && ImageFile.Length > 0)
            {
                string folderPath =
                    $"Uploads/ValueOfYear/Submissions/{institute.instituteid}";

                var uploadResult =
                    await _fileStorage.UploadAsync(
                        ImageFile,
                        folderPath);

                if (uploadResult == null ||
                    !uploadResult.success ||
                    string.IsNullOrWhiteSpace(uploadResult.filePath))
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = uploadResult?.message
                            ?? "Image upload failed."
                    });
                }

                submission.ImageActivity =
                    uploadResult.filePath;
            }


            if (VideoFile != null && VideoFile.Length > 0)
            {
                string folderPath =
                    $"Uploads/ValueOfYear/Submissions/{institute.instituteid}";

                var uploadResult =
                    await _fileStorage.UploadAsync(
                        VideoFile,
                        folderPath);

                if (uploadResult == null ||
                    !uploadResult.success ||
                    string.IsNullOrWhiteSpace(uploadResult.filePath))
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = uploadResult?.message
                            ?? "Video upload failed."
                    });
                }

                submission.VideoActivity =
                    uploadResult.filePath;
            }



            _context.creativityActivitiesSubmission2021s
                .Add(submission);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Capacity activity submitted successfully!"
            });
        }
    }

    public class SaveGroupDto
    {
        public string? GroupName { get; set; }
        public List<int>? StudentIds { get; set; }
    }
}