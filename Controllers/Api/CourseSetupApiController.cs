using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers.Api
{
    [Route("api/course-setup")]
    [ApiController]
    public class CourseSetupApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CourseSetupApiController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet("list/{masterType}")]
        public async Task<IActionResult> GetData(string masterType)
        {
            object data = masterType.ToLower() switch
            {
                "maincourse" => await _context.MainCourses.Where(x => !x.IsDeleted).ToListAsync(),
                "category" => await _context.CourseCategories.Where(x => !x.IsDeleted).ToListAsync(),
                "subcategory" => await _context.CourseSubCategories.Where(x => !x.IsDeleted).ToListAsync(),
                "ordernumber" => await _context.CourseOrderNumbers.Where(x => !x.IsDeleted).OrderBy(x => x.DisplayOrder).ToListAsync(),
                "years" => await _context.CourseYears.Where(x => !x.IsDeleted).ToListAsync(),
                "author" => await _context.CourseAuthors.Where(x => !x.IsDeleted).ToListAsync(),
                "manager" => await _context.CourseManagers.Where(x => !x.IsDeleted).ToListAsync(),
                "coursesfor" => await _context.CourseFors.Where(x => !x.IsDeleted).ToListAsync(),
                "customertype" => await _context.CourseCustomerTypes.Where(x => !x.IsDeleted).ToListAsync(),
                "durationtype" => await _context.CourseDurationTypes.Where(x => !x.IsDeleted).ToListAsync(),
                "courseallotedto" => await _context.CourseAllotedTos.Where(x => !x.IsDeleted).ToListAsync(),
                "mcqlevel" => await _context.CourseMcqLevels.Where(x => !x.IsDeleted).ToListAsync(),
                "levels" => await _context.CourseLevels.Where(x => !x.IsDeleted).ToListAsync(),
                "webinarmcqcategory" => await _context.WebinarMcqCategories                   
                    .Include(x => x.OrderNumber)
                    .OrderBy(x => x.OrderNumber != null ? x.OrderNumber.DisplayOrder : int.MaxValue)
                    .ToListAsync(),
                _ => null
            };

            if (data == null) return BadRequest(new { success = false, message = "Invalid Master Type" });
            return Ok(new { success = true, data });
        }

        [HttpPost("save/{masterType}")]
        public async Task<IActionResult> SaveData(string masterType, [FromForm] int id, [FromForm] string? name, [FromForm] string? description, [FromForm] int? displayOrder, [FromForm] int? orderNumberId, [FromForm] string? years, [FromForm] string? remark, [FromForm] bool? isLocked, [FromForm] bool? isActive, [FromForm] IFormFile? imageFile)
        {
            string? imageUrl = null;
            string type = masterType.ToLower();

            if (imageFile != null && imageFile.Length > 0)
            {
                string folder = Path.Combine(_env.WebRootPath, "uploads", "webinar");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string fileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
                using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                imageUrl = $"/uploads/webinar/{fileName}";
            }

            try
            {
                bool activeStatus = isActive ?? true;

                switch (type)
                {
                    case "maincourse":
                        var mc = id == 0 ? new MainCourse() : await _context.MainCourses.FindAsync(id);
                        if (id > 0 && mc == null) return NotFound(new { success = false, message = "Not found." });
                        mc.Name = name?.Trim() ?? string.Empty;
                        mc.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                        mc.IsActive = activeStatus;
                        if (id == 0) _context.MainCourses.Add(mc); else { mc.ModifiedDate = DateTime.UtcNow; _context.MainCourses.Update(mc); }
                        break;

                    case "category":
                        var cat = id == 0 ? new CourseCategory() : await _context.CourseCategories.FindAsync(id);
                        if (id > 0 && cat == null) return NotFound(new { success = false, message = "Not found." });
                        cat.Name = name?.Trim() ?? string.Empty;
                        cat.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                        cat.IsActive = activeStatus;
                        if (id == 0) _context.CourseCategories.Add(cat); else { cat.ModifiedDate = DateTime.UtcNow; _context.CourseCategories.Update(cat); }
                        break;

                    case "subcategory":
                        var sub = id == 0 ? new CourseSubCategory() : await _context.CourseSubCategories.FindAsync(id);
                        if (id > 0 && sub == null) return NotFound(new { success = false, message = "Not found." });
                        sub.Name = name?.Trim() ?? string.Empty;
                        sub.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                        sub.IsActive = activeStatus;
                        if (id == 0) _context.CourseSubCategories.Add(sub); else { sub.ModifiedDate = DateTime.UtcNow; _context.CourseSubCategories.Update(sub); }
                        break;

                    case "ordernumber":
                        var ord = id == 0 ? new CourseOrderNumber() : await _context.CourseOrderNumbers.FindAsync(id);
                        if (id > 0 && ord == null) return NotFound(new { success = false, message = "Not found." });
                        ord.DisplayOrder = displayOrder ?? 1;
                        ord.IsActive = activeStatus;
                        if (id == 0) _context.CourseOrderNumbers.Add(ord); else { ord.ModifiedDate = DateTime.UtcNow; _context.CourseOrderNumbers.Update(ord); }
                        break;

                    case "years":
                        var yr = id == 0 ? new CourseYear() : await _context.CourseYears.FindAsync(id);
                        if (id > 0 && yr == null) return NotFound(new { success = false, message = "Not found." });
                        yr.Years = years?.Trim() ?? string.Empty;
                        yr.Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
                        yr.IsActive = activeStatus;
                        if (id == 0) _context.CourseYears.Add(yr); else { yr.ModifiedDate = DateTime.UtcNow; _context.CourseYears.Update(yr); }
                        break;

                    case "author":
                        var aut = id == 0 ? new CourseAuthor() : await _context.CourseAuthors.FindAsync(id);
                        if (id > 0 && aut == null) return NotFound(new { success = false, message = "Not found." });
                        aut.Name = name?.Trim() ?? string.Empty;
                        aut.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                        aut.IsActive = activeStatus;
                        if (id == 0) _context.CourseAuthors.Add(aut); else { aut.ModifiedDate = DateTime.UtcNow; _context.CourseAuthors.Update(aut); }
                        break;

                    case "manager":
                        var mgr = id == 0 ? new CourseManager() : await _context.CourseManagers.FindAsync(id);
                        if (id > 0 && mgr == null) return NotFound(new { success = false, message = "Not found." });
                        mgr.Name = name?.Trim() ?? string.Empty;
                        mgr.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                        mgr.IsActive = activeStatus;
                        if (id == 0) _context.CourseManagers.Add(mgr); else { mgr.ModifiedDate = DateTime.UtcNow; _context.CourseManagers.Update(mgr); }
                        break;

                    case "coursesfor":
                        var cf = id == 0 ? new CourseFor() : await _context.CourseFors.FindAsync(id);
                        if (id > 0 && cf == null) return NotFound(new { success = false, message = "Not found." });
                        cf.Name = name?.Trim() ?? string.Empty;
                        cf.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                        cf.IsActive = activeStatus;
                        if (id == 0) _context.CourseFors.Add(cf); else { cf.ModifiedDate = DateTime.UtcNow; _context.CourseFors.Update(cf); }
                        break;

                    case "customertype":
                        var ct = id == 0 ? new CourseCustomerType() : await _context.CourseCustomerTypes.FindAsync(id);
                        if (id > 0 && ct == null) return NotFound(new { success = false, message = "Not found." });
                        ct.Name = name?.Trim() ?? string.Empty;
                        ct.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                        ct.IsActive = activeStatus;
                        if (id == 0) _context.CourseCustomerTypes.Add(ct); else { ct.ModifiedDate = DateTime.UtcNow; _context.CourseCustomerTypes.Update(ct); }
                        break;

                    case "durationtype":
                        var dt = id == 0 ? new CourseDurationType() : await _context.CourseDurationTypes.FindAsync(id);
                        if (id > 0 && dt == null) return NotFound(new { success = false, message = "Not found." });
                        dt.Name = name?.Trim() ?? string.Empty;
                        dt.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                        dt.IsActive = activeStatus;
                        if (id == 0) _context.CourseDurationTypes.Add(dt); else { dt.ModifiedDate = DateTime.UtcNow; _context.CourseDurationTypes.Update(dt); }
                        break;

                    case "courseallotedto":
                        var ca = id == 0 ? new CourseAllotedTo() : await _context.CourseAllotedTos.FindAsync(id);
                        if (id > 0 && ca == null) return NotFound(new { success = false, message = "Not found." });
                        ca.Name = name?.Trim() ?? string.Empty;
                        ca.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                        ca.IsActive = activeStatus;
                        if (id == 0) _context.CourseAllotedTos.Add(ca); else { ca.ModifiedDate = DateTime.UtcNow; _context.CourseAllotedTos.Update(ca); }
                        break;

                    case "mcqlevel":
                        var ml = id == 0 ? new CourseMcqLevel() : await _context.CourseMcqLevels.FindAsync(id);
                        if (id > 0 && ml == null) return NotFound(new { success = false, message = "Not found." });
                        ml.Name = name?.Trim() ?? string.Empty;
                        ml.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                        ml.IsActive = activeStatus;
                        if (id == 0) _context.CourseMcqLevels.Add(ml); else { ml.ModifiedDate = DateTime.UtcNow; _context.CourseMcqLevels.Update(ml); }
                        break;

                    case "levels":
                        var lvl = id == 0 ? new CourseLevel() : await _context.CourseLevels.FindAsync(id);
                        if (id > 0 && lvl == null) return NotFound(new { success = false, message = "Not found." });
                        lvl.Years = name?.Trim() ?? string.Empty;
                        lvl.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                        lvl.IsActive = activeStatus;
                        if (id == 0) _context.CourseLevels.Add(lvl); else { lvl.ModifiedDate = DateTime.UtcNow; _context.CourseLevels.Update(lvl); }
                        break;

                    case "webinarmcqcategory":
                        var wmc = id == 0 ? new WebinarMcqCategory() : await _context.WebinarMcqCategories.FindAsync(id);
                        if (id > 0 && wmc == null) return NotFound(new { success = false, message = "Not found." });
                        wmc.Name = name?.Trim() ?? string.Empty;
                        wmc.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                       // wmc.IsLocked = isLocked ?? false;
                        wmc.IsActive = activeStatus;
                        wmc.OrderNumberId = orderNumberId > 0 ? orderNumberId : null; // Save Order ID

                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            if (!string.IsNullOrEmpty(wmc.ImageUrl))
                            {
                                var oldPath = Path.Combine(_env.WebRootPath, wmc.ImageUrl.TrimStart('/'));
                                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                            }
                            wmc.ImageUrl = imageUrl;
                        }

                        if (id == 0) _context.WebinarMcqCategories.Add(wmc); else { wmc.ModifiedDate = DateTime.UtcNow; _context.WebinarMcqCategories.Update(wmc); }
                        break;

                    default:
                        return BadRequest(new { success = false, message = "Invalid Master Type" });
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Saved successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("delete/{masterType}/{id}")]
        public async Task<IActionResult> DeleteData(string masterType, int id)
        {
            bool found = masterType.ToLower() switch
            {
                "maincourse" => SetDeleted(await _context.MainCourses.FindAsync(id)),
                "category" => SetDeleted(await _context.CourseCategories.FindAsync(id)),
                "subcategory" => SetDeleted(await _context.CourseSubCategories.FindAsync(id)),
                "ordernumber" => SetDeleted(await _context.CourseOrderNumbers.FindAsync(id)),
                "years" => SetDeleted(await _context.CourseYears.FindAsync(id)),
                "author" => SetDeleted(await _context.CourseAuthors.FindAsync(id)),
                "manager" => SetDeleted(await _context.CourseManagers.FindAsync(id)),
                "coursesfor" => SetDeleted(await _context.CourseFors.FindAsync(id)),
                "customertype" => SetDeleted(await _context.CourseCustomerTypes.FindAsync(id)),
                "durationtype" => SetDeleted(await _context.CourseDurationTypes.FindAsync(id)),
                "courseallotedto" => SetDeleted(await _context.CourseAllotedTos.FindAsync(id)),
                "mcqlevel" => SetDeleted(await _context.CourseMcqLevels.FindAsync(id)),
                "levels" => SetDeleted(await _context.CourseLevels.FindAsync(id)),
                "webinarmcqcategory" => SetDeleted(await _context.WebinarMcqCategories.FindAsync(id)),
                _ => false
            };

            if (!found) return BadRequest(new { success = false, message = "Record not found or invalid type" });

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Deleted successfully!" });
        }

        private static bool SetDeleted(BaseMasterEntity? entity)
        {
            if (entity == null) return false;
            entity.IsDeleted = true;
            entity.ModifiedDate = DateTime.UtcNow;
            return true;
        }
    }
}