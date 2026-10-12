using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Route("api/project-evaluations")]
    [ApiController]
    public class ProjectEvaluationsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        public ProjectEvaluationsApiController(
     ApplicationDbContext context,
     IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet("dashboard-counts")]
        public async Task<IActionResult> GetDashboardCounts(int? month = null, int? year = null)
        {
            try
            {
                // All-time counts (upar display karne ke liye)
                int allTimeSel = await _context.creativityActivitiesSubmission2021s.AsNoTracking().Where(x => x.InstituteID != null).Select(x => x.InstituteID).Distinct().CountAsync();
                int allTimeFamily = await _context.Set<HfUploadAnubhuti>().AsNoTracking().CountAsync();
                int allTimeIndividual = await _context.Set<HfTblUploadIndividualProject>().AsNoTracking().CountAsync();
                int allTimeGroup = await _context.creativityActivitiesSubmission2021s.AsNoTracking().CountAsync();

                // Filtered counts based on selected month/year
                var selQuery = _context.creativityActivitiesSubmission2021s.AsNoTracking().Where(x => x.InstituteID != null);
                var familyQuery = _context.Set<HfUploadAnubhuti>().AsNoTracking();
                var indQuery = _context.Set<HfTblUploadIndividualProject>().AsNoTracking();
                var groupQuery = _context.creativityActivitiesSubmission2021s.AsNoTracking();

                if (month.HasValue && year.HasValue)
                {
                    selQuery = selQuery.Where(x => x.CreatedDate.HasValue && x.CreatedDate.Value.Month == month.Value && x.CreatedDate.Value.Year == year.Value);
                    familyQuery = familyQuery.Where(x => x.IsCreatedDate.HasValue && x.IsCreatedDate.Value.Month == month.Value && x.IsCreatedDate.Value.Year == year.Value);
                    indQuery = indQuery.Where(x => x.IsCreatedDate.HasValue && x.IsCreatedDate.Value.Month == month.Value && x.IsCreatedDate.Value.Year == year.Value);
                    groupQuery = groupQuery.Where(x => x.CreatedDate.HasValue && x.CreatedDate.Value.Month == month.Value && x.CreatedDate.Value.Year == year.Value);
                }

                int filteredSel = await selQuery.Select(x => x.InstituteID).Distinct().CountAsync();
                int filteredFamily = await familyQuery.CountAsync();
                int filteredInd = await indQuery.CountAsync();
                int filteredGroup = await groupQuery.CountAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        allTime = new { totalSelProjects = allTimeSel, familyProjects = allTimeFamily, individualProjects = allTimeIndividual, groupPbl = allTimeGroup },
                        filtered = new { totalSelProjects = filteredSel, familyProjects = filteredFamily, individualProjects = filteredInd, groupPbl = filteredGroup }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("list/{projectType}")]
        public async Task<IActionResult> GetProjectList(string projectType, int page = 1, int pageSize = 10, string searchTerm = "", int? month = null, int? year = null)
        {
            try
            {
                projectType = projectType?.Trim().ToLower() ?? "";
                searchTerm = searchTerm?.Trim().ToLower() ?? "";
                int skip = (page - 1) * pageSize;

                var institutes = await _context.Set<WJ_Institutes>().AsNoTracking().Where(i => i != null).ToDictionaryAsync(i => i.InstituteID, i => i.Name ?? "Unknown Institute");
                var studentAssociatesList = await _context.Set<WJ_StudentAssociates>().AsNoTracking().Where(x => x != null).ToListAsync();

                var studentInstitutes = studentAssociatesList
                    .GroupBy(x => x.StudentID)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault(x => x.IsPrimaryAssociate == true)?.InstituteID ?? g.First().InstituteID);

                var evaluations = await _context.ProjectEvaluations.AsNoTracking().Where(x => x != null && x.ProjectType == projectType).ToDictionaryAsync(x => x.ProjectID, x => x);

                if (projectType == "family-projects")
                {
                    var rawData = await _context.Set<HfUploadAnubhuti>().AsNoTracking().Where(x => x != null).OrderByDescending(x => x.IsCreatedDate).ToListAsync();

                    if (month.HasValue && year.HasValue)
                        rawData = rawData.Where(x => x.IsCreatedDate.HasValue && x.IsCreatedDate.Value.Month == month.Value && x.IsCreatedDate.Value.Year == year.Value).ToList();

                    var query = rawData.Select((f, index) => {
                        int? studentId = f.iscreatedby;
                        int? instId = (studentId != null && studentInstitutes.ContainsKey(studentId.Value)) ? studentInstitutes[studentId.Value] : (int?)null;
                        string schoolName = (instId != null && institutes.ContainsKey(instId.Value)) ? institutes[instId.Value] : (f.CourseName ?? "Unknown School");
                        var eval = evaluations.ContainsKey(f.Id) ? evaluations[f.Id] : null;

                        return new
                        {
                            RowNumber = index + 1,
                            Id = f.Id,
                            InstituteId = instId,
                            StudentId = studentId,
                            CourseName = f.CourseName ?? "",
                            SchoolName = schoolName,
                            Title = f.Title ?? f.CourseName ?? "Anubhuti Family Project",
                            FirstName = f.FirstName ?? "",
                            LastName = f.LastName ?? "",
                            FileLink = FormatWebPath(f.FileLink ?? string.Empty),
                            Status = f.Status ?? "Pending",
                            CreatedDate = f.IsCreatedDate?.ToString("yyyy-MM-dd") ?? "",
                            EvaluatedColor = eval?.StatusColor ?? "",
                            EvaluatedMarks = eval?.Marks ?? 0,
                            IsEvaluated = eval != null
                        };
                    }).AsQueryable();

                    if (!string.IsNullOrEmpty(searchTerm))
                    {
                        query = query.Where(x => x.SchoolName.ToLower().Contains(searchTerm) || x.FirstName.ToLower().Contains(searchTerm) || x.Title.ToLower().Contains(searchTerm) || x.Status.ToLower().Contains(searchTerm) || x.EvaluatedColor.ToLower().Contains(searchTerm));
                    }

                    var totalRecords = query.Count();
                    var data = query.Skip(skip).Take(pageSize).ToList();
                    return Ok(new { success = true, totalRecords, data });
                }
                else if (projectType == "individual-projects")
                {
                    var rawData = await _context.Set<HfTblUploadIndividualProject>().AsNoTracking().Where(x => x != null).OrderByDescending(x => x.IsCreatedDate).ToListAsync();

                    if (month.HasValue && year.HasValue)
                        rawData = rawData.Where(x => x.IsCreatedDate.HasValue && x.IsCreatedDate.Value.Month == month.Value && x.IsCreatedDate.Value.Year == year.Value).ToList();

                    var query = rawData.Select((ind, index) => {
                        int? studentId = ind.Iscreatedby;
                        int? instId = (studentId != null && studentInstitutes.ContainsKey(studentId.Value)) ? studentInstitutes[studentId.Value] : (int?)null;
                        string schoolName = (instId != null && institutes.ContainsKey(instId.Value)) ? institutes[instId.Value] : (ind.CourseName ?? "Unknown School");
                        var eval = evaluations.ContainsKey(ind.Id) ? evaluations[ind.Id] : null;

                        return new
                        {
                            RowNumber = index + 1,
                            Id = ind.Id,
                            InstituteId = instId,
                            StudentId = studentId,
                            CourseName = ind.CourseName ?? "",
                            SchoolName = schoolName,
                            Title = ind.Title ?? ind.CourseName ?? "Individual PBL Project",
                            FirstName = ind.FirstName ?? "",
                            LastName = ind.LastName ?? "",
                            FileLink = FormatWebPath(ind.FileLink ?? string.Empty),
                            Status = ind.Status ?? "Pending",
                            CreatedDate = ind.IsCreatedDate?.ToString("yyyy-MM-dd") ?? "",
                            EvaluatedColor = eval?.StatusColor ?? "",
                            EvaluatedMarks = eval?.Marks ?? 0,
                            IsEvaluated = eval != null
                        };
                    }).AsQueryable();

                    if (!string.IsNullOrEmpty(searchTerm))
                    {
                        query = query.Where(x => x.SchoolName.ToLower().Contains(searchTerm) || x.FirstName.ToLower().Contains(searchTerm) || x.Title.ToLower().Contains(searchTerm) || x.Status.ToLower().Contains(searchTerm) || x.EvaluatedColor.ToLower().Contains(searchTerm));
                    }

                    var totalRecords = query.Count();
                    var data = query.Skip(skip).Take(pageSize).ToList();
                    return Ok(new { success = true, totalRecords, data });
                }
                else if (projectType == "group-pbl")
                {
                    var rawData = await _context.creativityActivitiesSubmission2021s.AsNoTracking().Where(x => x != null).OrderByDescending(x => x.CreatedDate).ToListAsync();

                    if (month.HasValue && year.HasValue)
                        rawData = rawData.Where(x => x.CreatedDate.HasValue && x.CreatedDate.Value.Month == month.Value && x.CreatedDate.Value.Year == year.Value).ToList();

                    var query = rawData.Select((g, index) => {
                        var eval = evaluations.ContainsKey(g.SubmissionID) ? evaluations[g.SubmissionID] : null;
                        string schoolName = (g.InstituteID != null && institutes.ContainsKey(g.InstituteID.Value)) ? institutes[g.InstituteID.Value] : "Unknown Institute";

                        return new
                        {
                            RowNumber = index + 1,
                            Id = g.SubmissionID,
                            InstituteId = g.InstituteID,
                            StudentId = (int?)null,
                            CourseName = g.MainCourseID.ToString(),
                            SchoolName = schoolName,
                            Title = g.ShortDescription ?? "Group PBL Activity",
                            FirstName = "Team Members",
                            LastName = g.TeamMembersList ?? "",
                            FileLink = "",
                            ImageActivity = FormatWebPath(g.ImageActivity ?? string.Empty),
                            VideoActivity = FormatWebPath(g.VideoActivity ?? string.Empty),
                            Status = "Submitted",
                            CreatedDate = g.CreatedDate?.ToString("yyyy-MM-dd") ?? "",
                            EvaluatedColor = eval?.StatusColor ?? "",
                            EvaluatedMarks = eval?.Marks ?? 0,
                            IsEvaluated = eval != null
                        };
                    }).AsQueryable();

                    if (!string.IsNullOrEmpty(searchTerm))
                    {
                        query = query.Where(x => x.SchoolName.ToLower().Contains(searchTerm) || x.Title.ToLower().Contains(searchTerm) || x.LastName.ToLower().Contains(searchTerm) || x.EvaluatedColor.ToLower().Contains(searchTerm));
                    }

                    var totalRecords = query.Count();
                    var data = query.Skip(skip).Take(pageSize).ToList();
                    return Ok(new { success = true, totalRecords, data });
                }

                return BadRequest(new { success = false, message = "Invalid project type." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveEvaluation([FromBody] ProjectEvaluation model)
        {
            try
            {
                string? userId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
                model.EvaluatedByUserId = userId ?? string.Empty;
                model.EvaluatedDate = DateTime.Now;

                if (model.ProjectType == "group-pbl")
                {
                    if (!string.IsNullOrEmpty(model.CourseName) && int.TryParse(model.CourseName, out int courseId))
                    {
                        var connection = _context.Database.GetDbConnection();
                        if (connection.State != System.Data.ConnectionState.Open)
                        {
                            await connection.OpenAsync();
                        }

                        using var command = connection.CreateCommand();
                        command.CommandText = "SELECT maincoursename FROM maincourses WHERE maincourseid = @courseId";

                        var parameter = command.CreateParameter();
                        parameter.ParameterName = "@courseId";
                        parameter.Value = courseId;
                        command.Parameters.Add(parameter);

                        var result = await command.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            model.CourseName = result.ToString();
                        }
                    }
                }

                var existing = await _context.ProjectEvaluations
                    .FirstOrDefaultAsync(x => x.ProjectID == model.ProjectID && x.ProjectType == model.ProjectType);

                if (existing != null)
                {
                    existing.StatusColor = model.StatusColor;
                    existing.Marks = model.Marks;
                    existing.Description = model.Description;
                    existing.StudentID = model.StudentID;
                    existing.InstituteID = model.InstituteID;
                    existing.CourseName = model.CourseName;
                    existing.EvaluatedByUserId = userId ?? string.Empty;
                    existing.EvaluatedDate = DateTime.Now;
                }
                else
                {
                    _context.ProjectEvaluations.Add(model);
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Evaluation saved successfully!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetEvaluatedHistory()
        {
            var list = await _context.ProjectEvaluations.AsNoTracking().OrderByDescending(x => x.EvaluatedDate).Take(50).ToListAsync();
            return Ok(new { success = true, data = list });
        }

        private string FormatWebPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            string normalized = path.Replace("\\", "/").Trim();

            string uploadApiUrl =
                _configuration["FileSettings:InHouseUploadUrl"] ?? "";

            string fileBaseUrl = "";

            if (Uri.TryCreate(
                uploadApiUrl,
                UriKind.Absolute,
                out Uri? fileUri))
            {
                fileBaseUrl = $"{fileUri.Scheme}://{fileUri.Authority}";
            }

            if (string.IsNullOrWhiteSpace(fileBaseUrl))
                return normalized;


            int uploadsIndex = normalized.IndexOf(
                "Uploads/",
                StringComparison.OrdinalIgnoreCase);

            if (uploadsIndex >= 0)
            {
                string uploadPath = normalized.Substring(uploadsIndex);

                return fileBaseUrl.TrimEnd('/') +
                       "/" +
                       uploadPath.TrimStart('/');
            }

            if (normalized.StartsWith("/"))
            {
                return fileBaseUrl.TrimEnd('/') +
                       normalized;
            }

            if (normalized.StartsWith("Uploads/",
                StringComparison.OrdinalIgnoreCase))
            {
                return fileBaseUrl.TrimEnd('/') +
                       "/" +
                       normalized;
            }

            return normalized;
        }

        [HttpGet("school-history")]
        public async Task<IActionResult> GetSchoolEvaluatedHistory()
        {
            try
            {
                string? userId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not logged in." });
                }

                var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.AspNetUserID == userId);
                if (currentUser == null)
                {
                    return NotFound(new { success = false, message = "User profile not found." });
                }

                long instituteId = currentUser.UserID; 

                var list = await _context.ProjectEvaluations
                    .AsNoTracking()
                    .Where(x => x.InstituteID == instituteId)
                    .OrderByDescending(x => x.EvaluatedDate)
                    .ToListAsync();

                return Ok(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("student-history")]
        public async Task<IActionResult> GetStudentEvaluatedHistory()
        {
            try
            {
                string? userId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not logged in." });
                }

                var student = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.AspNetUserID == userId);

                if (student == null)
                {
                    return NotFound(new { success = false, message = "Student profile not found." });
                }

                int studentId = student.UserID; 

                var list = await _context.ProjectEvaluations
                    .AsNoTracking()
                    .Where(x => x.StudentID == studentId)
                    .OrderByDescending(x => x.EvaluatedDate)
                    .ToListAsync();

                return Ok(new { success = true, data = list });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

    }
}