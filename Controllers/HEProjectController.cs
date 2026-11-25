using HemFoundation.Models;
using HemFoundation.Services; // Ensure your service namespace is included
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NpgsqlTypes;

namespace HemFoundation.Controllers
{
    public class HEProjectController : Controller
    {
        private readonly string _connectionString;
        private readonly IFileStorageService _fileStorage;
        private readonly IWebHostEnvironment _env;

        public HEProjectController(
            IConfiguration configuration,
            IFileStorageService fileStorage,
            IWebHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _fileStorage = fileStorage;
            _env = env;
        }

        // GET: HEProject Index
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Creativity(int CourseID, int PractID)
        {
            int? userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return RedirectToAction("Index", "Home");
            int studentsID = userId.Value;

            ProjectSubmissionDto submission = null;
            List<StudentSelectionDto> availableStudents = new List<StudentSelectionDto>();
            List<InvitationStatusDto> invitedList = new List<InvitationStatusDto>();

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // 1. Get Submission status
                string subSql = @"SELECT 
                    COALESCE(SubmissionID, 0) AS SubmissionID, 
                    COALESCE(ProjectName, '') AS ProjectName, 
                    COALESCE(VideoActivity, '') AS VideoActivity 
                  FROM hf_tbl_heprojectsubmission 
                  WHERE studentsid = @studentsid AND courseid = @courseid";

                using (var cmd = new NpgsqlCommand(subSql, conn))
                {
                    cmd.Parameters.AddWithValue("@studentsid", studentsID);
                    cmd.Parameters.AddWithValue("@courseid", CourseID);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            submission = new ProjectSubmissionDto
                            {
                                SubmissionID = reader.GetInt32(0),
                                ProjectName = reader.GetString(1),
                                VideoActivity = reader.GetString(2)
                            };
                        }
                    }
                }

                bool isProjectCreated = (submission != null && !string.IsNullOrEmpty(submission.ProjectName));
                bool isUploaded = (submission != null && !string.IsNullOrEmpty(submission.VideoActivity));
                bool canUpload = false;

