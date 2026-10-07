using ClosedXML.Excel;
using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.Entities; // Adjust based on your DTO location
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Configuration;
using System.Data;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HemFoundation.Controllers // Changed namespace to Controllers convention
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public ReportController(IConfiguration configuration, ApplicationDbContext context)
        {
            _context = context;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("pbl-report-teacher")]
        public async Task<IActionResult> PBLReportForTeacher()
        {
            // 1. Get Teacher ID from your current session/claims setup

            int? userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null || userId == 0)
            {
                // Fall back to claims if session is missing
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                userId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }

            if (userId == null || userId == 0)
            {
                return Unauthorized(new { success = false, message = "User not logged in." });
            }
            int? teacherId = userId;

            // 2. Execute the PostgreSQL function and fetch results into a list
            var reportData = await _context.PblReportTeacherDtos
                .FromSqlRaw("SELECT * FROM pbl_report_teacher({0})", teacherId)
                .ToListAsync();

            using (XLWorkbook wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("PBL Report");

                // 1. Define your custom column headers manually
                ws.Cell(1, 1).Value = "User ID";
                ws.Cell(1, 2).Value = "First Name";
                ws.Cell(1, 3).Value = "Last Name";
                ws.Cell(1, 4).Value = "Standard";
                ws.Cell(1, 5).Value = "School Name";
                ws.Cell(1, 6).Value = "CourseName";
               ws.Cell(1, 7).Value = "PBL Type";
                ws.Cell(1, 8).Value = "File Name";
                ws.Cell(1, 9).Value = "Extension";
                ws.Cell(1, 10).Value = "File URL";
                ws.Cell(1, 11).Value = "Submission Date"; // Adjust labels to match your DTO properties

                // Style the header row (optional: make it bold & give it a light background)
                var headerRow = ws.Row(1);
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

                // 2. Insert the data starting from row 2 horizontally
                int rowIdx = 2;
                foreach (var item in reportData)
                {
                    ws.Cell(rowIdx, 1).Value = item.StudentID;     // Replace with your actual DTO property names
                    ws.Cell(rowIdx, 2).Value = item.FirstName;
                    ws.Cell(rowIdx, 3).Value = item.LastName;
                    ws.Cell(rowIdx, 4).Value = item.Standard;
                    ws.Cell(rowIdx, 5).Value = item.InstituteName;
                    ws.Cell(rowIdx, 6).Value = item.CourseName;
                    ws.Cell(rowIdx, 7).Value = item.Title;
                    ws.Cell(rowIdx, 8).Value = item.FileName;
                    ws.Cell(rowIdx, 9).Value = item.FileExtension;
                    ws.Cell(rowIdx, 10).Value = item.FileLink;
                    ws.Cell(rowIdx, 11).Value = item.SubmissionDate;

                    rowIdx++;
                }

                // Auto-fit columns for readability
                ws.Columns().AdjustToContents();

                using (MemoryStream stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    var fileName = "PBLReport.xlsx";

                    return File(stream.ToArray(), contentType, fileName);
                }
            }


        }

        [HttpGet("certified-students-report")]
        public async Task<IActionResult> CertifiedStudentsReportForTeacher()
        {
            // 1. Get Teacher ID from current session
            int? userId = HttpContext.Session.GetInt32("UserID");

            if (userId == null || userId == 0)
            {
                // Fall back to claims if session is missing
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                userId = int.TryParse(claimId, out int parsed) ? parsed : 0;
            }

            if (userId == null || userId == 0)
            {
                return Unauthorized(new { success = false, message = "User not logged in." });
            }
            int? teacherId = userId;

            // 2. Execute the PostgreSQL function via EF Core
            var reportData = await _context.CertifiedStudentsDtos
                .FromSqlRaw("SELECT * FROM certified_students_details_from_teacher({0})", teacherId)
                .ToListAsync();

            using (XLWorkbook wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Certified Students");

                // 1. Define your custom column headers manually in Row 1
                ws.Cell(1, 1).Value = "Student ID";
                ws.Cell(1, 2).Value = "Student Name";
                ws.Cell(1, 3).Value = "Standard";
                ws.Cell(1, 4).Value = "School Name";
                ws.Cell(1, 5).Value = "Email";
                ws.Cell(1, 6).Value = "Phone";
                ws.Cell(1, 7).Value = "Course ID";
                ws.Cell(1, 8).Value = "Course Name"; // Adjust headers to match your certified students DTO properties

                // Style the header row
                var headerRow = ws.Row(1);
                headerRow.Style.Font.Bold = true;
                headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

                // 2. Insert data row-by-row horizontally
                int rowIdx = 2;
                foreach (var item in reportData)
                {
                    ws.Cell(rowIdx, 1).Value = item.Id;   // Replace with your actual DTO properties
                    ws.Cell(rowIdx, 2).Value = item.StudentName;
                    ws.Cell(rowIdx, 3).Value = item.Standard;
                    ws.Cell(rowIdx, 4).Value = item.InstituteName;
                    ws.Cell(rowIdx, 5).Value = item.EmailID;
                    ws.Cell(rowIdx, 6).Value = item.MobileNo;
                    ws.Cell(rowIdx, 7).Value = item.CourseID;
                    ws.Cell(rowIdx, 8).Value = item.CourseName;

                    rowIdx++;
                }

                // Auto-fit column widths for clear readability
                ws.Columns().AdjustToContents();

                using (MemoryStream stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    var fileName = "CertifiedStudentsReport.xlsx";

                    return File(stream.ToArray(), contentType, fileName);
                }
            }
        }


        [HttpGet("export-institute-summary")]
        [HttpPost("export-institute-summary")]
        public async Task<IActionResult> ExportInstituteSummaryReport()
        {
            string instituteIDstr = null;
            int instituteID = 0;

            // 1. Try Query String
            if (Request.Query.TryGetValue("instituteIDstr", out var queryVal) && !string.IsNullOrEmpty(queryVal))
            {
                instituteIDstr = queryVal;
            }

            // 2. Try Form Data (application/x-www-form-urlencoded or multipart/form-data)
            if (string.IsNullOrEmpty(instituteIDstr) && Request.HasFormContentType && Request.Form.TryGetValue("instituteIDstr", out var formVal) && !string.IsNullOrEmpty(formVal))
            {
                instituteIDstr = formVal;
            }

            // 3. Try Request Body (JSON payload if sent via POST)
            if (string.IsNullOrEmpty(instituteIDstr) && Request.ContentLength > 0)
            {
                try
                {
                    Request.EnableBuffering();
                    Request.Body.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(Request.Body, leaveOpen: true))
                    {
                        var bodyJson = await reader.ReadToEndAsync();
                        Request.Body.Seek(0, SeekOrigin.Begin); // Reset stream position

                        if (!string.IsNullOrEmpty(bodyJson))
                        {
                            // Parse JSON dynamically using System.Text.Json
                            var jsonDoc = System.Text.Json.JsonDocument.Parse(bodyJson);
                            if (jsonDoc.RootElement.TryGetProperty("instituteIDstr", out var prop) && prop.ValueKind != System.Text.Json.JsonValueKind.Null)
                            {
                                instituteIDstr = prop.ToString();
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore JSON parsing exceptions if body isn't a valid JSON object
                }
            }

            // 4. Parse string to int if found from any of the above sources
            if (!string.IsNullOrEmpty(instituteIDstr) && int.TryParse(instituteIDstr, out int parsedId))
            {
                instituteID = parsedId;
            }

            // 5. Fallback to Session UserID if not provided anywhere else
            if (instituteID <= 0)
            {
                int? sessionUserId = HttpContext.Session.GetInt32("UserID");
                if (sessionUserId.HasValue && sessionUserId.Value > 0)
                {
                    instituteID = sessionUserId.Value;
                }
            }

            if (instituteID <= 0)
            {
                return BadRequest("Invalid Institute ID.");
            }

            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection");
                DataSet essr = new DataSet();
                DataTable pblReportDataTable = new DataTable("PBL Report");
                DataTable groupSubmissionDataTable = new DataTable("Group Submission Report");

                using (var conn = new Npgsql.NpgsqlConnection(connString))
                {
                    conn.Open();

                    string schoolSummaryQuery = @"
                -- Table 0: Student Details
                SELECT 
                    wjs.studentid AS ""StudentID"",
                    u.firstname || ' ' || u.lastname AS ""StudentName"",
                    u.gender AS ""Gender"",
                    wjs.standard AS ""Standard"",
                    wjs.division AS ""Division"",
                    u.mobileno AS ""MobileNo"",
                    u.cityname AS ""CityName"",
                    wji.""Name"" AS ""SchoolName""
                FROM wj_students AS wjs
                LEFT JOIN users AS u ON u.userid = wjs.studentid
                LEFT JOIN wj_studentassociates AS wjsa ON wjsa.studentid = wjs.studentid
                LEFT JOIN wj_institutes AS wji ON wji.instituteid = wjsa.instituteid
                WHERE wji.instituteid = @InstituteID;

                -- Table 1: MCQ Marks
                SELECT wjs.studentid AS ""StudentID"", 
                    concat(u.firstname, ' ', u.lastname) AS ""Student_Name"", 
                    wjs.standard AS ""Standard"", 
                    wjist.instituteid AS ""InstituteID"", 
                    wjist.""Name"" AS ""InstituteName"", 
                    vs.maincoursename AS ""MainCourseName"", 
                    c.coursename AS ""CourseName"", 
                    t.topicname AS ""TopicName"", 
                    '5' AS ""TotalMarks"", 
                    e.marksobtain AS ""MarksObtain"", 
                    e.starttime AS ""Activity_Date""
                FROM hf_view_diffrentexaminationrecords AS e 
                LEFT JOIN wj_students AS wjs ON e.userid = wjs.studentid 
                LEFT JOIN users AS u ON u.userid = e.userid 
                LEFT JOIN wj_studentassociates AS sa ON sa.studentid = u.userid 
                LEFT JOIN wj_institutes AS wjist ON wjist.instituteid = sa.instituteid 
                LEFT JOIN topics AS t ON t.topicid = e.examtopicid 
                LEFT JOIN courses AS c ON c.courseid = t.courseid 
                LEFT JOIN view_wj_syllabus AS vs ON vs.courseid = t.courseid 
                WHERE e.endtime IS NOT NULL AND e.endtime::text <> '0' 
                  AND c.courseallocatedto <> 'Teacher' 
                  AND c.coursename <> 'HEM-Sampark Mega Quiz' 
                  AND t.topicname <> 'Belief Survey MCQ' 
                  AND vs.maincoursename NOT LIKE '%Beliefs%' 
                  AND wjist.instituteid = @InstituteID;

                -- Table 2: Practical Marks
                SELECT DISTINCT wjs.studentid AS ""StudentID"", 
                    concat(u.firstname, ' ', u.lastname) AS ""Student_Name"", 
                    wjs.standard AS ""Standard"", 
                    wjist.instituteid AS ""InstituteID"", 
                    wjist.""Name"" AS ""InstituteName"", 
                    vs.maincoursename AS ""MainCourseName"", 
                    c.coursename AS ""CourseName"", 
                    pa.title AS ""Title"", 
                    pa.coins AS ""TotalMarks"", 
                    CASE 
                        WHEN pa.code = 'G_E_10' THEN '10' 
                        WHEN pa.title NOT IN ('Game','Ken Ken','Futoshiki','Puzzle','Scramble','Wordgame') THEN '5' 
                        WHEN pa.title = 'Game' THEN (SELECT count(DISTINCT game24id) FROM game24solution WHERE game24solution.courseid = pa.courseid AND game24solution.iscreatedby = wjs.studentid AND game24solution.solutionanswers <> 'NA' AND game24solution.game24id <> 0) 
                        WHEN pa.title = 'Ken Ken' THEN (SELECT count(DISTINCT kmid) FROM ken_answers WHERE ken_answers.courseid = pa.courseid AND ken_answers.iscreatedby = wjs.studentid AND ken_answers.kmid NOT IN ('345','346','350','351','352')) 
                        WHEN pa.title = 'Futoshiki' THEN (SELECT count(DISTINCT fid) FROM futoshiki_answer WHERE futoshiki_answer.courseid = pa.courseid AND futoshiki_answer.iscreatedby = wjs.studentid AND futoshiki_answer.fid <> 0) 
                         WHEN pa.title = 'Puzzle' THEN (SELECT COUNT(DISTINCT pztranid)*2 FROM puzzletransaction WHERE puzzletransaction.courseid = pa.courseid AND puzzletransaction.isresolvedby = wjs.studentid AND puzzletransaction.isresolved ='Y')   
                         WHEN pa.title = 'Scramble' THEN (SELECT (COUNT(ansid) * 2)::text FROM wj_scrambleanswer WHERE userid = wjs.studentid AND answer IS NOT NULL AND questionid IN (SELECT questionid FROM wj_scramblequestion WHERE courseid = pa.courseid)) 
                        WHEN pa.title IN ('Wordgame') THEN '2' 
                        ELSE '0' 
                    END AS ""MarksObtain"", 
                    p.createddate::DATE AS ""Activity_Date""
                FROM hf_view_praticalsubmissionrecords AS p 
                LEFT JOIN wj_students AS wjs ON p.studentid = wjs.studentid 
                LEFT JOIN users AS u ON u.userid = p.studentid 
                LEFT JOIN wj_studentassociates AS sa ON sa.studentid = p.studentid 
                LEFT JOIN wj_institutes AS wjist ON wjist.instituteid = sa.instituteid 
                LEFT JOIN wj_practicals AS pa ON pa.practicalid = p.practicalid 
                LEFT JOIN courses AS c ON c.courseid = pa.courseid 
                LEFT JOIN view_wj_syllabus AS vs ON vs.courseid = c.courseid 
                WHERE wjist.instituteid = @InstituteID AND pa.code NOT IN ('G_E_011','G_E_010');
            ";

                    using (var cmd = new Npgsql.NpgsqlCommand(schoolSummaryQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("InstituteID", instituteID);
                        using (var reader = cmd.ExecuteReader())
                        {
                            DataTable dtStudent = new DataTable("StudentDetails");
                            dtStudent.Load(reader);
                            essr.Tables.Add(dtStudent);

                            DataTable dtMcq = new DataTable("MCQ");
                            dtMcq.Load(reader);
                            essr.Tables.Add(dtMcq);

                            DataTable dtPractical = new DataTable("Practical");
                            dtPractical.Load(reader);
                            essr.Tables.Add(dtPractical);
                        }
                    }

                    string pblQuery = @"
                SELECT WJS.StudentID, CONCAT(U.FirstName, ' ', U.LastName) AS ""Student_Name"", WJS.Standard, WJI.""Name"" AS ""InstituteName"", v.MainCourseName, HFA.CourseName, 'Family PBL' AS ""Title"", FileName, FileExtension, 'https://hemvirtues.com/Uploads/Anubhuti/' || CAST(WJI.InstituteID AS VARCHAR(10)) || '/' || CAST(WJS.StudentID AS VARCHAR(10)) || '/' || FileName || FileExtension AS ""FileLink"", '5' AS ""TotalMarks"", '5' AS ""MarksObtain"", IsCreatedDate AS ""SubmissionDate""    
                FROM HF_UploadAnubhuti AS HFA    
                INNER JOIN Users AS U ON U.UserID = HFA.IsCreatedBy    
                INNER JOIN WJ_Students AS WJS ON WJS.StudentID = HFA.IsCreatedBy    
                INNER JOIN WJ_StudentAssociates AS WJSA ON WJSA.StudentID = WJS.StudentID    
                INNER JOIN WJ_Institutes AS WJI ON WJI.InstituteID = WJSA.InstituteID    
                INNER JOIN WJ_Practicals AS p ON p.CourseID = HFA.CourseID    
                INNER JOIN View_WJ_Syllabus AS v ON v.CourseID = p.CourseID    
                WHERE (@InstituteID IS NULL OR WJI.InstituteID = @InstituteID)    
                GROUP BY WJS.StudentID, U.FirstName, U.LastName, WJS.Standard, WJI.""Name"", v.MainCourseName, HFA.CourseName, FileName, FileExtension, IsCreatedDate, WJI.InstituteID    

                UNION ALL    

                SELECT WJS.StudentID, CONCAT(U.FirstName, ' ', U.LastName) AS ""Student_Name"", WJS.Standard, WJI.""Name"" AS ""InstituteName"", v.MainCourseName, HFA.CourseName, 'Individual PBL' AS ""Title"", FileName, FileExtension, 'https://hemvirtues.com/Uploads/Anubhuti/' || CAST(WJI.InstituteID AS VARCHAR(10)) || '/' || CAST(WJS.StudentID AS VARCHAR(10)) || '/' || FileName || FileExtension AS ""FileLink"", '5' AS ""TotalMarks"", '5' AS ""MarksObtain"", IsCreatedDate AS ""SubmissionDate""    
                FROM HF_TBL_UploadIndividualProject AS HFA    
                INNER JOIN Users AS U ON U.UserID = HFA.IsCreatedBy    
                INNER JOIN WJ_Students AS WJS ON WJS.StudentID = HFA.IsCreatedBy    
                INNER JOIN WJ_StudentAssociates AS WJSA ON WJSA.StudentID = WJS.StudentID    
                INNER JOIN WJ_Institutes AS WJI ON WJI.InstituteID = WJSA.InstituteID    
                INNER JOIN WJ_Practicals AS p ON p.CourseID = HFA.MainCourseID    
                INNER JOIN View_WJ_Syllabus AS v ON v.CourseID = p.CourseID    
                WHERE (@InstituteID IS NULL OR WJI.InstituteID = @InstituteID)    
                GROUP BY WJS.StudentID, U.FirstName, U.LastName, WJS.Standard, WJI.""Name"", v.MainCourseName, HFA.CourseName, FileName, FileExtension, IsCreatedDate, WJI.InstituteID    

                UNION ALL    

                SELECT SU.StudentID, CONCAT(U.FirstName, ' ', U.LastName) AS ""Student_Name"", SU.Standard, WJI.""Name"" AS ""InstituteName"", v.MainCourseName, '' AS ""CourseName"", 'Group PBL' AS ""Title"", '' AS ""FileName"", '' AS ""FileExtension"", 'https://www.hemvirtues.com' || REPLACE(SUBSTRING(C.VideoActivity, POSITION('wwwroot\\' IN C.VideoActivity) + 7, LENGTH(C.ImageActivity)), '\\', '/') AS ""FileLink"", '10' AS ""TotalMarks"", '10' AS ""MarksObtain"", C.CreatedDate AS ""SubmissionDate""    
                FROM CreativityActivitesSubmission2021 C    
                LEFT JOIN HF_Rel_Manage_Group_List L ON C.InstituteID = L.IsAllocatedBy AND C.MainCourseID = L.MainCourseID    
                LEFT JOIN Users U ON L.StudentID = U.UserID    
                LEFT JOIN wj_students SU ON L.StudentID = SU.StudentID    
                LEFT JOIN WJ_Institutes WJI ON C.InstituteID = WJI.InstituteID    
                LEFT JOIN MainCourses V ON C.MainCourseID = V.MainCourseID    
                WHERE (@InstituteID IS NULL OR WJI.InstituteID = @InstituteID);
            ";

                    using (var cmd = new Npgsql.NpgsqlCommand(pblQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("InstituteID", instituteID);
                        using (var reader = cmd.ExecuteReader())
                        {
                            pblReportDataTable.Load(reader);
                        }
                    }

                    string casQuery = @"
                SELECT DISTINCT CAS.InstituteID, WJI.institute_name AS ""School Name"", 
                    (SELECT MainCourseName FROM MainCourses WHERE MainCourseID = CAS.MainCourseID) AS ""MainCourseName"", 
                    WJI.firstName || ' ' || WJI.lastName AS ""Co-Ordinator Name"", 
                    WJI.cityName, WJI.referralcode, WJI.mobileno, 
                    'https://hemvirtues.com' || REPLACE(SUBSTRING(CAS.ImageActivity, POSITION('wwwroot\\' IN CAS.ImageActivity) + 7, LENGTH(CAS.ImageActivity)), '\\', '/') AS ""ImageActivity"", 
                    'https://hemvirtues.com' || REPLACE(SUBSTRING(CAS.VideoActivity, POSITION('wwwroot\\' IN CAS.VideoActivity) + 7, LENGTH(CAS.ImageActivity)), '\\', '/') AS ""VideoActivity"", 
                    CAS.ShortDescription, CAS.CreatedDate AS ""Submission Date"", hfrm.GroupName, hfrm.ListStudents, hfrm.MainCourseID, CAS.TeamMembersList, CAS.ActivityType
                FROM CreativityActivitesSubmission2021 AS CAS 
                INNER JOIN vw_school_profile_details AS WJI ON WJI.instituteid = CAS.InstituteID 
                LEFT JOIN ( 
                    SELECT DISTINCT MT.GroupName, sq.IsAllocatedBy, sq.MainCourseID, 
                        STRING_AGG(CAST(sq.StudentID AS varchar), ',') AS ListStudents 
                    FROM HF_Rel_Manage_Group_List sq 
                    INNER JOIN HF_Rel_Manage_Group_List MT ON sq.GroupName = MT.GroupName AND sq.MainCourseID = MT.MainCourseID AND sq.IsAllocatedBy = MT.IsAllocatedBy
                    GROUP BY MT.GroupName, sq.IsAllocatedBy, sq.MainCourseID 
                ) AS hfrm ON hfrm.IsAllocatedBy = CAS.InstituteID AND hfrm.MainCourseID = CAS.MainCourseID 
                WHERE (SELECT MainCourseName FROM MainCourses WHERE MainCourseID = CAS.MainCourseID) IS NOT NULL 
                  AND (CAS.TeamMembersList IS NULL OR CAS.TeamMembersList = '') 
                  AND (CAS.ActivityType IS NULL OR CAS.ActivityType = '') 
                  AND CAS.ImageActivity IS NOT NULL 
                  AND CAS.VideoActivity IS NOT NULL 
                  AND CAS.ShortDescription IS NOT NULL 
                  AND CAS.InstituteID = @InstituteID 
                ORDER BY CAS.CreatedDate DESC;
            ";

                    using (var cmd = new Npgsql.NpgsqlCommand(casQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("InstituteID", instituteID);
                        using (var reader = cmd.ExecuteReader())
                        {
                            groupSubmissionDataTable.Load(reader);
                        }
                    }
                }

                if (essr.Tables.Count > 0) essr.Tables[0].TableName = "StudentDetails";
                if (essr.Tables.Count > 1) essr.Tables[1].TableName = "MCQ";
                if (essr.Tables.Count > 2) essr.Tables[2].TableName = "Practical";
                pblReportDataTable.TableName = "PBL Report";
                groupSubmissionDataTable.TableName = "Group Submission Report";

                using (XLWorkbook wb = new XLWorkbook())
                {
                    // 1. Build and Add Student Summary Table FIRST so it is the first tab
                    DataTable summaryTable = new DataTable();
                    summaryTable.Columns.Add("StudentID");
                    summaryTable.Columns.Add("StudentName");
                    summaryTable.Columns.Add("Standard");
                    summaryTable.Columns.Add("MainCourseName");
                    summaryTable.Columns.Add("Total Marks", typeof(int));
                    summaryTable.Columns.Add("Gain Marks", typeof(int));

                    var allData = Enumerable.Empty<DataRow>();
                    if (essr.Tables.Count > 1) allData = allData.Union(essr.Tables[1].AsEnumerable());
                    if (essr.Tables.Count > 2) allData = allData.Union(essr.Tables[2].AsEnumerable());
                    allData = allData.Union(pblReportDataTable.AsEnumerable());

                    var summaryData = allData
                        .GroupBy(row => new
                        {
                            StudentID = row["StudentID"],
                            StudentName = row["Student_Name"].ToString(),
                            Standard = row["Standard"].ToString(),
                            MainCourseName = row["MainCourseName"].ToString()
                        })
                        .Select(g => new
                        {
                            StudentID = g.Key.StudentID,
                            Student_Name = g.Key.StudentName,
                            Standard = g.Key.Standard,
                            MainCourseName = g.Key.MainCourseName,
                            TotalMarks = 100,
                            GainMarks = g.Sum(row => row.Table.Columns.Contains("MarksObtain")
                                        ? (row["MarksObtain"] != DBNull.Value
                                            ? Convert.ToInt32(row["MarksObtain"])
                                            : 0)
                                        : 0)
                        }).OrderBy(x => x.Student_Name).ToList();

                    foreach (var item in summaryData)
                    {
                        summaryTable.Rows.Add(item.StudentID, item.Student_Name, item.Standard, item.MainCourseName, item.TotalMarks, item.GainMarks);
                    }

                    var summarySheet = wb.Worksheets.Add("Student Summary");
                    summarySheet.Cell(1, 1).InsertTable(summaryTable);

                    // 2. Add remaining reference data worksheets afterwards
                    if (essr.Tables.Count > 0) wb.Worksheets.Add(essr.Tables[0], "StudentDetails");
                    if (essr.Tables.Count > 1) wb.Worksheets.Add(essr.Tables[1], "MCQ");
                    if (essr.Tables.Count > 2) wb.Worksheets.Add(essr.Tables[2], "Practical");
                    wb.Worksheets.Add(pblReportDataTable, "PBL");
                    wb.Worksheets.Add(groupSubmissionDataTable, "Group Submission");

                    // 3. Add individual Main Course dynamic worksheets last
                    var distinctMainCourses = new List<string>();
                    if (essr.Tables.Count > 1)
                        distinctMainCourses.AddRange(essr.Tables[1].AsEnumerable().Select(row => row["MainCourseName"].ToString()));
                    if (essr.Tables.Count > 2)
                        distinctMainCourses.AddRange(essr.Tables[2].AsEnumerable().Select(row => row["MainCourseName"].ToString()));
                    distinctMainCourses.AddRange(pblReportDataTable.AsEnumerable().Select(row => row["MainCourseName"].ToString()));

                    distinctMainCourses = distinctMainCourses.Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();

                    foreach (var mainCourserow in distinctMainCourses)
                    {
                        string mainCourse = mainCourserow.Trim();
                        if (mainCourse.Length > 31)
                            mainCourse = mainCourse.Substring(0, 31);

                        mainCourse = mainCourse.Replace(":", "").Replace("/", "").Replace("\\", "").Replace("?", "").Replace("*", "").Replace("[", "").Replace("]", "");

                        var ws = wb.Worksheets.Add(mainCourse);
                        int currentRow = 1;

                        if (essr.Tables.Count > 1)
                        {
                            DataTable mcqTable = essr.Tables[1].Clone();
                            var mcqRows = essr.Tables[1].AsEnumerable().Where(row => row["MainCourseName"].ToString() == mainCourserow);
                            foreach (var row in mcqRows) mcqTable.ImportRow(row);

                            if (mcqTable.Rows.Count > 0)
                            {
                                ws.Cell(currentRow, 1).Value = $"{mainCourse} - MCQ Data";
                                ws.Cell(currentRow, 1).Style.Font.Bold = true;
                                currentRow++;
                                ws.Cell(currentRow, 1).InsertTable(mcqTable);
                                currentRow += mcqTable.Rows.Count + 3;
                            }
                        }

                        if (essr.Tables.Count > 2)
                        {
                            DataTable practicalTable = essr.Tables[2].Clone();
                            var practicalRows = essr.Tables[2].AsEnumerable().Where(row => row["MainCourseName"].ToString() == mainCourserow);
                            foreach (var row in practicalRows) practicalTable.ImportRow(row);

                            if (practicalTable.Rows.Count > 0)
                            {
                                ws.Cell(currentRow, 1).Value = $"{mainCourse} - Practical Data";
                                ws.Cell(currentRow, 1).Style.Font.Bold = true;
                                currentRow++;
                                ws.Cell(currentRow, 1).InsertTable(practicalTable);
                                currentRow += practicalTable.Rows.Count + 3;
                            }
                        }

                        DataTable pblTable = pblReportDataTable.Clone();
                        var pblRows = pblReportDataTable.AsEnumerable().Where(row => row["MainCourseName"].ToString() == mainCourserow);
                        foreach (var row in pblRows) pblTable.ImportRow(row);

                        if (pblTable.Rows.Count > 0)
                        {
                            ws.Cell(currentRow, 1).Value = $"{mainCourse} - PBL Report";
                            ws.Cell(currentRow, 1).Style.Font.Bold = true;
                            currentRow++;
                            ws.Cell(currentRow, 1).InsertTable(pblTable);
                        }
                    }

                    using (MemoryStream stream = new MemoryStream())
                    {
                        wb.SaveAs(stream);
                        stream.Position = 0;
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "StudentPerformanceReport.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        [HttpGet("export-survey-report")]
        public IActionResult ExportStudentSurveyReports()
        {
            int instituteID = 0;

            // Safely extract session value as an int
            int? sessionUserId = HttpContext.Session.GetInt32("UserID");
            if (sessionUserId.HasValue && sessionUserId.Value > 0)
            {
                instituteID = sessionUserId.Value;
            }
            else
            {
                string? claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(claimId, out int parsedClaim))
                {
                    instituteID = parsedClaim;
                }
            }

            if (instituteID == 0)
            {
                return Unauthorized(new { success = false, message = "User session not found or user not logged in." });
            }

            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection");
                DataSet ds = new DataSet();
                DataTable dt = new DataTable("SurveyReport");

                using (var conn = new Npgsql.NpgsqlConnection(connString))
                {
                    conn.Open();
                    string query = "SELECT * FROM \"ExportSurvey_ReportWithInstitute\"(@InstituteID);";

                    using (var cmd = new Npgsql.NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("InstituteID", instituteID);
                        using (var adapter = new Npgsql.NpgsqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }

                ds.Tables.Add(dt);

                using (XLWorkbook wb = new XLWorkbook())
                {
                    foreach (DataTable table in ds.Tables)
                    {
                        wb.Worksheets.Add(table);
                    }

                    using (MemoryStream stream = new MemoryStream())
                    {
                        wb.SaveAs(stream);
                        stream.Position = 0;
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SurveyReport.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("export-teacher-summary")]
        public async Task<IActionResult> TeacherSummaryReportForInstitute()
        {
            // Retrieve InstituteID from session or claims
            int instituteID = 0;
            if (HttpContext.Session.GetInt32("UserID") is int sessionId && sessionId > 0)
            {
                instituteID = sessionId;
            }
            else if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedClaim))
            {
                instituteID = parsedClaim;
            }

            if (instituteID == 0)
            {
                return Unauthorized(new { success = false, message = "User session not found or user not logged in." });
            }

            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection");
                DataSet essr = new DataSet();

                using (var conn = new Npgsql.NpgsqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // Batched multi-result-set query compatible with PostgreSQL
                    string batchSql = @"
                -- Table 0: TeacherDetails
                SELECT DISTINCT 
                    WJT.TeacherID AS ""TeacherID"", 
                    U.FirstName || ' ' || U.LastName AS ""TeacherName"", 
                    U.Gender AS ""Gender"", 
                    WJT.Standard AS ""Standard"", 
                    WJT.Division AS ""Division"", 
                    U.MobileNo AS ""MobileNo"", 
                    U.CityName AS ""CityName"", 
                    WJI.""Name"" AS ""SchoolName"" 
                FROM WJ_Teacher AS WJT 
                LEFT JOIN Users AS U ON U.UserID = WJT.TeacherID 
                INNER JOIN WJ_TeacherAssociates AS WJTA ON WJTA.TeacherID = WJT.TeacherID 
                INNER JOIN WJ_Institutes AS WJI ON WJI.InstituteID = WJTA.InstituteID 
                WHERE (@InstituteID = 0 OR WJI.InstituteID = @InstituteID);

                -- Table 1: MCQ Marks
                SELECT DISTINCT 
                    E.UserID AS ""UserID"", 
                    Q.""Name"" AS ""Course Name"", 
                    T.TopicName AS ""TopicName"", 
                    T.NoOfQuestion AS ""TotalMarks"", 
                    MAX(E.MarksObtain) AS ""MarksObtain"" 
                FROM Examination AS E 
                LEFT JOIN Users AS U ON U.UserID = E.UserID 
                LEFT JOIN Quiz AS Q ON Q.QuizID = E.QuizID 
                LEFT JOIN Topics AS T ON T.TopicID = E.ExamTopicID 
                LEFT JOIN WJ_TeacherAssociates AS WJTA ON WJTA.TeacherID = U.UserID 
                WHERE E.UserID = WJTA.TeacherID 
                  AND E.MarksObtain IS NOT NULL 
                  AND (Q.""Name"" NOT LIKE '%Week%' AND Q.""Name"" NOT LIKE '%Beliefs Survey%' AND Q.""Name"" NOT LIKE '%HEM-Sampark Mega Quiz%') 
                  AND (@InstituteID = 0 OR WJTA.InstituteID = @InstituteID) 
                GROUP BY E.UserID, Q.""Name"", T.TopicName, T.NoOfQuestion 
                ORDER BY ""Course Name"", ""TopicName"", ""UserID"";
            ";

                    using (var cmd = new Npgsql.NpgsqlCommand(batchSql, conn))
                    {
                        cmd.Parameters.AddWithValue("InstituteID", instituteID);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            // Read Table 0: TeacherDetails
                            DataTable dtTeacher = new DataTable("TeacherDetails");
                            dtTeacher.Load(reader);
                            essr.Tables.Add(dtTeacher);

                            // Read Table 1: MCQ
                            DataTable dtMcq = new DataTable("MCQ");
                            dtMcq.Load(reader);
                            essr.Tables.Add(dtMcq);
                        }
                    }
                }

                using (XLWorkbook wb = new XLWorkbook())
                {
                    if (essr.Tables.Count > 0 && essr.Tables[0].Rows.Count > 0)
                        wb.Worksheets.Add(essr.Tables[0], "TeacherDetails");

                    if (essr.Tables.Count > 1 && essr.Tables[1].Rows.Count > 0)
                        wb.Worksheets.Add(essr.Tables[1], "MCQ");

                    using (MemoryStream stream = new MemoryStream())
                    {
                        wb.SaveAs(stream);
                        stream.Position = 0; // CRITICAL: Reset stream position to prevent 0 KB file downloads
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TeacherPerformanceReport.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        [HttpGet("export-creativity-summary")]
        public async Task<IActionResult> CreativitySummaryReport()
        {
            // Retrieve InstituteID from session or claims safely
            int instituteID = 0;
            if (HttpContext.Session.GetInt32("UserID") is int sessionId && sessionId > 0)
            {
                instituteID = sessionId;
            }
            else if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedClaim))
            {
                instituteID = parsedClaim;
            }

            if (instituteID == 0)
            {
                return Unauthorized(new { success = false, message = "User session not found or user not logged in." });
            }

            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection");
                DataTable dt = new DataTable("CreativitySummary");

                using (var conn = new Npgsql.NpgsqlConnection(connString))
                {
                    await conn.OpenAsync();

                    string casQuery = @"
                SELECT DISTINCT CAS.InstituteID, WJI.institute_name AS ""School Name"", 
                    (SELECT MainCourseName FROM MainCourses WHERE MainCourseID = CAS.MainCourseID) AS ""MainCourseName"", 
                    WJI.firstName || ' ' || WJI.lastName AS ""Co-Ordinator Name"", 
                    WJI.cityName, WJI.referralcode, WJI.mobileno, 
                    'https://hemvirtues.com' || REPLACE(SUBSTRING(CAS.ImageActivity, POSITION('wwwroot\\' IN CAS.ImageActivity) + 7, LENGTH(CAS.ImageActivity)), '\\', '/') AS ""ImageActivity"", 
                    'https://hemvirtues.com' || REPLACE(SUBSTRING(CAS.VideoActivity, POSITION('wwwroot\\' IN CAS.VideoActivity) + 7, LENGTH(CAS.ImageActivity)), '\\', '/') AS ""VideoActivity"", 
                    CAS.ShortDescription, CAS.CreatedDate AS ""Submission Date"", hfrm.GroupName, hfrm.ListStudents, hfrm.MainCourseID, CAS.TeamMembersList, CAS.ActivityType 
                FROM CreativityActivitesSubmission2021 AS CAS 
                INNER JOIN vw_school_profile_details AS WJI ON WJI.instituteid = CAS.InstituteID 
                LEFT JOIN ( 
                    SELECT DISTINCT MT.GroupName, sq.IsAllocatedBy, sq.MainCourseID, 
                        STRING_AGG(CAST(sq.StudentID AS varchar), ',') AS ListStudents 
                    FROM HF_Rel_Manage_Group_List sq 
                    INNER JOIN HF_Rel_Manage_Group_List MT ON sq.GroupName = MT.GroupName AND sq.MainCourseID = MT.MainCourseID AND sq.IsAllocatedBy = MT.IsAllocatedBy 
                    GROUP BY MT.GroupName, sq.IsAllocatedBy, sq.MainCourseID 
                ) AS hfrm ON hfrm.IsAllocatedBy = CAS.InstituteID AND hfrm.MainCourseID = CAS.MainCourseID 
                WHERE (SELECT MainCourseName FROM MainCourses WHERE MainCourseID = CAS.MainCourseID) IS NOT NULL 
                  AND (CAS.TeamMembersList IS NULL OR CAS.TeamMembersList = '') 
                  AND (CAS.ActivityType IS NULL OR CAS.ActivityType = '') 
                  AND CAS.ImageActivity IS NOT NULL 
                  AND CAS.VideoActivity IS NOT NULL 
                  AND CAS.ShortDescription IS NOT NULL 
                  AND CAS.InstituteID = @InstituteID 
                ORDER BY CAS.CreatedDate DESC;
            ";

                    using (var cmd = new Npgsql.NpgsqlCommand(casQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("InstituteID", instituteID);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            dt.Load(reader);
                        }
                    }
                }

                using (XLWorkbook wb = new XLWorkbook())
                {
                    wb.Worksheets.Add(dt, "CreativitySummary");

                    using (MemoryStream stream = new MemoryStream())
                    {
                        wb.SaveAs(stream);
                        stream.Position = 0; // CRITICAL: Reset stream position to prevent 0 KB file downloads

                        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        var fileName = "CreativitySummary.xlsx";

                        return File(stream.ToArray(), contentType, fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        public class InstituteReportRequestModel
        {
            public string instituteIDstr { get; set; }
        }



        [HttpGet("export-institute-Admin-summary")]
        [HttpPost("export-institute-Admin-summary")]
        public IActionResult ExportInstituteSummaryReportForAdmin(string instituteIDstr = null)
        {
            int instituteID = 0;

            if (string.IsNullOrEmpty(instituteIDstr))
            {
                instituteIDstr = Request.Query["instituteIDstr"];
            }

            if (string.IsNullOrEmpty(instituteIDstr) && Request.HasFormContentType)
            {
                instituteIDstr = Request.Form["instituteIDstr"];
            }

            if (!string.IsNullOrEmpty(instituteIDstr) && int.TryParse(instituteIDstr, out int parsedId))
            {
                instituteID = parsedId;
            }
            else if (HttpContext.Session.GetInt32("UserID") is int sessionId && sessionId > 0)
            {
                instituteID = sessionId;
            }
            else if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parsedClaim))
            {
                instituteID = parsedClaim;
            }

            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection");
                DataSet essr = new DataSet();
                DataTable pblReportDataTable = new DataTable("PBL Report");
                DataTable groupSubmissionDataTable = new DataTable("Group Submission Report");

                using (var conn = new Npgsql.NpgsqlConnection(connString))
                {
                    conn.Open();

                    string schoolSummaryQuery = @"
         -- Table 0: Student Details
         SELECT 
             wjs.studentid AS ""StudentID"",
             u.firstname || ' ' || u.lastname AS ""StudentName"",
             u.gender AS ""Gender"",
             wjs.standard AS ""Standard"",
             wjs.division AS ""Division"",
             u.mobileno AS ""MobileNo"",
             u.cityname AS ""CityName"",
             wji.""Name"" AS ""SchoolName""
         FROM wj_students AS wjs
         LEFT JOIN users AS u ON u.userid = wjs.studentid
         LEFT JOIN wj_studentassociates AS wjsa ON wjsa.studentid = wjs.studentid
         LEFT JOIN wj_institutes AS wji ON wji.instituteid = wjsa.instituteid
         ;

         -- Table 1: MCQ Marks
         SELECT wjs.studentid AS ""StudentID"", 
             concat(u.firstname, ' ', u.lastname) AS ""Student_Name"", 
             wjs.standard AS ""Standard"", 
             wjist.instituteid AS ""InstituteID"", 
             wjist.""Name"" AS ""InstituteName"", 
             vs.maincoursename AS ""MainCourseName"", 
             c.coursename AS ""CourseName"", 
             t.topicname AS ""TopicName"", 
             '5' AS ""TotalMarks"", 
             e.marksobtain AS ""MarksObtain"", 
             e.starttime AS ""Activity_Date""
         FROM hf_view_diffrentexaminationrecords AS e 
         LEFT JOIN wj_students AS wjs ON e.userid = wjs.studentid 
         LEFT JOIN users AS u ON u.userid = e.userid 
         LEFT JOIN wj_studentassociates AS sa ON sa.studentid = u.userid 
         LEFT JOIN wj_institutes AS wjist ON wjist.instituteid = sa.instituteid 
         LEFT JOIN topics AS t ON t.topicid = e.examtopicid 
         LEFT JOIN courses AS c ON c.courseid = t.courseid 
         LEFT JOIN view_wj_syllabus AS vs ON vs.courseid = t.courseid 
         WHERE e.endtime IS NOT NULL AND e.endtime::text <> '0' 
           AND c.courseallocatedto <> 'Teacher' 
           AND c.coursename <> 'HEM-Sampark Mega Quiz' 
           AND t.topicname <> 'Belief Survey MCQ' 
           AND vs.maincoursename NOT LIKE '%Beliefs%' 
           ;

         -- Table 2: Practical Marks
         SELECT DISTINCT wjs.studentid AS ""StudentID"", 
             concat(u.firstname, ' ', u.lastname) AS ""Student_Name"", 
             wjs.standard AS ""Standard"", 
             wjist.instituteid AS ""InstituteID"", 
             wjist.""Name"" AS ""InstituteName"", 
             vs.maincoursename AS ""MainCourseName"", 
             c.coursename AS ""CourseName"", 
             pa.title AS ""Title"", 
             pa.coins AS ""TotalMarks"", 
             CASE 
                 WHEN pa.code = 'G_E_10' THEN '10' 
                 WHEN pa.title NOT IN ('Game','Ken Ken','Futoshiki','Puzzle','Scramble','Wordgame') THEN '5' 
                 WHEN pa.title = 'Game' THEN (SELECT count(DISTINCT game24id) FROM game24solution WHERE game24solution.courseid = pa.courseid AND game24solution.iscreatedby = wjs.studentid AND game24solution.solutionanswers <> 'NA' AND game24solution.game24id <> 0) 
                 WHEN pa.title = 'Ken Ken' THEN (SELECT count(DISTINCT kmid) FROM ken_answers WHERE ken_answers.courseid = pa.courseid AND ken_answers.iscreatedby = wjs.studentid AND ken_answers.kmid NOT IN ('345','346','350','351','352')) 
                 WHEN pa.title = 'Futoshiki' THEN (SELECT count(DISTINCT fid) FROM futoshiki_answer WHERE futoshiki_answer.courseid = pa.courseid AND futoshiki_answer.iscreatedby = wjs.studentid AND futoshiki_answer.fid <> 0) 
                 WHEN pa.title = 'Puzzle' THEN (SELECT COUNT(DISTINCT pztranid)*2 FROM puzzletransaction WHERE puzzletransaction.courseid = pa.courseid AND puzzletransaction.isresolvedby = wjs.studentid AND puzzletransaction.isresolved ='Y')   
                 WHEN pa.title = 'Scramble' THEN (SELECT (COUNT(ansid) * 2)::text FROM wj_scrambleanswer WHERE userid = wjs.studentid AND answer IS NOT NULL AND questionid IN (SELECT questionid FROM wj_scramblequestion WHERE courseid = pa.courseid)) 
                 WHEN pa.title IN ('Wordgame') THEN '2' 
                 ELSE '0' 
             END AS ""MarksObtain"", 
             p.createddate::DATE AS ""Activity_Date""
         FROM hf_view_praticalsubmissionrecords AS p 
         LEFT JOIN wj_students AS wjs ON p.studentid = wjs.studentid 
         LEFT JOIN users AS u ON u.userid = p.studentid 
         LEFT JOIN wj_studentassociates AS sa ON sa.studentid = p.studentid 
         LEFT JOIN wj_institutes AS wjist ON wjist.instituteid = sa.instituteid 
         LEFT JOIN wj_practicals AS pa ON pa.practicalid = p.practicalid 
         LEFT JOIN courses AS c ON c.courseid = pa.courseid 
         LEFT JOIN view_wj_syllabus AS vs ON vs.courseid = c.courseid 
         WHERE pa.code NOT IN ('G_E_011','G_E_010');
     ";

                    using (var cmd = new Npgsql.NpgsqlCommand(schoolSummaryQuery, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            DataTable dtStudent = new DataTable("StudentDetails");
                            dtStudent.Load(reader);
                            essr.Tables.Add(dtStudent);

                            DataTable dtMcq = new DataTable("MCQ");
                            dtMcq.Load(reader);
                            essr.Tables.Add(dtMcq);

                            DataTable dtPractical = new DataTable("Practical");
                            dtPractical.Load(reader);
                            essr.Tables.Add(dtPractical);
                        }
                    }

                    string pblQuery = @"
         SELECT WJS.StudentID, CONCAT(U.FirstName, ' ', U.LastName) AS ""Student_Name"", WJS.Standard, WJI.""Name"" AS ""InstituteName"", v.MainCourseName, HFA.CourseName, 'Family PBL' AS ""Title"", FileName, FileExtension, 'https://hemvirtues.com/Uploads/Anubhuti/' || CAST(WJI.InstituteID AS VARCHAR(10)) || '/' || CAST(WJS.StudentID AS VARCHAR(10)) || '/' || FileName || FileExtension AS ""FileLink"", '5' AS ""TotalMarks"", '5' AS ""MarksObtain"", IsCreatedDate AS ""SubmissionDate""    
         FROM HF_UploadAnubhuti AS HFA    
         INNER JOIN Users AS U ON U.UserID = HFA.IsCreatedBy    
         INNER JOIN WJ_Students AS WJS ON WJS.StudentID = HFA.IsCreatedBy    
         INNER JOIN WJ_StudentAssociates AS WJSA ON WJSA.StudentID = WJS.StudentID    
         INNER JOIN WJ_Institutes AS WJI ON WJI.InstituteID = WJSA.InstituteID    
         INNER JOIN WJ_Practicals AS p ON p.CourseID = HFA.CourseID    
         INNER JOIN View_WJ_Syllabus AS v ON v.CourseID = p.CourseID    
   
         GROUP BY WJS.StudentID, U.FirstName, U.LastName, WJS.Standard, WJI.""Name"", v.MainCourseName, HFA.CourseName, FileName, FileExtension, IsCreatedDate, WJI.InstituteID    

         UNION ALL    

         SELECT WJS.StudentID, CONCAT(U.FirstName, ' ', U.LastName) AS ""Student_Name"", WJS.Standard, WJI.""Name"" AS ""InstituteName"", v.MainCourseName, HFA.CourseName, 'Individual PBL' AS ""Title"", FileName, FileExtension, 'https://hemvirtues.com/Uploads/Anubhuti/' || CAST(WJI.InstituteID AS VARCHAR(10)) || '/' || CAST(WJS.StudentID AS VARCHAR(10)) || '/' || FileName || FileExtension AS ""FileLink"", '5' AS ""TotalMarks"", '5' AS ""MarksObtain"", IsCreatedDate AS ""SubmissionDate""    
         FROM HF_TBL_UploadIndividualProject AS HFA    
         INNER JOIN Users AS U ON U.UserID = HFA.IsCreatedBy    
         INNER JOIN WJ_Students AS WJS ON WJS.StudentID = HFA.IsCreatedBy    
         INNER JOIN WJ_StudentAssociates AS WJSA ON WJSA.StudentID = WJS.StudentID    
         INNER JOIN WJ_Institutes AS WJI ON WJI.InstituteID = WJSA.InstituteID    
         INNER JOIN WJ_Practicals AS p ON p.CourseID = HFA.MainCourseID    
         INNER JOIN View_WJ_Syllabus AS v ON v.CourseID = p.CourseID    
         
         GROUP BY WJS.StudentID, U.FirstName, U.LastName, WJS.Standard, WJI.""Name"", v.MainCourseName, HFA.CourseName, FileName, FileExtension, IsCreatedDate, WJI.InstituteID    

         UNION ALL    

         SELECT SU.StudentID, CONCAT(U.FirstName, ' ', U.LastName) AS ""Student_Name"", SU.Standard, WJI.""Name"" AS ""InstituteName"", v.MainCourseName, '' AS ""CourseName"", 'Group PBL' AS ""Title"", '' AS ""FileName"", '' AS ""FileExtension"", 'https://www.hemvirtues.com' || REPLACE(SUBSTRING(C.VideoActivity, POSITION('wwwroot\\' IN C.VideoActivity) + 7, LENGTH(C.ImageActivity)), '\\', '/') AS ""FileLink"", '10' AS ""TotalMarks"", '10' AS ""MarksObtain"", C.CreatedDate AS ""SubmissionDate""    
         FROM CreativityActivitesSubmission2021 C    
         LEFT JOIN HF_Rel_Manage_Group_List L ON C.InstituteID = L.IsAllocatedBy AND C.MainCourseID = L.MainCourseID    
         LEFT JOIN Users U ON L.StudentID = U.UserID    
         LEFT JOIN wj_students SU ON L.StudentID = SU.StudentID    
         LEFT JOIN WJ_Institutes WJI ON C.InstituteID = WJI.InstituteID    
         LEFT JOIN MainCourses V ON C.MainCourseID = V.MainCourseID    
         ;
     ";

                    using (var cmd = new Npgsql.NpgsqlCommand(pblQuery, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            pblReportDataTable.Load(reader);
                        }
                    }

                    string casQuery = @"
         SELECT DISTINCT CAS.InstituteID, WJI.institute_name AS ""School Name"", 
             (SELECT MainCourseName FROM MainCourses WHERE MainCourseID = CAS.MainCourseID) AS ""MainCourseName"", 
             WJI.firstName || ' ' || WJI.lastName AS ""Co-Ordinator Name"", 
             WJI.cityName, WJI.referralcode, WJI.mobileno, 
             'https://hemvirtues.com' || REPLACE(SUBSTRING(CAS.ImageActivity, POSITION('wwwroot\\' IN CAS.ImageActivity) + 7, LENGTH(CAS.ImageActivity)), '\\', '/') AS ""ImageActivity"", 
             'https://hemvirtues.com' || REPLACE(SUBSTRING(CAS.VideoActivity, POSITION('wwwroot\\' IN CAS.VideoActivity) + 7, LENGTH(CAS.ImageActivity)), '\\', '/') AS ""VideoActivity"", 
             CAS.ShortDescription, CAS.CreatedDate AS ""Submission Date"", hfrm.GroupName, hfrm.ListStudents, hfrm.MainCourseID, CAS.TeamMembersList, CAS.ActivityType
         FROM CreativityActivitesSubmission2021 AS CAS 
         INNER JOIN vw_school_profile_details AS WJI ON WJI.instituteid = CAS.InstituteID 
         LEFT JOIN ( 
             SELECT DISTINCT MT.GroupName, sq.IsAllocatedBy, sq.MainCourseID, 
                 STRING_AGG(CAST(sq.StudentID AS varchar), ',') AS ListStudents 
             FROM HF_Rel_Manage_Group_List sq 
             INNER JOIN HF_Rel_Manage_Group_List MT ON sq.GroupName = MT.GroupName AND sq.MainCourseID = MT.MainCourseID AND sq.IsAllocatedBy = MT.IsAllocatedBy
             GROUP BY MT.GroupName, sq.IsAllocatedBy, sq.MainCourseID 
         ) AS hfrm ON hfrm.IsAllocatedBy = CAS.InstituteID AND hfrm.MainCourseID = CAS.MainCourseID 
         WHERE (SELECT MainCourseName FROM MainCourses WHERE MainCourseID = CAS.MainCourseID) IS NOT NULL 
           AND (CAS.TeamMembersList IS NULL OR CAS.TeamMembersList = '') 
           AND (CAS.ActivityType IS NULL OR CAS.ActivityType = '') 
           AND CAS.ImageActivity IS NOT NULL 
           AND CAS.VideoActivity IS NOT NULL 
           AND CAS.ShortDescription IS NOT NULL 
           
         ORDER BY CAS.CreatedDate DESC;
     ";

                    using (var cmd = new Npgsql.NpgsqlCommand(casQuery, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            groupSubmissionDataTable.Load(reader);
                        }
                    }
                }

                if (essr.Tables.Count > 0) essr.Tables[0].TableName = "StudentDetails";
                if (essr.Tables.Count > 1) essr.Tables[1].TableName = "MCQ";
                if (essr.Tables.Count > 2) essr.Tables[2].TableName = "Practical";
                pblReportDataTable.TableName = "PBL Report";
                groupSubmissionDataTable.TableName = "Group Submission Report";

                using (XLWorkbook wb = new XLWorkbook())
                {
                    DataTable summaryTable = new DataTable();
                    summaryTable.Columns.Add("StudentID");
                    summaryTable.Columns.Add("StudentName");
                    summaryTable.Columns.Add("Standard");
                    summaryTable.Columns.Add("MainCourseName");
                    summaryTable.Columns.Add("Total Marks", typeof(int));
                    summaryTable.Columns.Add("Gain Marks", typeof(int));

                    var allData = Enumerable.Empty<DataRow>();
                    if (essr.Tables.Count > 1) allData = allData.Union(essr.Tables[1].AsEnumerable());
                    if (essr.Tables.Count > 2) allData = allData.Union(essr.Tables[2].AsEnumerable());
                    allData = allData.Union(pblReportDataTable.AsEnumerable());

                    var summaryData = allData
                        .GroupBy(row => new
                        {
                            StudentID = row["StudentID"],
                            StudentName = row["Student_Name"].ToString(),
                            Standard = row["Standard"].ToString(),
                            MainCourseName = row["MainCourseName"].ToString()
                        })
                        .Select(g => new
                        {
                            StudentID = g.Key.StudentID,
                            Student_Name = g.Key.StudentName,
                            Standard = g.Key.Standard,
                            MainCourseName = g.Key.MainCourseName,
                            TotalMarks = 100,
                            GainMarks = g.Sum(row => row.Table.Columns.Contains("MarksObtain")
                                        ? (row["MarksObtain"] != DBNull.Value
                                            ? Convert.ToInt32(row["MarksObtain"])
                                            : 0)
                                        : 0)
                        }).OrderBy(x => x.Student_Name).ToList();

                    foreach (var item in summaryData)
                    {
                        summaryTable.Rows.Add(item.StudentID, item.Student_Name, item.Standard, item.MainCourseName, item.TotalMarks, item.GainMarks);
                    }

                    var summarySheet = wb.Worksheets.Add("Student Summary");
                    summarySheet.Cell(1, 1).InsertTable(summaryTable);

                    if (essr.Tables.Count > 0) wb.Worksheets.Add(essr.Tables[0], "StudentDetails");
                    if (essr.Tables.Count > 1) wb.Worksheets.Add(essr.Tables[1], "MCQ");
                    if (essr.Tables.Count > 2) wb.Worksheets.Add(essr.Tables[2], "Practical");
                    wb.Worksheets.Add(pblReportDataTable, "PBL");
                    wb.Worksheets.Add(groupSubmissionDataTable, "Group Submission");

                    var distinctMainCourses = new List<string>();
                    if (essr.Tables.Count > 1)
                        distinctMainCourses.AddRange(essr.Tables[1].AsEnumerable().Select(row => row["MainCourseName"].ToString()));
                    if (essr.Tables.Count > 2)
                        distinctMainCourses.AddRange(essr.Tables[2].AsEnumerable().Select(row => row["MainCourseName"].ToString()));
                    distinctMainCourses.AddRange(pblReportDataTable.AsEnumerable().Select(row => row["MainCourseName"].ToString()));

                    distinctMainCourses = distinctMainCourses.Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();

                    foreach (var mainCourserow in distinctMainCourses)
                    {
                        string mainCourse = mainCourserow.Trim();
                        if (mainCourse.Length > 31)
                            mainCourse = mainCourse.Substring(0, 31);

                        mainCourse = mainCourse.Replace(":", "").Replace("/", "").Replace("\\", "").Replace("?", "").Replace("*", "").Replace("[", "").Replace("]", "");

                        var ws = wb.Worksheets.Add(mainCourse);
                        int currentRow = 1;

                        if (essr.Tables.Count > 1)
                        {
                            DataTable mcqTable = essr.Tables[1].Clone();
                            var mcqRows = essr.Tables[1].AsEnumerable().Where(row => row["MainCourseName"].ToString() == mainCourserow);
                            foreach (var row in mcqRows) mcqTable.ImportRow(row);

                            if (mcqTable.Rows.Count > 0)
                            {
                                ws.Cell(currentRow, 1).Value = $"{mainCourse} - MCQ Data";
                                ws.Cell(currentRow, 1).Style.Font.Bold = true;
                                currentRow++;
                                ws.Cell(currentRow, 1).InsertTable(mcqTable);
                                currentRow += mcqTable.Rows.Count + 3;
                            }
                        }

                        if (essr.Tables.Count > 2)
                        {
                            DataTable practicalTable = essr.Tables[2].Clone();
                            var practicalRows = essr.Tables[2].AsEnumerable().Where(row => row["MainCourseName"].ToString() == mainCourserow);
                            foreach (var row in practicalRows) practicalTable.ImportRow(row);

                            if (practicalTable.Rows.Count > 0)
                            {
                                ws.Cell(currentRow, 1).Value = $"{mainCourse} - Practical Data";
                                ws.Cell(currentRow, 1).Style.Font.Bold = true;
                                currentRow++;
                                ws.Cell(currentRow, 1).InsertTable(practicalTable);
                                currentRow += practicalTable.Rows.Count + 3;
                            }
                        }

                        DataTable pblTable = pblReportDataTable.Clone();
                        var pblRows = pblReportDataTable.AsEnumerable().Where(row => row["MainCourseName"].ToString() == mainCourserow);
                        foreach (var row in pblRows) pblTable.ImportRow(row);

                        if (pblTable.Rows.Count > 0)
                        {
                            ws.Cell(currentRow, 1).Value = $"{mainCourse} - PBL Report";
                            ws.Cell(currentRow, 1).Style.Font.Bold = true;
                            currentRow++;
                            ws.Cell(currentRow, 1).InsertTable(pblTable);
                        }
                    }

                    using (MemoryStream stream = new MemoryStream())
                    {
                        wb.SaveAs(stream);
                        stream.Position = 0;
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "StudentPerformanceReport.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("affiliate-active-students")]
        public FileResult GetAffiliateWiseActiveStudentsdetails(int? AffiliateID, string filter = "A")
        {
           
            int userId = AffiliateID ?? HttpContext.Session.GetInt32("UserID") ?? 0;

            if (userId == 0)
            {
                userId = HttpContext.Session.GetInt32("UserID") ?? 0;
            }
            
            if (string.IsNullOrEmpty(filter))
            {
                filter = "A";
            }

            DataSet essr1 = new DataSet();
            string connString = _configuration.GetConnectionString("DefaultConnection");

            using (var conn = new Npgsql.NpgsqlConnection(connString))
            {
                conn.Open();
                string sql = "SELECT * FROM hf_sp_getuserwiseactivestudentsdetails(@UserID, @FilterParameter)";

                using (var cmd = new Npgsql.NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("UserID", userId);
                    cmd.Parameters.AddWithValue("FilterParameter", filter);

                    using (var adapter = new Npgsql.NpgsqlDataAdapter(cmd))
                    {
                        adapter.Fill(essr1);
                    }
                }
            }

            using (XLWorkbook wb = new XLWorkbook())
            {
                bool dataFound = false;

                if (essr1 != null && essr1.Tables.Count > 0)
                {
                    for (int i = 0; i < essr1.Tables.Count; i++)
                    {
                        DataTable dt = essr1.Tables[i];
                        if (dt.Rows.Count > 0)
                        {
                            wb.Worksheets.Add(dt, "ActiveStudents " + (i + 1));
                            dataFound = true;
                        }
                    }
                }

                if (!dataFound)
                {
                    var ws = wb.Worksheets.Add("No Data Found");
                    ws.Cell("A1").Value = "No active student details found for the selected criteria.";
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    string role = (filter == "A") ? "All" : "Focused";
                    var fileName = $"ActiveStudents_Details_{role}_{DateTime.Today:yyyy-MM-dd}.xlsx";
                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                    return File(stream.ToArray(), contentType, fileName);
                }
            }
        }


        [HttpGet("creativity-combined-report")]
        public FileResult AffiliateSchoolCreativityCombinedReport(Int32? AffiliateID, string filter = "A")
        {
            int userId = AffiliateID ?? HttpContext.Session.GetInt32("UserID") ?? 0;



            if (userId == 0)
            {
                userId = HttpContext.Session.GetInt32("UserID") ?? 0;
            }
            if (string.IsNullOrEmpty(filter))
            {
                filter = "A";
            }

            string connString = _configuration.GetConnectionString("DefaultConnection");
            DataSet essr1 = new DataSet();
            DataSet essr2 = new DataSet();

            using (var conn = new Npgsql.NpgsqlConnection(connString))
            {
                conn.Open();

                // 1. Fetch Standard CAS Report
                using (var cmd1 = new Npgsql.NpgsqlCommand("SELECT * FROM hf_sp_getschoolscasreport(@UserID, @FilterParameter)", conn))
                {
                    cmd1.Parameters.AddWithValue("UserID", userId);
                    cmd1.Parameters.AddWithValue("FilterParameter", filter);
                    using (var adapter = new Npgsql.NpgsqlDataAdapter(cmd1))
                    {
                        adapter.Fill(essr1);
                    }
                }

                // 2. Fetch Higher Education CAS Report
                using (var cmd2 = new Npgsql.NpgsqlCommand("SELECT * FROM hf_sp_getschoolscasreporthe(@UserID, @FilterParameter)", conn))
                {
                    cmd2.Parameters.AddWithValue("UserID", userId);
                    cmd2.Parameters.AddWithValue("FilterParameter", filter);
                    using (var adapter = new Npgsql.NpgsqlDataAdapter(cmd2))
                    {
                        adapter.Fill(essr2);
                    }
                }
            }

            using (XLWorkbook wb = new XLWorkbook())
            {
                bool dataFound = false;

                if (essr1 != null && essr1.Tables.Count > 0)
                {
                    for (int i = 0; i < essr1.Tables.Count; i++)
                    {
                        DataTable dt = essr1.Tables[i];
                        if (dt.Rows.Count > 0)
                        {
                            wb.Worksheets.Add(dt, "CAS Report " + (i + 1));
                            dataFound = true;
                        }
                    }
                }

                if (essr2 != null && essr2.Tables.Count > 0)
                {
                    for (int i = 0; i < essr2.Tables.Count; i++)
                    {
                        DataTable dt = essr2.Tables[i];
                        if (dt.Rows.Count > 0)
                        {
                            wb.Worksheets.Add(dt, "CAS Report HE " + (i + 1));
                            dataFound = true;
                        }
                    }
                }

                if (!dataFound)
                {
                    var ws = wb.Worksheets.Add("No Data Found");
                    ws.Cell("A1").Value = "No creativity report data found for this user and filter.";
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    string filterText = (filter == "F") ? "_Focused" : "_All";
                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    var fileName = $"AffiliateSchoolCreativityCombinedReport{filterText}_For_{userId}.xlsx";

                    return File(stream.ToArray(), contentType, fileName);
                }
            }
        }

        [HttpGet("affiliate-individual-project")]
        public FileResult GetAffiliateWiseIndividualProject(Int32? AffiliateID, string filter = "A")
        {
            int userId = AffiliateID ?? HttpContext.Session.GetInt32("UserID") ?? 0;

            if (userId == 0)
            {
                userId = HttpContext.Session.GetInt32("UserID") ?? 0;
            }
            if (string.IsNullOrEmpty(filter))
            {
                filter = "A";
            }

            string connString = _configuration.GetConnectionString("DefaultConnection");
            DataSet essr1 = new DataSet();

            using (var conn = new Npgsql.NpgsqlConnection(connString))
            {
                conn.Open();
                using (var cmd = new Npgsql.NpgsqlCommand("SELECT * FROM hf_sp_getaffiliatewiseindividualproject(@UserID, @FilterParameter)", conn))
                {
                    cmd.Parameters.AddWithValue("UserID", userId);
                    cmd.Parameters.AddWithValue("FilterParameter", filter);

                    using (var adapter = new Npgsql.NpgsqlDataAdapter(cmd))
                    {
                        adapter.Fill(essr1);
                    }
                }
            }

            using (XLWorkbook wb = new XLWorkbook())
            {
                bool dataFound = false;

                if (essr1 != null && essr1.Tables.Count > 0)
                {
                    for (int i = 0; i < essr1.Tables.Count; i++)
                    {
                        DataTable dt = essr1.Tables[i];
                        if (dt.Rows.Count > 0)
                        {
                            wb.Worksheets.Add(dt, "IndividualProject " + (i + 1));
                            dataFound = true;
                        }
                    }
                }

                if (!dataFound)
                {
                    var ws = wb.Worksheets.Add("No Data Found");
                    ws.Cell("A1").Value = "No individual project data found for the selected criteria.";
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    string filterText = (filter == "F") ? "_Focused" : "_All";
                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    var fileName = $"AffiliateWiseIndividualProject{filterText}_For_{userId}.xlsx";

                    return File(stream.ToArray(), contentType, fileName);
                }
            }
        }

        [HttpGet("affiliate-teacher-individual-project")]
        public FileResult GetAffiliateWiseTeacherIndividualProject(Int32? AffiliateID, string filter = "A")
        {
            int userId = AffiliateID ?? HttpContext.Session.GetInt32("UserID") ?? 0;

            if (userId == 0)
            {
                userId = HttpContext.Session.GetInt32("UserID") ?? 0;
            }
            if (string.IsNullOrEmpty(filter))
            {
                filter = "A";
            }

            string connString = _configuration.GetConnectionString("DefaultConnection");
            DataSet essr1 = new DataSet();

            using (var conn = new Npgsql.NpgsqlConnection(connString))
            {
                conn.Open();
                using (var cmd = new Npgsql.NpgsqlCommand("SELECT * FROM hf_sp_getaffiliatewiseteacherindividualproject(@UserID, @FilterParameter)", conn))
                {
                    cmd.Parameters.AddWithValue("UserID", userId);
                    cmd.Parameters.AddWithValue("FilterParameter", filter);

                    using (var adapter = new Npgsql.NpgsqlDataAdapter(cmd))
                    {
                        adapter.Fill(essr1);
                    }
                }
            }

            using (XLWorkbook wb = new XLWorkbook())
            {
                bool dataFound = false;

                if (essr1 != null && essr1.Tables.Count > 0)
                {
                    for (int i = 0; i < essr1.Tables.Count; i++)
                    {
                        DataTable dt = essr1.Tables[i];
                        if (dt.Rows.Count > 0)
                        {
                            wb.Worksheets.Add(dt, "IndividualProject " + (i + 1));
                            dataFound = true;
                        }
                    }
                }

                if (!dataFound)
                {
                    var ws = wb.Worksheets.Add("No Data Found");
                    ws.Cell("A1").Value = "No individual project data found for the selected criteria.";
                }

                using (MemoryStream stream = new MemoryStream())
                {
                    wb.SaveAs(stream);
                    stream.Seek(0, SeekOrigin.Begin);

                    string filterText = (filter == "F") ? "_Focused" : "_All";
                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    var fileName = $"AffiliateWiseIndividualProject{filterText}_For_{userId}.xlsx";

                    return File(stream.ToArray(), contentType, fileName);
                }
            }
        }


    }
}