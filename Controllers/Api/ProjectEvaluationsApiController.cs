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

        public ProjectEvaluationsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("dashboard-counts")]
        public async Task<IActionResult> GetDashboardCounts()
        {
            try
            {
                int totalSelCount = await _context.creativityActivitiesSubmission2021s
                    .AsNoTracking()
                    .Where(x => x.InstituteID != null)
                    .Select(x => x.InstituteID)
                    .Distinct()
                    .CountAsync();

                int familyCount = await _context.Set<HfUploadAnubhuti>()
                    .AsNoTracking()
                    .CountAsync();

                int individualCount = await _context.Set<HfTblUploadIndividualProject>()
                    .AsNoTracking()
                    .CountAsync();

                int groupPblCount = await _context.creativityActivitiesSubmission2021s
                    .AsNoTracking()
                    .CountAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalSelProjects = totalSelCount,
                        familyProjects = familyCount,
                        individualProjects = individualCount,
                        groupPbl = groupPblCount
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("list/{projectType}")]
        public async Task<IActionResult> GetProjectList(string projectType, int page = 1, int pageSize = 10, string searchTerm = "")
        {
            try
            {
                projectType = projectType?.Trim().ToLower() ?? "";
                searchTerm = searchTerm?.Trim().ToLower() ?? "";
                int skip = (page - 1) * pageSize;

                var institutes = await _context.Set<WJ_Institutes>()
                    .AsNoTracking()
                    .Where(i => i != null)
                    .ToDictionaryAsync(i => i.InstituteID, i => i.Name ?? "Unknown Institute");

                var studentAssociatesList = await _context.Set<WJ_StudentAssociates>()
                    .AsNoTracking()
                    .Where(x => x != null)
                    .ToListAsync();

                var studentInstitutes = studentAssociatesList
                    .GroupBy(x => x.StudentID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.FirstOrDefault(x => x.IsPrimaryAssociate == true)?.InstituteID ?? g.First().InstituteID
                    );

                var evaluations = await _context.ProjectEvaluations
                    .AsNoTracking()
                    .Where(x => x != null && x.ProjectType == projectType)
                    .ToDictionaryAsync(x => x.ProjectID, x => x);

                if (projectType == "family-projects")
                {
                    var rawData = await _context.Set<HfUploadAnubhuti>()
                        .AsNoTracking()
                        .Where(x => x != null)
                        .OrderByDescending(x => x.IsCreatedDate)
                        .ToListAsync();

                    var query = rawData.Select(f => {
                        int? studentId = f.iscreatedby;
                        int? instId = (studentId != null && studentInstitutes.ContainsKey(studentId.Value)) ? studentInstitutes[studentId.Value] : (int?)null;

                        string schoolName = "Unknown School";
                        if (instId != null && institutes.ContainsKey(instId.Value))
                            schoolName = institutes[instId.Value];
                        else if (!string.IsNullOrEmpty(f.CourseName))
                            schoolName = f.CourseName;

                        var eval = evaluations.ContainsKey(f.Id) ? evaluations[f.Id] : null;

                        return new
                        {
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
                            CreatedDate = f.IsCreatedDate,
                            EvaluatedColor = eval?.StatusColor ?? "",
                            EvaluatedMarks = eval?.Marks ?? 0,
                            IsEvaluated = eval != null
                        };
                    }).AsQueryable();

                    if (!string.IsNullOrEmpty(searchTerm))
                    {
                        query = query.Where(x => x.SchoolName.ToLower().Contains(searchTerm) || x.FirstName.ToLower().Contains(searchTerm) || x.Title.ToLower().Contains(searchTerm));
                    }

                    var totalRecords = query.Count();
                    var data = query.Skip(skip).Take(pageSize).ToList();

                    return Ok(new { success = true, totalRecords, data });
                }
                else if (projectType == "individual-projects")
                {
                    var rawData = await _context.Set<HfTblUploadIndividualProject>()
                        .AsNoTracking()
                        .Where(x => x != null)
                        .OrderByDescending(x => x.IsCreatedDate)
                        .ToListAsync();

                    var query = rawData.Select(ind => {
                        int? studentId = ind.Student_Id;
                        int? instId = (studentId != null && studentInstitutes.ContainsKey(studentId.Value)) ? studentInstitutes[studentId.Value] : (int?)null;

                        string schoolName = "Unknown School";
                        if (instId != null && institutes.ContainsKey(instId.Value))
                            schoolName = institutes[instId.Value];
                        else if (!string.IsNullOrEmpty(ind.CourseName))
                            schoolName = ind.CourseName;

                        var eval = evaluations.ContainsKey(ind.Id) ? evaluations[ind.Id] : null;

                        return new
                        {
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
                            CreatedDate = ind.IsCreatedDate,
                            EvaluatedColor = eval?.StatusColor ?? "",
                            EvaluatedMarks = eval?.Marks ?? 0,
                            IsEvaluated = eval != null
                        };
                    }).AsQueryable();

                    if (!string.IsNullOrEmpty(searchTerm))
                    {
                        query = query.Where(x => x.SchoolName.ToLower().Contains(searchTerm) || x.FirstName.ToLower().Contains(searchTerm) || x.Title.ToLower().Contains(searchTerm));
                    }

                    var totalRecords = query.Count();
                    var data = query.Skip(skip).Take(pageSize).ToList();

                    return Ok(new { success = true, totalRecords, data });
                }
                else if (projectType == "group-pbl")
                {
                    var rawData = await _context.creativityActivitiesSubmission2021s
                        .AsNoTracking()
                        .Where(x => x != null)
                        .OrderByDescending(x => x.CreatedDate)
                        .ToListAsync();

                    var query = rawData.Select(g => {
                        var eval = evaluations.ContainsKey(g.SubmissionID) ? evaluations[g.SubmissionID] : null;

                        string schoolName = "Unknown Institute";
                        if (g.InstituteID != null && institutes.ContainsKey(g.InstituteID.Value))
                        {
                            schoolName = institutes[g.InstituteID.Value];
                        }

                        

                        return new
                        {
                            Id = g.SubmissionID,
                            InstituteId = g.InstituteID,
                            StudentId = (int?)null,
                            SchoolName = schoolName,
                            Title = g.ShortDescription ?? "Group PBL Activity",
                            FirstName = "Team Members",
                            LastName = g.TeamMembersList ?? "",
                            FileLink = "",
                            ImageActivity = FormatWebPath(g.ImageActivity ?? string.Empty),
                            VideoActivity = FormatWebPath(g.VideoActivity ?? string.Empty),
                            Status = "Submitted",
                            CreatedDate = g.CreatedDate,
                            EvaluatedColor = eval?.StatusColor ?? "",
                            EvaluatedMarks = eval?.Marks ?? 0,
                            IsEvaluated = eval != null
                        };
                    }).AsQueryable();

                    if (!string.IsNullOrEmpty(searchTerm))
                    {
                        query = query.Where(x => x.SchoolName.ToLower().Contains(searchTerm));
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
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                model.EvaluatedByUserId = userId ?? string.Empty;
                model.EvaluatedDate = DateTime.UtcNow;

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
                    existing.EvaluatedDate = DateTime.UtcNow;
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
            var list = await _context.ProjectEvaluations
                .AsNoTracking()
                .OrderByDescending(x => x.EvaluatedDate)
                .Take(50)
                .ToListAsync();

            return Ok(new { success = true, data = list });
        }

        private static string FormatWebPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string normalized = path.Replace("\\", "/");
            if (normalized.Contains("Uploads/"))
            {
                int index = normalized.IndexOf("Uploads/");
                return "https://hemvirtues.com/" + normalized.Substring(index);
            }
            return normalized;
        }
    }
}