                if (isProjectCreated && !isUploaded)
                {
                    string acceptedCountSql = "SELECT COUNT(1) FROM hf_tbl_hegroupinvitation WHERE submissionid = @subid AND accepted_student_id <> @studentsid";
                    using (var cmd = new NpgsqlCommand(acceptedCountSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@subid", submission.SubmissionID);
                        cmd.Parameters.AddWithValue("@studentsid", studentsID);
                        int acceptedCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        canUpload = (acceptedCount > 0);
                    }
                }

                ViewBag.IsProjectCreated = isProjectCreated;
                ViewBag.IsUploaded = isUploaded;
                ViewBag.CanUpload = canUpload;
                ViewBag.MCID = CourseID;
                ViewBag.PractID = PractID;

                // 2. Fetch Available Students
                string availSql = @"SELECT a.studentid, 
                    COALESCE(CONCAT(u.firstname, ' ', u.lastname), 'Student') AS studentname, 
                    COALESCE(u.emailid, '') AS email 
                  FROM wj_students s
                  LEFT JOIN users u ON s.studentid = u.userid
                  LEFT JOIN wj_studentassociates a ON a.studentid = s.studentid
                  WHERE s.standard = (SELECT standard FROM wj_students WHERE studentid = @studentsid) 
                    AND a.instituteid IN (SELECT instituteid FROM wj_studentassociates WHERE studentid = @studentsid)
                    AND s.studentid <> @studentsid
                    AND s.studentid NOT IN (
                        SELECT accepted_student_id FROM hf_tbl_hegroupinvitation 
                        WHERE submissionid = COALESCE((SELECT submissionid FROM hf_tbl_heprojectsubmission WHERE studentsid = @studentsid AND courseid = @courseid), 0)
                    )";

                using (var cmd = new NpgsqlCommand(availSql, conn))
                {
                    cmd.Parameters.AddWithValue("@studentsid", studentsID);
                    cmd.Parameters.AddWithValue("@courseid", CourseID);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            availableStudents.Add(new StudentSelectionDto
                            {
                                StudentID = reader.GetInt32(0),
                                StudentName = reader.GetString(1),
                                Email = reader.GetString(2)
                            });
                        }
                    }
                }
                ViewBag.AvailableStudents = availableStudents;

                // 3. Fetch Invited List
                if (submission != null)
                {
                    string invitedSql = @"SELECT userid AS studentid, CONCAT(firstname, ' ', lastname) AS studentsname, emailid AS email, TRUE AS isaccepted 
                                          FROM users WHERE userid IN (
                                              SELECT accepted_student_id FROM hf_tbl_hegroupinvitation WHERE submissionid = @subid
                                          )";
                    using (var cmd = new NpgsqlCommand(invitedSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@subid", submission.SubmissionID);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                invitedList.Add(new InvitationStatusDto
                                {
                                    StudentID = reader.GetInt32(0),
                                    StudentsName = reader.GetString(1),
                                    Email = reader.GetString(2),
                                    IsAccepted = reader.GetBoolean(3)
                                });
                            }
                        }
                    }
                }
                ViewBag.InvitedList = invitedList;
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateProjectDetails(int MCID, int PractID, string projectName, string SuggestionComment)
        {
            int? userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return RedirectToAction("Login", "Account");
            int studentsID = userId.Value;

            if (!string.IsNullOrEmpty(projectName) && projectName.Length > 50)
            {
                TempData["Error"] = "Project should not be more then 50 character!!";
                return RedirectToAction("Creativity", new { CourseID = MCID, PractID = PractID });
            }
            await SaveCreativitySubmissionWithoutGroupNewAsync(studentsID, null, null, SuggestionComment, MCID, projectName, false);

            TempData["Success"] = "Project details saved! Now you can invite your colleague.";
            return RedirectToAction("Creativity", new { CourseID = MCID, PractID = PractID });
        }

        [HttpPost]
        public async Task<IActionResult> SendSelectedProjectInvitations(int MCID, int PractID, List<int> SelectedStudentIDs)
        {
            int? userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return RedirectToAction("Login", "Account");
            int studentsID = userId.Value;

            if (SelectedStudentIDs == null || !SelectedStudentIDs.Any())
            {
                TempData["Error"] = "Please select at least one colleague to invite.";
                return RedirectToAction("Creativity", new { CourseID = MCID, PractID = PractID });
            }

            int submissionID = 0;
            string senderName = "A student";
            string projectName = "Higher Standard Project";

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (var cmd = new NpgsqlCommand("SELECT submissionid FROM hf_tbl_heprojectsubmission WHERE studentsid = @sid AND courseid = @cid", conn))
                {
                    cmd.Parameters.AddWithValue("@sid", studentsID);
                    cmd.Parameters.AddWithValue("@cid", MCID);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null) submissionID = Convert.ToInt32(res);
                }

                using (var cmd = new NpgsqlCommand("SELECT CONCAT(firstname, ' ', lastname) FROM users WHERE userid = @sid", conn))
                {
                    cmd.Parameters.AddWithValue("@sid", studentsID);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null) senderName = res.ToString();
                }

                using (var cmd = new NpgsqlCommand("SELECT projectname FROM hf_tbl_heprojectsubmission WHERE submissionid = @subid", conn))
                {
                    cmd.Parameters.AddWithValue("@subid", submissionID);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null) projectName = res.ToString();
                }

                foreach (var targetStudentID in SelectedStudentIDs)
                {
                    int exists = 0;
                    using (var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM hf_tbl_hegroupinvitation WHERE submissionid = @subid AND accepted_student_id = @tsid", conn))
                    {
                        cmd.Parameters.AddWithValue("@subid", submissionID);
                        cmd.Parameters.AddWithValue("@tsid", targetStudentID);
                        exists = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    if (exists == 0)
                    {
                        string targetEmail = "";
                        using (var cmd = new NpgsqlCommand("SELECT emailid FROM users WHERE userid = @tsid", conn))
                        {
                            cmd.Parameters.AddWithValue("@tsid", targetStudentID);
                            var res = await cmd.ExecuteScalarAsync();
                            if (res != null) targetEmail = res.ToString();
                        }

                        if (!string.IsNullOrEmpty(targetEmail))
                        {
                            try
                            {
                                string acceptUrl = Url.Action("AcceptProjectInvitation", "HEProject", new { subID = submissionID, SID = targetStudentID }, Request.Scheme);

                                string emailBody = $@"
                                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; background-color: #f9f9f9;'>
                                    <div style='background-color: #0d6efd; color: white; padding: 15px; text-align: center; border-radius: 6px 6px 0 0;'>
                                        <h2 style='margin: 0;'>Project Collaboration Invitation</h2>
                                    </div>
                                    <div style='padding: 20px; background-color: white;'>
                                        <p style='font-size: 16px; color: #333;'>Hello,</p>
                                        <p style='font-size: 15px; color: #555;'><strong>{senderName}</strong> has invited you to collaborate on their Higher Standard Project: <strong>'{projectName}'</strong>.</p>
                                        <div style='text-align: center; margin: 30px 0;'>
                                            <a href='{acceptUrl}' style='background-color: #198754; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; font-weight: bold; font-size: 16px;'>Accept Invitation</a>
                                        </div>
                                    </div>
                                </div>";

                                 EmailManager.SendEmailAws(targetEmail, "Project Collaboration Invitation", emailBody, null, "", "");
                            }
                            catch (Exception) { }
                        }
                    }
                }
            }

            TempData["Success"] = "Invitations sent successfully!";
            return RedirectToAction("Creativity", new { CourseID = MCID, PractID = PractID });
        }

        [HttpPost("final-project-upload")]
        [RequestSizeLimit(110L * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 110L * 1024 * 1024)]
        public async Task<IActionResult> FinalizeProjectUpload(
            [FromForm] int MCID,
            [FromForm] int PractID,
            [FromForm] IFormFile? UploadImageDoc,
            [FromForm] IFormFile? VideoFile,
            [FromForm] string? VideoDriveFileId,
            [FromForm] string? VideoCloudUrl)
        {
            int? userId = HttpContext.Session.GetInt32("UserID");
            if (userId == null) return Unauthorized(new { success = false, message = "Session expired. Please log in." });
            int studentsID = userId.Value;

            const long MaxVideoSize = 100L * 1024 * 1024;

            VideoDriveFileId = VideoDriveFileId?.Trim();
            VideoCloudUrl = VideoCloudUrl?.Trim();

            bool hasDriveId = !string.IsNullOrWhiteSpace(VideoDriveFileId);
            bool hasCloudUrl = !string.IsNullOrWhiteSpace(VideoCloudUrl);
            bool hasVideoFile = VideoFile != null && VideoFile.Length > 0;

            int videoMethods = (hasDriveId ? 1 : 0) + (hasCloudUrl ? 1 : 0) + (hasVideoFile ? 1 : 0);
           
            if (videoMethods == 0)
            {
                return BadRequest(new { success = false, message = "Video submission is mandatory. Upload a video, select one from Google Drive, or provide a cloud link." });
            }

            if (videoMethods > 1)
            {
                return BadRequest(new { success = false, message = "Choose only one video option: upload a video, select from Google Drive, or enter a cloud link." });
            }

            string folderPath = $"Uploads/HEProject/Course_{MCID}/Student_{studentsID}";
            string finalVideoActivityPath = null;

            if (hasDriveId)
            {
                if (!Regex.IsMatch(VideoDriveFileId!, @"^[a-zA-Z0-9_-]+$"))
                {
                    return BadRequest(new { success = false, message = "Invalid Google Drive file selection." });
                }
                finalVideoActivityPath = $"https://drive.google.com/file/d/{VideoDriveFileId}/view";
            }
            else if (hasCloudUrl)
            {
                if (!Uri.TryCreate(VideoCloudUrl, UriKind.Absolute, out Uri? cloudUri) ||
                    (cloudUri.Scheme != Uri.UriSchemeHttps && cloudUri.Scheme != Uri.UriSchemeHttp))
                {
                    return BadRequest(new { success = false, message = "Please enter a valid HTTP or HTTPS video link." });
                }
                finalVideoActivityPath = cloudUri.AbsoluteUri;
            }
            else if (hasVideoFile)
            {
                if (VideoFile!.Length > MaxVideoSize)
                {
                    return BadRequest(new { success = false, message = "Video size cannot exceed 100 MB." });
                }

                // Utilize your InHouseFileStorageService upload method
                var uploadResult = await _fileStorage.UploadAsync(VideoFile, folderPath);
                if (uploadResult == null || !uploadResult.success || string.IsNullOrWhiteSpace(uploadResult.filePath))
                {
                    return StatusCode(500, new { success = false, message = uploadResult?.message ?? "Video upload failed." });
                }

                finalVideoActivityPath = uploadResult.filePath;
            }

            await SaveCreativitySubmissionWithoutGroupNewAsync(studentsID, null, finalVideoActivityPath, null, MCID, null, true);

            return Ok(new { success = true, message = "Project successfully finalized and submitted!" });
        }

        [HttpGet]
        public async Task<IActionResult> AcceptProjectInvitation(int subID, int SID)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                int exists = 0;
                using (var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM hf_tbl_hegroupinvitation WHERE submissionid = @subid AND accepted_student_id = @sid", conn))
                {
                    cmd.Parameters.AddWithValue("@subid", subID);
                    cmd.Parameters.AddWithValue("@sid", SID);
                    exists = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                int invitedBy = 0;
                int courseID = 0;

                if (exists == 0)
                {
                    using (var cmd = new NpgsqlCommand("SELECT studentsid FROM hf_tbl_heprojectsubmission WHERE submissionid = @subid", conn))
                    {
                        cmd.Parameters.AddWithValue("@subid", subID);
                        invitedBy = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    using (var cmd = new NpgsqlCommand("INSERT INTO hf_tbl_hegroupinvitation (submissionid, accepted_student_id, invited_by) VALUES (@subid, @sid, @invby)", conn))
                    {
                        cmd.Parameters.AddWithValue("@subid", subID);
                        cmd.Parameters.AddWithValue("@sid", SID);
                        cmd.Parameters.AddWithValue("@invby", invitedBy);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                using (var cmd = new NpgsqlCommand("SELECT courseid FROM hf_tbl_heprojectsubmission WHERE submissionid = @subid", conn))
                {
                    cmd.Parameters.AddWithValue("@subid", subID);
                    courseID = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                int practicalID = 0;
                using (var cmd = new NpgsqlCommand("SELECT practicalid FROM wj_practicals WHERE code = 'G_E_10' AND courseid = @cid LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@cid", courseID);
                    var res = await cmd.ExecuteScalarAsync();
                    if (res != null) practicalID = Convert.ToInt32(res);
                }

                if (practicalID > 0)
                {
                    int practicalSubExists = 0;
                    using (var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM wj_practicalsubmission WHERE studentid = @sid AND practicalid = @pid", conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", SID);
                        cmd.Parameters.AddWithValue("@pid", practicalID);
                        practicalSubExists = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }

                    if (practicalSubExists == 0)
                    {
                        using (var cmd = new NpgsqlCommand("INSERT INTO wj_practicalsubmission (studentid, practicalid, status, createddate) VALUES (@sid, @pid, 1, NOW())", conn))
                        {
                            cmd.Parameters.AddWithValue("@sid", SID);
                            cmd.Parameters.AddWithValue("@pid", practicalID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                TempData["Success"] = "You have successfully accepted the project invitation!";
                return RedirectToAction("MessageWithoutLogin", "HEProject", new { MCID = courseID });
            }
        }

        public IActionResult MessageWithoutLogin(int MCID)
        {
            ViewBag.MCID = MCID;
            ViewBag.returnpath = "/Learn/index";
            return View();
        }


       // Make sure this is included at the top of your controller

    private async Task SaveCreativitySubmissionWithoutGroupNewAsync(int studentsID, string teamMembersList, string videoActivity, string shortDesc, int mcid, string projectName, bool isFinalSubmit)
    {
        using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            using (var cmd = new NpgsqlCommand("CALL public.hf_sp_heprojectsubmissiondatanowithoutgroupprojectnew(@_studentsid, @_teammemberslist, @_videoactivity, @_shortdesc, @_mcid, @_projectname, @_isfinalsubmit)", conn))
            {
                // Explicitly map parameter types so Npgsql never infers 'unknown'
                cmd.Parameters.Add(new NpgsqlParameter("@_studentsid", NpgsqlDbType.Integer) { Value = studentsID });

                cmd.Parameters.Add(new NpgsqlParameter("@_teammemberslist", NpgsqlDbType.Text)
                {
                    Value = (object)teamMembersList ?? DBNull.Value
                });

                cmd.Parameters.Add(new NpgsqlParameter("@_videoactivity", NpgsqlDbType.Text)
                {
                    Value = (object)videoActivity ?? DBNull.Value
                });

                cmd.Parameters.Add(new NpgsqlParameter("@_shortdesc", NpgsqlDbType.Text)
                {
                    Value = (object)shortDesc ?? DBNull.Value
                });

                cmd.Parameters.Add(new NpgsqlParameter("@_mcid", NpgsqlDbType.Integer) { Value = mcid });

                cmd.Parameters.Add(new NpgsqlParameter("@_projectname", NpgsqlDbType.Text)
                {
                    Value = (object)projectName ?? DBNull.Value
                });

                cmd.Parameters.Add(new NpgsqlParameter("@_isfinalsubmit", NpgsqlDbType.Integer)
                {
                    Value = isFinalSubmit ? 1 : 0
                });

                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

}
}