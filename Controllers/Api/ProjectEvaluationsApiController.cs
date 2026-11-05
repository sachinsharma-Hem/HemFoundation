using HemFoundation.Data;
using HemFoundation.Models;
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
        public async Task<IActionResult> GetProjectList(string projectType, int page = 1, int pageSize = 3000, string searchTerm = "", int? month = null, int? year = null)
        {
            try
            {
                projectType = projectType?.Trim().ToLower() ?? "";
                searchTerm = searchTerm?.Trim().ToLower() ?? "";
                int skip = (page - 1) * pageSize;

                // 1. Institutes & Students
                var institutes = await _context.Set<WJ_Institutes>().AsNoTracking()
                    .Where(i => i != null).ToDictionaryAsync(i => i.InstituteID, i => i.Name ?? "Unknown Institute");

                var studentAssociatesList = await _context.Set<WJ_StudentAssociates>().AsNoTracking()
                    .Where(x => x != null).ToListAsync();

                var studentInstitutes = studentAssociatesList
                    .GroupBy(x => x.StudentID)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault(x => x.IsPrimaryAssociate == true)?.InstituteID ?? g.First().InstituteID);

                // 2. Student Standard fetch (WJ_Students)
                var studentStandardsRaw = await _context.Set<WJ_Students>().AsNoTracking()
                    .Where(s => s.StudentID != null)
                    .Select(s => new { s.StudentID, s.Standard })
                    .ToListAsync();

                var studentStandards = studentStandardsRaw
                    .GroupBy(s => s.StudentID)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.Standard ?? "");

                // 3. Courses Dictionary (Course Name -> Course ID)
                var coursesListRaw = await _context.Set<Courses>().AsNoTracking()
                    .Where(c => !string.IsNullOrEmpty(c.CourseName))
                    .Select(c => new { c.CourseName, c.CourseID })
                    .ToListAsync();

                var courseNameToIdDict = coursesListRaw
                    .GroupBy(c => c.CourseName.Trim().ToLower())
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault().CourseID);

                // 4. WJ_Syllabus Dictionary (MainCourse Mapping)
                var syllabusRaw = await _context.CourseSyllabuses.AsNoTracking()
    .Where(s => s.MainCourseId != null)
    .Select(s => new { CourseID = s.CourseId, MainCourseID = s.MainCourseId })
    .ToListAsync();

                var courseIdToMainCourseId = syllabusRaw
                    .GroupBy(s => s.CourseID)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault().MainCourseID);

                var mainCourseIdToCourseIds = syllabusRaw
                    .GroupBy(s => s.MainCourseID)
                    .ToDictionary(g => g.Key, g => g.Select(s => s.CourseID).ToList());



                var masterItems = await _context.vw_valueitemmasters.AsNoTracking().ToListAsync();

                var courseNames = masterItems
                    .Where(x => x.MainCourseId != 0)
                    .GroupBy(x => x.MainCourseId)
                    .ToDictionary(g => g.Key, g => g.First().ValueTitleEng ?? g.First().ValueTitleEng);

                var standards = masterItems
                    .Where(x => x.MainCourseId != 0 && !string.IsNullOrEmpty(x.GradeId))
                    .GroupBy(x => x.MainCourseId)
                    .ToDictionary(g => g.Key, g => g.First().GradeId);

                var MpracticalFiles = masterItems
                    .Where(x => x.MainCourseId != 0 && !string.IsNullOrEmpty(x.ActivityPdfUrl))
                    .GroupBy(x => x.MainCourseId)
                    .ToDictionary(g => g.Key, g => g.First().ActivityPdfUrl);

                var allowedCodesForEval = new List<string>();
                if (projectType == "family-projects")
                {
                    allowedCodesForEval.Add("G_E_01");
                }
                else if (projectType == "individual-projects")
                {
                    allowedCodesForEval.Add("G_E_011");
                }
                else
                {
                    allowedCodesForEval.AddRange(new[] { "G_E_010", "G_E_10", "G_E_011" });
                }

                var practicalFilesRaw = await _context.Set<WJ_Practicals>().AsNoTracking()
                    .Where(p => !string.IsNullOrEmpty(p.Resources) && allowedCodesForEval.Contains(p.Code))
                    .Select(p => new { p.CourseID, p.Resources })
                    .ToListAsync();

                var practicalFiles = practicalFilesRaw
                    .GroupBy(p => p.CourseID)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.Resources ?? "");

                var evaluations = await _context.ProjectEvaluations.AsNoTracking()
                    .Where(x => x != null && x.ProjectType == projectType).ToDictionaryAsync(x => x.ProjectID, x => x);

                if (projectType == "family-projects")
                {
                    var rawData = await _context.Set<HfUploadAnubhuti>().AsNoTracking().Where(x => x != null).ToListAsync();

                    if (month.HasValue && year.HasValue)
                        rawData = rawData.Where(x => x.IsCreatedDate.HasValue && x.IsCreatedDate.Value.Month == month.Value && x.IsCreatedDate.Value.Year == year.Value).ToList();

                    var query = rawData.Select(f => {
                        int? studentId = f.iscreatedby;
                        int? instId = (studentId != null && studentInstitutes.ContainsKey(studentId.Value)) ? studentInstitutes[studentId.Value] : (int?)null;
                        string schoolName = (instId != null && institutes.ContainsKey(instId.Value)) ? institutes[instId.Value] : "Unknown School";
                        var eval = evaluations.ContainsKey(f.Id) ? evaluations[f.Id] : null;

                        string courseName = f.CourseName ?? "";
                        string standard = (studentId != null && studentStandards.ContainsKey(studentId.Value)) ? studentStandards[studentId.Value] : "";

                        int courseId = 0;
                        string normalizedCourseName = courseName.Trim().ToLower();
                        if (!string.IsNullOrEmpty(normalizedCourseName))
                        {
                            if (!courseNameToIdDict.TryGetValue(normalizedCourseName, out courseId))
                            {
                                var match = courseNameToIdDict.FirstOrDefault(c => c.Key.Contains(normalizedCourseName) || normalizedCourseName.Contains(c.Key));
                                if (match.Key != null) courseId = match.Value;
                            }
                        }

                        string evalFileLink = "";
                        if (courseId != 0)
                        {
                            if (courseIdToMainCourseId.TryGetValue(courseId, out int? mainCourseId))
                            {
                                if (mainCourseIdToCourseIds.TryGetValue(mainCourseId, out var relatedCourseIds))
                                {
                                    foreach (var relId in relatedCourseIds)
                                    {
                                        if (practicalFiles.TryGetValue(relId, out string res))
                                        {
                                            evalFileLink = FormatWebPath(res);
                                            break;
                                        }
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(evalFileLink) && practicalFiles.TryGetValue(courseId, out string directRes))
                            {
                                evalFileLink = FormatWebPath(directRes);
                            }
                        }

                        return new
                        {
                            Id = f.Id,
                            InstituteId = instId,
                            StudentId = studentId,
                            CourseName = courseName,
                            SchoolName = schoolName,
                            Standard = standard,
                            Title = f.Title ?? f.CourseName ?? "Anubhuti Family Project",
                            FirstName = f.FirstName ?? "",
                            LastName = f.LastName ?? "",
                            FileLink = FormatWebPath(f.FileLink ?? string.Empty),
                            EvaluationFileUrl = evalFileLink,
                            Status = f.Status ?? "Pending",
                            CreatedDate = f.IsCreatedDate?.ToString("yyyy-MM-dd") ?? "",
                            EvaluatedColor = eval?.StatusColor ?? "",
                            EvaluatedMarks = eval?.Marks ?? 0,
                            IsEvaluated = eval != null
                        };
                    }).AsQueryable();

                    if (!string.IsNullOrEmpty(searchTerm))
                        query = query.Where(x => x.SchoolName.ToLower().Contains(searchTerm) || x.FirstName.ToLower().Contains(searchTerm) || x.Title.ToLower().Contains(searchTerm) || x.Status.ToLower().Contains(searchTerm) || x.EvaluatedColor.ToLower().Contains(searchTerm));

                    query = query.OrderBy(x => x.CourseName).ThenBy(x => x.Standard);
                    var totalRecords = query.Count();
                    var data = query.Skip(skip).Take(pageSize).ToList();
                    return Ok(new { success = true, totalRecords, data });
                }
                else if (projectType == "individual-projects")
                {
                    var rawData = await _context.Set<HfTblUploadIndividualProject>().AsNoTracking().Where(x => x != null).ToListAsync();

                    if (month.HasValue && year.HasValue)
                        rawData = rawData.Where(x => x.IsCreatedDate.HasValue && x.IsCreatedDate.Value.Month == month.Value && x.IsCreatedDate.Value.Year == year.Value).ToList();

                    var query = rawData.Select(ind => {
                        int? studentId = ind.Iscreatedby;
                        int? instId = (studentId != null && studentInstitutes.ContainsKey(studentId.Value)) ? studentInstitutes[studentId.Value] : (int?)null;
                        string schoolName = (instId != null && institutes.ContainsKey(instId.Value)) ? institutes[instId.Value] : "Unknown School";
                        var eval = evaluations.ContainsKey(ind.Id) ? evaluations[ind.Id] : null;

                        string courseName = ind.CourseName ?? "";
                        string standard = (studentId != null && studentStandards.ContainsKey(studentId.Value)) ? studentStandards[studentId.Value] : "";

                        int courseId = 0;
                        string normalizedCourseName = courseName.Trim().ToLower();
                        if (!string.IsNullOrEmpty(normalizedCourseName))
                        {
                            if (!courseNameToIdDict.TryGetValue(normalizedCourseName, out courseId))
                            {
                                var match = courseNameToIdDict.FirstOrDefault(c => c.Key.Contains(normalizedCourseName) || normalizedCourseName.Contains(c.Key));
                                if (match.Key != null) courseId = match.Value;
                            }
                        }

                        string evalFileLink = "";
                        if (courseId != 0)
                        {
                            if (courseIdToMainCourseId.TryGetValue(courseId, out int? mainCourseId))
                            {
                                if (mainCourseIdToCourseIds.TryGetValue(mainCourseId, out var relatedCourseIds))
                                {
                                    foreach (var relId in relatedCourseIds)
                                    {
                                        if (practicalFiles.TryGetValue(relId, out string res))
                                        {
                                            evalFileLink = FormatWebPath(res);
                                            break;
                                        }
                                    }
                                }
                            }
                            if (string.IsNullOrEmpty(evalFileLink) && practicalFiles.TryGetValue(courseId, out string directRes))
                            {
                                evalFileLink = FormatWebPath(directRes);
                            }
                        }

                        return new
                        {
                            Id = ind.Id,
                            InstituteId = instId,
                            StudentId = studentId,
                            CourseName = courseName,
                            SchoolName = schoolName,
                            Standard = standard,
                            Title = ind.Title ?? ind.CourseName ?? "Individual PBL Project",
                            FirstName = ind.FirstName ?? "",
                            LastName = ind.LastName ?? "",
                            FileLink = FormatWebPath(ind.FileLink ?? string.Empty),
                            EvaluationFileUrl = evalFileLink,
                            Status = ind.Status ?? "Pending",
                            CreatedDate = ind.IsCreatedDate?.ToString("yyyy-MM-dd") ?? "",
                            EvaluatedColor = eval?.StatusColor ?? "",
                            EvaluatedMarks = eval?.Marks ?? 0,
                            IsEvaluated = eval != null
                        };
                    }).AsQueryable();

                    if (!string.IsNullOrEmpty(searchTerm))
                        query = query.Where(x => x.SchoolName.ToLower().Contains(searchTerm) || x.FirstName.ToLower().Contains(searchTerm) || x.Title.ToLower().Contains(searchTerm) || x.Status.ToLower().Contains(searchTerm) || x.EvaluatedColor.ToLower().Contains(searchTerm));

                    query = query.OrderBy(x => x.CourseName).ThenBy(x => x.Standard);
                    var totalRecords = query.Count();
                    var data = query.Skip(skip).Take(pageSize).ToList();
                    return Ok(new { success = true, totalRecords, data });
                }
                else if (projectType == "group-pbl")
                {
                    var rawData = await _context.creativityActivitiesSubmission2021s.AsNoTracking().Where(x => x != null).ToListAsync();

                    if (month.HasValue && year.HasValue)
                        rawData = rawData.Where(x => x.CreatedDate.HasValue && x.CreatedDate.Value.Month == month.Value && x.CreatedDate.Value.Year == year.Value).ToList();

                    // Ensure you have pre-loaded lookup dictionaries from vw_valueitemmasters:
                    // e.g., 
                    // var courseNames = ... (keyed by MainCourseID or Id)
                    // var standards = ... (keyed by MainCourseID or Id, returning `grade`)
                    // var practicalFiles = ... (keyed by MainCourseID or Id, returning `activitypdfurl`)

                    var query = rawData.Select(g => {
                        var eval = evaluations.ContainsKey(g.SubmissionID) ? evaluations[g.SubmissionID] : null;
                        string schoolName = (g.InstituteID != null && institutes.ContainsKey(g.InstituteID.Value)) ? institutes[g.InstituteID.Value] : "Unknown Institute";

                        int mainCourseId = g.MainCourseID ?? 0;

                        // Retrieve CourseName, Standard (Grade), and Evaluation File URL directly using MainCourseID
                        string courseName = (mainCourseId != 0 && courseNames.ContainsKey(mainCourseId)) ? courseNames[mainCourseId] : (g.MainCourseID?.ToString() ?? "");
                        string standard = (mainCourseId != 0 && standards.ContainsKey(mainCourseId)) ? standards[mainCourseId] : "";

                        string evalFileLink = "";
                        if (mainCourseId != 0 && MpracticalFiles.TryGetValue(mainCourseId, out string res))
                        {
                            evalFileLink = FormatWebPath(res);
                        }

                        return new
                        {
                            Id = g.SubmissionID,
                            InstituteId = g.InstituteID,
                            StudentId = (int?)null,
                            CourseName = courseName,
                            SchoolName = schoolName,
                            Standard = standard,
                            Title = g.ShortDescription ?? "Group PBL Activity",
                            FirstName = "Team Members",
                            LastName = g.TeamMembersList ?? "",
                            FileLink = "",
                            ImageActivity = FormatWebPath(g.ImageActivity ?? string.Empty),
                            VideoActivity = FormatWebPath(g.VideoActivity ?? string.Empty),
                            EvaluationFileUrl = evalFileLink,
                            Status = "Submitted",
                            CreatedDate = g.CreatedDate?.ToString("yyyy-MM-dd") ?? "",
                            EvaluatedColor = eval?.StatusColor ?? "",
                            EvaluatedMarks = eval?.Marks ?? 0,
                            IsEvaluated = eval != null
                        };
                    }).AsQueryable();

                    if (!string.IsNullOrEmpty(searchTerm))
                        query = query.Where(x => x.SchoolName.ToLower().Contains(searchTerm) || x.Title.ToLower().Contains(searchTerm) || x.LastName.ToLower().Contains(searchTerm) || x.EvaluatedColor.ToLower().Contains(searchTerm));

                    query = query.OrderBy(x => x.CourseName).ThenBy(x => x.Standard);
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
       
        [HttpGet("list1/{projectType}")]
        public async Task<IActionResult> GetProjectList1(string projectType, int page = 1, int pageSize = 10, string searchTerm = "", int? month = null, int? year = null)
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

                var evaluations = await _context.ProjectEvaluations
                    .AsNoTracking()
                    .Where(x => x.InstituteID == instituteId)
                    .OrderByDescending(x => x.EvaluatedDate)
                    .ToListAsync();

                var studentIds = evaluations
                    .Where(x => x.StudentID.HasValue)
                    .Select(x => x.StudentID.Value)
                    .Distinct()
                    .ToList();

                var studentDict = await _context.Users
                    .AsNoTracking()
                    .Where(u => studentIds.Contains(u.UserID))
                    .ToDictionaryAsync(
                        u => u.UserID,
                        u => $"{u.FirstName ?? ""} {u.LastName ?? ""}".Trim()
                    );

                // Fetch student standards mapping
                var studentStandards = await _context.Set<WJ_Students>().AsNoTracking()
                    .Where(s => s.StudentID != null && studentIds.Contains((int)s.StudentID))
                    .Select(s => new { s.StudentID, s.Standard })
                    .ToListAsync();
                var studentStdDict = studentStandards.GroupBy(s => s.StudentID).ToDictionary(g => g.Key, g => g.FirstOrDefault()?.Standard ?? "");

                // Fetch Courses & Syllabus mappings for PBL file lookup
                var coursesListRaw = await _context.Set<Courses>().AsNoTracking()
                    .Where(c => !string.IsNullOrEmpty(c.CourseName))
                    .Select(c => new { c.CourseName, c.CourseID })
                    .ToListAsync();

                var courseNameToIdDict = coursesListRaw
                    .GroupBy(c => c.CourseName.Trim().ToLower())
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault().CourseID);

                var syllabusRaw = await _context.CourseSyllabuses.AsNoTracking()
                    .Where(s => s.MainCourseId != null)
                    .Select(s => new { CourseID = s.CourseId, MainCourseID = s.MainCourseId })
                    .ToListAsync();

                var courseIdToMainCourseId = syllabusRaw
                    .GroupBy(s => s.CourseID)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault().MainCourseID);

                var mainCourseIdToCourseIds = syllabusRaw
                    .GroupBy(s => s.MainCourseID)
                    .ToDictionary(g => g.Key, g => g.Select(s => s.CourseID).ToList());

                var masterItems = await _context.vw_valueitemmasters.AsNoTracking().ToListAsync();
                var MpracticalFiles = masterItems.Where(x => x.MainCourseId != 0 && !string.IsNullOrEmpty(x.ActivityPdfUrl))
                    .GroupBy(x => x.MainCourseId).ToDictionary(g => g.Key, g => g.First().ActivityPdfUrl);

                var allowedCodesForEval = new List<string> { "G_E_01", "G_E_011", "G_E_010", "G_E_10" };
                var practicalFilesRaw = await _context.Set<WJ_Practicals>().AsNoTracking()
                    .Where(p => !string.IsNullOrEmpty(p.Resources) && allowedCodesForEval.Contains(p.Code))
                    .Select(p => new { p.CourseID, p.Resources })
                    .ToListAsync();

                var practicalFiles = practicalFilesRaw
                    .GroupBy(p => p.CourseID)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.Resources ?? "");

                var groupSubmissions = await _context.creativityActivitiesSubmission2021s.AsNoTracking()
                    .Where(g => g.InstituteID == instituteId).ToDictionaryAsync(g => g.SubmissionID, g => g);

                var individualProjects = await _context.Set<HfTblUploadIndividualProject>().AsNoTracking().ToListAsync();
                var indDict = individualProjects.ToDictionary(ind => ind.Id, ind => ind);

                var anubhutiProjects = await _context.Set<HfUploadAnubhuti>().AsNoTracking().ToListAsync();
                var anubhutiDict = anubhutiProjects.ToDictionary(a => a.Id, a => a);

                string schoolName = $"{currentUser.FirstName} {currentUser.LastName}".Trim();

                var resultList = evaluations.Select(x => {
                    string studentName = "Group / Team Project";
                    if (x.StudentID.HasValue && studentDict.ContainsKey(x.StudentID.Value))
                    {
                        studentName = studentDict[x.StudentID.Value];
                        if (string.IsNullOrEmpty(studentName)) studentName = "Unknown Student";
                    }

                    string standard = "";
                    string imageActivity = "";
                    string videoActivity = "";
                    string evaluationFileUrl = "";

                    if (x.StudentID.HasValue && studentStdDict.ContainsKey(x.StudentID.Value))
                    {
                        standard = studentStdDict[x.StudentID.Value];
                    }

                    // Helper lambda to resolve PBL file link by course name
                    Func<string, string> getEvalFileUrlByCourse = (string cName) => {
                        if (string.IsNullOrEmpty(cName)) return "";
                        int cId = 0;
                        string normName = cName.Trim().ToLower();
                        if (courseNameToIdDict.TryGetValue(normName, out cId) ||
                            (courseNameToIdDict.FirstOrDefault(c => c.Key.Contains(normName) || normName.Contains(c.Key)).Key != null))
                        {
                            if (cId == 0) cId = courseNameToIdDict.FirstOrDefault(c => c.Key.Contains(normName) || normName.Contains(c.Key)).Value;
                        }

                        if (cId != 0)
                        {
                            if (courseIdToMainCourseId.TryGetValue(cId, out int? mId) && mId.HasValue)
                            {
                                if (mainCourseIdToCourseIds.TryGetValue(mId.Value, out var relIds))
                                {
                                    foreach (var rId in relIds)
                                    {
                                        if (practicalFiles.TryGetValue(rId, out string res)) return FormatWebPath(res);
                                    }
                                }
                                if (MpracticalFiles.TryGetValue(mId.Value, out string mRes)) return FormatWebPath(mRes);
                            }
                            if (practicalFiles.TryGetValue(cId, out string dRes)) return FormatWebPath(dRes);
                        }
                        return "";
                    };

                    // Map specific project type details (Media & PBL files)
                    if (x.ProjectType == "group-pbl" && groupSubmissions.ContainsKey(x.ProjectID))
                    {
                        var grp = groupSubmissions[x.ProjectID];
                        imageActivity = FormatWebPath(grp.ImageActivity ?? "");
                        videoActivity = FormatWebPath(grp.VideoActivity ?? "");
                        if (grp.MainCourseID.HasValue && MpracticalFiles.ContainsKey(grp.MainCourseID.Value))
                        {
                            evaluationFileUrl = FormatWebPath(MpracticalFiles[grp.MainCourseID.Value]);
                        }
                    }
                    else if (x.ProjectType == "individual-projects" && indDict.ContainsKey(x.ProjectID))
                    {
                        var ind = indDict[x.ProjectID];
                        imageActivity = FormatWebPath(ind.FileLink ?? "");
                        evaluationFileUrl = getEvalFileUrlByCourse(ind.CourseName ?? x.CourseName ?? "");
                    }
                    else if (x.ProjectType == "family-projects" && anubhutiDict.ContainsKey(x.ProjectID))
                    {
                        var fam = anubhutiDict[x.ProjectID];
                        imageActivity = FormatWebPath(fam.FileLink ?? "");
                        evaluationFileUrl = getEvalFileUrlByCourse(fam.CourseName ?? x.CourseName ?? "");
                    }

                    return new
                    {
                        x.EvaluationID,
                        x.ProjectID,
                        x.ProjectType,
                        x.StudentID,
                        x.InstituteID,
                        StudentName = studentName,
                        SchoolName = schoolName,
                        x.CourseName,
                        Standard = standard,
                        EvaluationFileUrl = evaluationFileUrl,
                        ImageActivity = imageActivity,
                        VideoActivity = videoActivity,
                        x.StatusColor,
                        x.Marks,
                        x.Description,
                        x.EvaluatedByUserId,
                        x.EvaluatedDate
                    };
                }).ToList();

                return Ok(new { success = true, data = resultList });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("affiliate-history")]
        public async Task<IActionResult> GetStudentEvaluatedHistory()
        {
            try
            {
                string? userId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not logged in." });
                }

                var affiliateUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.AspNetUserID == userId);

                if (affiliateUser == null)
                {
                    return NotFound(new { success = false, message = "Affiliate profile not found." });
                }

                int affiliateUserId = affiliateUser.UserID;

                var referralCodeRecord = await _context.Set<WJ_ReferralCodes>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.UserID == affiliateUserId);

                if (referralCodeRecord == null || string.IsNullOrEmpty(referralCodeRecord.ReferralCode))
                {
                    return Ok(new { success = true, data = new List<object>() });
                }

                string referralCode = referralCodeRecord.ReferralCode;

                var instituteDict = await _context.Set<WJ_Institutes>()
                    .AsNoTracking()
                    .Where(i => i.ReferralCode == referralCode)
                    .ToDictionaryAsync(i => (int)i.InstituteID, i => i.Name ?? "Unknown School");

                var instituteIds = instituteDict.Keys.ToList();

                if (!instituteIds.Any())
                {
                    return Ok(new { success = true, data = new List<object>() });
                }

                var studentIds = await _context.Set<WJ_StudentAssociates>()
                    .AsNoTracking()
                    .Where(s => instituteIds.Contains(s.InstituteID))
                    .Select(s => s.StudentID)
                    .Distinct()
                    .ToListAsync();

                var studentDict = await _context.Users
                    .AsNoTracking()
                    .Where(u => studentIds.Contains(u.UserID))
                    .ToDictionaryAsync(
                        u => u.UserID,
                        u => $"{u.FirstName ?? ""} {u.LastName ?? ""}".Trim()
                    );

                // Fetch student standards mapping
                var studentStandards = await _context.Set<WJ_Students>().AsNoTracking()
                    .Where(s => s.StudentID != null && studentIds.Contains((int)s.StudentID))
                    .Select(s => new { s.StudentID, s.Standard })
                    .ToListAsync();
                var studentStdDict = studentStandards.GroupBy(s => s.StudentID).ToDictionary(g => g.Key, g => g.FirstOrDefault()?.Standard ?? "");

                var coursesListRaw = await _context.Set<Courses>().AsNoTracking()
                    .Where(c => !string.IsNullOrEmpty(c.CourseName))
                    .Select(c => new { c.CourseName, c.CourseID })
                    .ToListAsync();

                var courseNameToIdDict = coursesListRaw
                    .GroupBy(c => c.CourseName.Trim().ToLower())
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault().CourseID);

                var syllabusRaw = await _context.CourseSyllabuses.AsNoTracking()
                    .Where(s => s.MainCourseId != null)
                    .Select(s => new { CourseID = s.CourseId, MainCourseID = s.MainCourseId })
                    .ToListAsync();

                var courseIdToMainCourseId = syllabusRaw
                    .GroupBy(s => s.CourseID)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault().MainCourseID);

                var mainCourseIdToCourseIds = syllabusRaw
                    .GroupBy(s => s.MainCourseID)
                    .ToDictionary(g => g.Key, g => g.Select(s => s.CourseID).ToList());

                var masterItems = await _context.vw_valueitemmasters.AsNoTracking().ToListAsync();
                var MpracticalFiles = masterItems.Where(x => x.MainCourseId != 0 && !string.IsNullOrEmpty(x.ActivityPdfUrl))
                    .GroupBy(x => x.MainCourseId).ToDictionary(g => g.Key, g => g.First().ActivityPdfUrl);

                var allowedCodesForEval = new List<string> { "G_E_01", "G_E_011", "G_E_010", "G_E_10" };
                var practicalFilesRaw = await _context.Set<WJ_Practicals>().AsNoTracking()
                    .Where(p => !string.IsNullOrEmpty(p.Resources) && allowedCodesForEval.Contains(p.Code))
                    .Select(p => new { p.CourseID, p.Resources })
                    .ToListAsync();

                var practicalFiles = practicalFilesRaw
                    .GroupBy(p => p.CourseID)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.Resources ?? "");

                var groupSubmissions = await _context.creativityActivitiesSubmission2021s.AsNoTracking().ToDictionaryAsync(g => g.SubmissionID, g => g);
                var individualProjects = await _context.Set<HfTblUploadIndividualProject>().AsNoTracking().ToDictionaryAsync(ind => ind.Id, ind => ind);
                var anubhutiProjects = await _context.Set<HfUploadAnubhuti>().AsNoTracking().ToDictionaryAsync(a => a.Id, a => a);

                var evaluations = await _context.ProjectEvaluations
                    .AsNoTracking()
                    .Where(x => (x.InstituteID != null && instituteIds.Contains(x.InstituteID.Value)) ||
                                (x.StudentID != null && studentIds.Contains(x.StudentID.Value)))
                    .OrderByDescending(x => x.EvaluatedDate)
                    .ToListAsync();

                var resultList = evaluations.Select(x => {
                    string schoolName = "N/A";
                    if (x.InstituteID.HasValue && instituteDict.ContainsKey(x.InstituteID.Value))
                    {
                        schoolName = instituteDict[x.InstituteID.Value];
                    }

                    string studentName = "Group / Team Project";
                    if (x.StudentID.HasValue && studentDict.ContainsKey(x.StudentID.Value))
                    {
                        studentName = studentDict[x.StudentID.Value];
                        if (string.IsNullOrEmpty(studentName)) studentName = "Unknown Student";
                    }

                    string standard = "";
                    string imageActivity = "";
                    string videoActivity = "";
                    string evaluationFileUrl = "";

                    if (x.StudentID.HasValue && studentStdDict.ContainsKey(x.StudentID.Value))
                    {
                        standard = studentStdDict[x.StudentID.Value];
                    }

                    Func<string, string> getEvalFileUrlByCourse = (string cName) => {
                        if (string.IsNullOrEmpty(cName)) return "";
                        int cId = 0;
                        string normName = cName.Trim().ToLower();
                        if (courseNameToIdDict.TryGetValue(normName, out cId) ||
                            (courseNameToIdDict.FirstOrDefault(c => c.Key.Contains(normName) || normName.Contains(c.Key)).Key != null))
                        {
                            if (cId == 0) cId = courseNameToIdDict.FirstOrDefault(c => c.Key.Contains(normName) || normName.Contains(c.Key)).Value;
                        }

                        if (cId != 0)
                        {
                            if (courseIdToMainCourseId.TryGetValue(cId, out int? mId) && mId.HasValue)
                            {
                                if (mainCourseIdToCourseIds.TryGetValue(mId.Value, out var relIds))
                                {
                                    foreach (var rId in relIds)
                                    {
                                        if (practicalFiles.TryGetValue(rId, out string res)) return FormatWebPath(res);
                                    }
                                }
                                if (MpracticalFiles.TryGetValue(mId.Value, out string mRes)) return FormatWebPath(mRes);
                            }
                            if (practicalFiles.TryGetValue(cId, out string dRes)) return FormatWebPath(dRes);
                        }
                        return "";
                    };

                    if (x.ProjectType == "group-pbl" && groupSubmissions.ContainsKey(x.ProjectID))
                    {
                        var grp = groupSubmissions[x.ProjectID];
                        imageActivity = FormatWebPath(grp.ImageActivity ?? "");
                        videoActivity = FormatWebPath(grp.VideoActivity ?? "");
                        if (grp.MainCourseID.HasValue && MpracticalFiles.ContainsKey(grp.MainCourseID.Value))
                        {
                            evaluationFileUrl = FormatWebPath(MpracticalFiles[grp.MainCourseID.Value]);
                        }
                    }
                    else if (x.ProjectType == "individual-projects" && individualProjects.ContainsKey(x.ProjectID))
                    {
                        var ind = individualProjects[x.ProjectID];
                        imageActivity = FormatWebPath(ind.FileLink ?? "");
                        evaluationFileUrl = getEvalFileUrlByCourse(ind.CourseName ?? x.CourseName ?? "");
                    }
                    else if (x.ProjectType == "family-projects" && anubhutiProjects.ContainsKey(x.ProjectID))
                    {
                        var fam = anubhutiProjects[x.ProjectID];
                        imageActivity = FormatWebPath(fam.FileLink ?? "");
                        evaluationFileUrl = getEvalFileUrlByCourse(fam.CourseName ?? x.CourseName ?? "");
                    }

                    return new
                    {
                        x.EvaluationID,
                        x.ProjectID,
                        x.ProjectType,
                        x.StudentID,
                        x.InstituteID,
                        StudentName = studentName,
                        SchoolName = schoolName,
                        x.CourseName,
                        Standard = standard,
                        EvaluationFileUrl = evaluationFileUrl,
                        ImageActivity = imageActivity,
                        VideoActivity = videoActivity,
                        x.StatusColor,
                        x.Marks,
                        x.Description,
                        x.EvaluatedByUserId,
                        x.EvaluatedDate
                    };
                }).ToList();

                return Ok(new { success = true, data = resultList });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        //[HttpGet("school-history")]
        //public async Task<IActionResult> GetSchoolEvaluatedHistory()
        //{
        //    try
        //    {
        //        string? userId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        //        if (string.IsNullOrEmpty(userId))
        //        {
        //            return Unauthorized(new { success = false, message = "User not logged in." });
        //        }

        //        var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.AspNetUserID == userId);
        //        if (currentUser == null)
        //        {
        //            return NotFound(new { success = false, message = "User profile not found." });
        //        }

        //        long instituteId = currentUser.UserID;

        //        var evaluations = await _context.ProjectEvaluations
        //            .AsNoTracking()
        //            .Where(x => x.InstituteID == instituteId)
        //            .OrderByDescending(x => x.EvaluatedDate)
        //            .ToListAsync();

        //        var studentIds = evaluations
        //            .Where(x => x.StudentID.HasValue)
        //            .Select(x => x.StudentID.Value)
        //            .Distinct()
        //            .ToList();

        //        var studentDict = await _context.Users
        //            .AsNoTracking()
        //            .Where(u => studentIds.Contains(u.UserID))
        //            .ToDictionaryAsync(
        //                u => u.UserID,
        //                u => $"{u.FirstName ?? ""} {u.LastName ?? ""}".Trim()
        //            );

        //        string schoolName = $"{currentUser.FirstName} {currentUser.LastName}".Trim();

        //        var resultList = evaluations.Select(x => {
        //            string studentName = "Group / Team Project";
        //            if (x.StudentID.HasValue && studentDict.ContainsKey(x.StudentID.Value))
        //            {
        //                studentName = studentDict[x.StudentID.Value];
        //                if (string.IsNullOrEmpty(studentName)) studentName = "Unknown Student";
        //            }

        //            return new
        //            {
        //                x.EvaluationID,
        //                x.ProjectID,
        //                x.ProjectType,
        //                x.StudentID,
        //                x.InstituteID,
        //                StudentName = studentName,
        //                SchoolName = schoolName,
        //                x.CourseName,
        //                x.StatusColor,
        //                x.Marks,
        //                x.Description,
        //                x.EvaluatedByUserId,
        //                x.EvaluatedDate
        //            };
        //        }).ToList();

        //        return Ok(new { success = true, data = resultList });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, message = ex.Message });
        //    }
        //}

        //[HttpGet("affiliate-history")]
        //public async Task<IActionResult> GetStudentEvaluatedHistory()
        //{
        //    try
        //    {
        //        string? userId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        //        if (string.IsNullOrEmpty(userId))
        //        {
        //            return Unauthorized(new { success = false, message = "User not logged in." });
        //        }

        //        var affiliateUser = await _context.Users
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(s => s.AspNetUserID == userId);

        //        if (affiliateUser == null)
        //        {
        //            return NotFound(new { success = false, message = "Affiliate profile not found." });
        //        }

        //        int affiliateUserId = affiliateUser.UserID;

        //        var referralCodeRecord = await _context.Set<WJ_ReferralCodes>()
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(r => r.UserID == affiliateUserId);

        //        if (referralCodeRecord == null || string.IsNullOrEmpty(referralCodeRecord.ReferralCode))
        //        {
        //            return Ok(new { success = true, data = new List<object>() });
        //        }

        //        string referralCode = referralCodeRecord.ReferralCode;

        //        var instituteDict = await _context.Set<WJ_Institutes>()
        //            .AsNoTracking()
        //            .Where(i => i.ReferralCode == referralCode)
        //            .ToDictionaryAsync(i => (int)i.InstituteID, i => i.Name ?? "Unknown School");

        //        var instituteIds = instituteDict.Keys.ToList();

        //        if (!instituteIds.Any())
        //        {
        //            return Ok(new { success = true, data = new List<object>() });
        //        }

        //        var studentIds = await _context.Set<WJ_StudentAssociates>()
        //            .AsNoTracking()
        //            .Where(s => instituteIds.Contains(s.InstituteID))
        //            .Select(s => s.StudentID)
        //            .Distinct()
        //            .ToListAsync();

        //        var studentDict = await _context.Users
        //            .AsNoTracking()
        //            .Where(u => studentIds.Contains(u.UserID))
        //            .ToDictionaryAsync(
        //                u => u.UserID,
        //                u => $"{u.FirstName ?? ""} {u.LastName ?? ""}".Trim()
        //            );

        //        var evaluations = await _context.ProjectEvaluations
        //            .AsNoTracking()
        //            .Where(x => (x.InstituteID != null && instituteIds.Contains(x.InstituteID.Value)) ||
        //                        (x.StudentID != null && studentIds.Contains(x.StudentID.Value)))
        //            .OrderByDescending(x => x.EvaluatedDate)
        //            .ToListAsync();

        //        var resultList = evaluations.Select(x => {
        //            string schoolName = "N/A";
        //            if (x.InstituteID.HasValue && instituteDict.ContainsKey(x.InstituteID.Value))
        //            {
        //                schoolName = instituteDict[x.InstituteID.Value];
        //            }

        //            string studentName = "Group / Team Project";
        //            if (x.StudentID.HasValue && studentDict.ContainsKey(x.StudentID.Value))
        //            {
        //                studentName = studentDict[x.StudentID.Value];
        //                if (string.IsNullOrEmpty(studentName)) studentName = "Unknown Student";
        //            }

        //            return new
        //            {
        //                x.EvaluationID,
        //                x.ProjectID,
        //                x.ProjectType,
        //                x.StudentID,
        //                x.InstituteID,
        //                StudentName = studentName,
        //                SchoolName = schoolName,
        //                x.CourseName,
        //                x.StatusColor,
        //                x.Marks,
        //                x.Description,
        //                x.EvaluatedByUserId,
        //                x.EvaluatedDate
        //            };
        //        }).ToList();

        //        return Ok(new { success = true, data = resultList });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, message = ex.Message });
        //    }
        //}

        [HttpGet("teacher-history")]
        public async Task<IActionResult> GetTeacherEvaluatedHistory()
        {
            try
            {
                int? teacherId = HttpContext.Session.GetInt32("UserID");
                if (teacherId == null)
                {
                    string? userId = HttpContext.Session.GetString("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var userRecord = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.AspNetUserID == userId);
                        if (userRecord != null) teacherId = userRecord.UserID;
                    }
                }

                if (teacherId == null || teacherId <= 0)
                {
                    return Unauthorized(new { success = false, message = "Teacher not logged in." });
                }

                var teacherDetails = await _context.Set<ViewHfTeacherDetails>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TeacherID == teacherId);

                if (teacherDetails == null || !teacherDetails.InstituteID.HasValue)
                {
                    return NotFound(new { success = false, message = "Teacher profile or institute not found." });
                }

                long instituteId = teacherDetails.InstituteID.Value;
                string schoolName = teacherDetails.InstituteName ?? "Unknown School";

                var assignedStandards = !string.IsNullOrEmpty(teacherDetails.Standard)
                    ? teacherDetails.Standard.Split(',').Select(s => s.Trim()).ToList()
                    : new List<string>();

                var allStudents = await _context.Set<hf_view_studentsdetails>()
                    .AsNoTracking()
                    .Where(s => s.InstituteID == instituteId)
                    .ToListAsync();

                var filteredStudentUserIds = allStudents
                    .Where(s => assignedStandards.Contains(s.Standard) || assignedStandards.Contains(s.Standard + "-" + s.Division))
                    .Select(s => (int)s.StudentId)
                    .Distinct()
                    .ToList();

                // 4. Students ka naam map karne ke liye dictionary
                var studentDict = await _context.Users
                    .AsNoTracking()
                    .Where(u => filteredStudentUserIds.Contains(u.UserID))
                    .ToDictionaryAsync(
                        u => u.UserID,
                        u => $"{u.FirstName ?? ""} {u.LastName ?? ""}".Trim()
                    );

                var evaluations = await _context.ProjectEvaluations
                    .AsNoTracking()
                    .Where(x =>
                        (x.StudentID != null && filteredStudentUserIds.Contains(x.StudentID.Value)) ||
                        (x.InstituteID == instituteId) 
                    )
                    .OrderByDescending(x => x.EvaluatedDate)
                    .ToListAsync();

                // 6. Map to Result List
                var resultList = evaluations.Select(x => {
                    string studentName = "Group / Team Project";
                    if (x.StudentID.HasValue && studentDict.ContainsKey(x.StudentID.Value))
                    {
                        studentName = studentDict[x.StudentID.Value];
                        if (string.IsNullOrEmpty(studentName)) studentName = "Unknown Student";
                    }

                    return new
                    {
                        x.EvaluationID,
                        x.ProjectID,
                        x.ProjectType,
                        x.StudentID,
                        x.InstituteID,
                        StudentName = studentName,
                        SchoolName = schoolName,
                        x.CourseName,
                        x.StatusColor,
                        x.Marks,
                        x.Description,
                        x.EvaluatedByUserId,
                        x.EvaluatedDate
                    };
                }).ToList();

                return Ok(new { success = true, data = resultList });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

    }
}