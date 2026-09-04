using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Wordprocessing;
using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace HemFoundation.Controllers
{
   
    public class AnubhutiController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly ApplicationDbContext _context;
        private readonly ExamBAL _examBAL;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AnubhutiController(IWebHostEnvironment webHostEnvironment, IConfiguration configuration, ApplicationDbContext context, ExamBAL examBAL)
        {
            _configuration = configuration;
            _context = context;
            _examBAL = examBAL;
            _webHostEnvironment = webHostEnvironment;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet]
        public async Task<IActionResult> ThirdIndex(int courseID, int practID)
        {
            // 1. Resolve UserID safely from claims or session fallback
            int userId = 0;
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdClaim))
            {
                int.TryParse(userIdClaim, out userId);
            }
            if (userId == 0)
            {
                userId = HttpContext.Session.GetInt32("TempStudentID") ?? 1;
            }

            // 2. Language ID resolution from Session
            int languageId = HttpContext.Session.GetInt32("LanguageID") ?? 1;
            ViewBag.LanguageID = languageId;

            // 3. Get Course Name via EF Core
            var courseName = await _context.Courses
                .Where(c => c.CourseID == courseID)
                .Select(c => c.CourseName)
                .FirstOrDefaultAsync();

            ViewBag.CourseName = courseName ?? "Unknown Course";

            // 4. Get Practical Resources Path/Query via EF Core
            string practicalQuery = await _context.WJ_Practicals
                .Where(p => p.PracticalID == practID)
                .Select(p => p.Resources)
                .FirstOrDefaultAsync();

            List<WJ_Practical_ThumbnailView> pracs = new List<WJ_Practical_ThumbnailView>();
            if (!string.IsNullOrEmpty(practicalQuery) && practicalQuery != "NoFile")
            {
                pracs.Add(new WJ_Practical_ThumbnailView { Resources = practicalQuery });
            }
            ViewBag.Pracs = pracs;

            // 5. Fetch Anubhuti Master Data excluding already resolved ones via EF Core LINQ
            var resolvedAnbIds = _context.AnubhutiTransaction
                .Where(t => t.IsResolvedBy == userId)
                .Select(t => t.AnbID);

            var anubhutiList = await _context.AnubhutiMaster
                .Where(a => a.CourseID == courseID && a.AnbType == "Anubhuti2" && !resolvedAnbIds.Contains(a.AnbID))
                .ToListAsync();

            ViewData.Model = anubhutiList.Any() ? anubhutiList : string.Empty;
            ViewBag.CourseID = courseID;
            ViewBag.PracticleID = practID;

            return View();
        }

        [HttpPost]
        
        public async Task<JsonResult> SaveAnubhtiData([FromBody] AnubhutiSubmissionRequest request)
        {
            List<object> list = new List<object>();

            try
            {
                int studentId = 0;
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim)) int.TryParse(userIdClaim, out studentId);
                if (studentId == 0) studentId = HttpContext.Session.GetInt32("TempStudentID") ?? 1;

                // Step 1: Check if submission already exists
                bool alreadySubmitted = await _context.WJ_PracticalSubmission
                    .AnyAsync(s => s.StudentID == studentId && s.PracticalID == request.PracID);

                if (alreadySubmitted)
                {
                    return Json(new { success = false, message = "You have already submitted this practical." });
                }

                // Step 2: Save Anubhuti transaction data items
                if (request.cls_Anubhuti != null)
                {
                    foreach (var item in request.cls_Anubhuti)
                    {
                        var transaction = new AnubhutiTransaction
                        {
                            AnbID = item.AnbID,
                            IsResolvedBy = studentId
                        };

                        if (request.AnuType == "AN1")
                        {
                            transaction.Action1 = item.Action1;
                            transaction.Action2 = item.Action2;
                        }
                        else if (request.AnuType == "AN2")
                        {
                            transaction.Comments1 = item.Comments1;
                        }
                        else if (request.AnuType == "AN3")
                        {
                            transaction.Comments1 = item.Comments1;
                            transaction.Comments2 = item.Comments2;
                            transaction.Comments3 = item.Comments3;
                        }

                        _context.AnubhutiTransaction.Add(transaction);
                        await _context.SaveChangesAsync();
                        list.Add(0);
                    }
                }

                // Step 3 & 4: Add coins and Record Practical Submission
                string coinTag = request.AnuType == "AN1" ? "Task1-(Colour_the_face)" :
                                 request.AnuType == "AN2" ? "Task3-(Sketch_Your_Answers)" :
                                 "Task2-(BE_A_BETTER_FRIEND)";

               

                var submissionEntry = new WJ_PracticalSubmission
                {
                    StudentID = studentId,
                    PracticalID = request.PracID,
                    Status = "Approve",
                    Remarks = "yes",
                    CreatedDate = DateTime.Now
                };

                
                _context.WJ_PracticalSubmission.Add(submissionEntry);
                await _context.SaveChangesAsync();

                return Json(list);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SecondIndex(int courseID, int practID)
        {
            int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 1;
            int languageId = HttpContext.Session.GetInt32("LanguageID") ?? 1;

            try
            {

                ViewBag.LanguageID = languageId;

                var course = await _context.Courses
                    .Where(c => c.CourseID == courseID)
                    .Select(c => c.CourseName)
                    .FirstOrDefaultAsync();

                ViewBag.CourseName = course ?? "Unknown Course";

                string practicalQuery = await _context.WJ_Practicals
                    .Where(p => p.PracticalID == practID)
                    .Select(p => p.Resources)
                    .FirstOrDefaultAsync();

                List<WJ_Practical_ThumbnailView> pracs = new List<WJ_Practical_ThumbnailView>();
                if (!string.IsNullOrEmpty(practicalQuery) && practicalQuery != "NoFile")
                {
                    pracs.Add(new WJ_Practical_ThumbnailView { Resources = practicalQuery });
                }
                ViewBag.Pracs = pracs;

                var resolvedAnbIds = _context.AnubhutiTransaction
                    .Where(t => t.IsResolvedBy == userId)
                    .Select(t => t.AnbID);

                var anubhutiList = await _context.AnubhutiMaster
                    .Where(a => a.CourseID == courseID && a.AnbType == "Anubhuti3" && !resolvedAnbIds.Contains(a.AnbID))
                    .ToListAsync();

                ViewData.Model = anubhutiList;
                ViewBag.CourseID = courseID;
                ViewBag.PracticleID = practID;

            }
            catch(Exception ex)
            { }


            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AnubhutiIndex(int courseID, int practID)
        {
            int userId = HttpContext.Session.GetInt32("TempStudentID") ?? 1;
            int languageId = HttpContext.Session.GetInt32("LanguageID") ?? 1;
            ViewBag.LanguageID = languageId;

            var courseName = await _context.Courses
                .Where(c => c.CourseID == courseID)
                .Select(c => c.CourseName)
                .FirstOrDefaultAsync();

            ViewBag.CourseName = courseName ?? "Unknown Course";

            string practicalQuery = await _context.WJ_Practicals
                .Where(p => p.PracticalID == practID)
                .Select(p => p.Resources)
                .FirstOrDefaultAsync();

            List<WJ_Practical_ThumbnailView> pracs = new List<WJ_Practical_ThumbnailView>();
            if (!string.IsNullOrEmpty(practicalQuery) && practicalQuery != "NoFile")
            {
                pracs.Add(new WJ_Practical_ThumbnailView { Resources = practicalQuery });
            }

            var resolvedAnbIds = _context.AnubhutiTransaction
                .Where(t => t.IsResolvedBy == userId)
                .Select(t => t.AnbID);

            var imageQuestions = await _context.V_HF_Anubhuti_ImageQuestionBind
                .Where(q => q.CourseID == courseID && q.AnbType == "Anubhuti1" && !resolvedAnbIds.Contains(q.AnbID))
                .ToListAsync();

            List<AnubhutiModel> anubhutiList = new List<AnubhutiModel>();

            if (imageQuestions.Any())
            {
                foreach (var item in imageQuestions)
                {
                    string smileImage = item.Smile_Image != null ? $"data:image/jpeg;base64,{Convert.ToBase64String(item.Smile_Image)}" : string.Empty;
                    string sadImage = item.Sad_Image != null ? $"data:image/jpeg;base64,{Convert.ToBase64String(item.Sad_Image)}" : string.Empty;

                    anubhutiList.Add(new AnubhutiModel
                    {
                        AnbID = item.AnbID.ToString(),
                        CourseID = courseID.ToString(),
                        AnbType = item.AnbType,
                        AnbQuestion = item.AnbQuestion,
                        Smile_Image = smileImage,
                        Sad_Image = sadImage
                    });
                }

                ViewBag.Anubhutilist = anubhutiList;
                ViewBag.CourseID = courseID;
                ViewBag.PracticleID = practID;
            }
            else
            {
                ViewBag.CourseID = courseID;
                ViewBag.PracticleID = practID;
                ViewBag.Anubhutilist = null;
                TempData["Error"] = "Upcoming Task..";
            }

            return View(pracs);
        }
        private bool IsExtensionAllowed(string extension)
        {
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".txt", ".pdf" };
            return allowedExtensions.Contains(extension);
        }

        private string GetSafeFileName(string originalFileName)
        {
            return originalFileName.Substring(0, originalFileName.Length - Path.GetExtension(originalFileName).Length).Replace(" ", "_");
        }

        private string GetFilePath(int instituteID, int userID)
        {
            string subPath = Path.Combine("Uploads", "Anubhuti", instituteID.ToString(), userID.ToString());
            return Path.Combine(_webHostEnvironment.WebRootPath, subPath);
        }

        private string GetFilePath(int userID)
        {
            string subPath = Path.Combine("Uploads", "Anubhuti", userID.ToString());
            return Path.Combine(_webHostEnvironment.WebRootPath, subPath);
        }

        private void ConfigureNpgsqlParameterswithpracticalID(NpgsqlCommand cmd, int courseID, byte[] bytes, int userID, string firstName, string lastName, string courseName, string fileName, string filePath, string extension, int practicalID)
        {
            cmd.CommandType = CommandType.Text; // Using text for CALL syntax
            cmd.Parameters.AddWithValue("@p_courseid", courseID);
            cmd.Parameters.AddWithValue("@p_practicalid", practicalID);
            cmd.Parameters.AddWithValue("@p_uploadedfiles", bytes);
            cmd.Parameters.AddWithValue("@p_iscreatedby", userID);
            cmd.Parameters.AddWithValue("@p_filename", fileName + extension);
            cmd.Parameters.AddWithValue("@p_filelink", filePath);
            cmd.Parameters.AddWithValue("@p_fileextension", extension);
            cmd.Parameters.AddWithValue("@p_firstname", firstName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p_lastname", lastName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@p_coursename", courseName);
        }

        private void SetSuccessSweetAlert(int result)
        {
            TempData["UseSweetAlert6SP"] = true;
            TempData["SweetAlertTitle"] = "Success";
            TempData["SweetAlertMessage"] = "Congratulations! Successfully Upload File.";
            TempData["SweetAlertType"] = "success";
        }

        private void SetSuccessSweetAlertSP(int result)
        {
            TempData["UseSweetAlertSP"] = true;
            TempData["SweetAlertTitle"] = "Success";
            TempData["SweetAlertMessage"] = "Congratulations! Successfully Upload File.";
            TempData["SweetAlertType"] = "success";
        }

        [HttpPost]
        public async Task<IActionResult> IndividualCreativity1(IFormFile UploadDoc, string CourseName, int PractID)
        {
            try
            {
                int userID = HttpContext.Session.GetInt32("UserID") ?? 1;
                string courseName = CourseName;

                bool hasAlreadyUploaded = false;
                string firstName = "";
                string lastName = "";
                int instituteID = 0;



                int courseID = 1; // Default fallback

                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // 1. Check if already uploaded
                    using (var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM hf_tbl_uploadindividualproject WHERE iscreatedby = @UserID AND coursename = @CourseName", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserID", userID);
                        cmd.Parameters.AddWithValue("@CourseName", courseName);
                        long count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                        hasAlreadyUploaded = (count > 0);
                    }

                    // 2. Fetch CourseID based on CourseName
                    using (var cmd = new NpgsqlCommand("SELECT courseid FROM courses WHERE coursename = @CourseName LIMIT 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@CourseName", courseName);
                        var courseIdResult = await cmd.ExecuteScalarAsync();
                        if (courseIdResult != null && int.TryParse(courseIdResult.ToString(), out int cId))
                        {
                            courseID = cId;
                        }
                    }

                    // 3. Fetch User First Name & Last Name
                    using (var cmd = new NpgsqlCommand("SELECT firstname, lastname FROM users WHERE userid = @UserID", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserID", userID);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                firstName = reader["firstname"]?.ToString() ?? "";
                                lastName = reader["lastname"]?.ToString() ?? "";
                            }
                        }
                    }

                    // 4. Fetch Institute ID if applicable
                    using (var cmd = new NpgsqlCommand("SELECT instituteid FROM wj_studentassociates WHERE studentid = @StudentID", conn))
                    {
                        cmd.Parameters.AddWithValue("@StudentID", userID);
                        var resultVal = await cmd.ExecuteScalarAsync();
                        if (resultVal != null && int.TryParse(resultVal.ToString(), out int instId))
                        {
                            instituteID = instId;
                        }
                    }
                }
                if (!hasAlreadyUploaded)
                {
                    if (UploadDoc != null && UploadDoc.Length > 0)
                    {
                        string extension = Path.GetExtension(UploadDoc.FileName).ToLower();

                        if (IsExtensionAllowed(extension))
                        {
                            string fileName = GetSafeFileName(UploadDoc.FileName);
                            string path = instituteID != 0 ? GetFilePath(instituteID, userID) : GetFilePath(userID);

                            if (!Directory.Exists(path))
                            {
                                Directory.CreateDirectory(path);
                            }

                            string filePath = Path.Combine(path, fileName + extension);
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await UploadDoc.CopyToAsync(stream);
                            }

                            byte[] bytes = [];
                            //using (var memoryStream = new MemoryStream())
                            //{
                            //    await UploadDoc.CopyToAsync(memoryStream);
                            //    bytes = memoryStream.ToArray();
                            //}

                            using (var conn = new NpgsqlConnection(_connectionString))
                            {
                                await conn.OpenAsync();
                                using (var cmd = new NpgsqlCommand("CALL public.hf_sp_saveindividualuplodedfiles(@p_courseid, @p_uploadedfiles, @p_iscreatedby, @p_filename, @p_filelink, @p_fileextension, @p_firstname, @p_lastname, @p_coursename, @p_practicalid)", conn))
                                {
                                    // Now passing the actual fetched firstName and lastName instead of ""
                                    //ConfigureNpgsqlParameterswithpracticalID(cmd, 1, bytes, userID, firstName, lastName, courseName, fileName, filePath, extension, PractID);
                                    ConfigureNpgsqlParameterswithpracticalID(cmd, courseID, bytes, userID, firstName, lastName, courseName, fileName, filePath, extension, PractID);
                                    int result = await cmd.ExecuteNonQueryAsync();
                                    SetSuccessSweetAlertSP(result);

                                    return RedirectToAction("ShowSweetAlert", "Exam");
                                }
                            }
                        }
                        else
                        {
                            TempData["UseSweetAlert5SP"] = true;
                            TempData["SweetAlertTitle"] = "Alert";
                            TempData["SweetAlertMessage"] = "Only .jpg, .jpeg, .png, .txt, and .pdf extension files are allowed to upload.";
                            TempData["SweetAlertType"] = "Alert";
                            return RedirectToAction("ShowSweetAlert", "Exam");
                        }
                    }
                    else
                    {
                        TempData["UseSweetAlert11SP"] = true;
                        TempData["SweetAlertTitle"] = "Alert";
                        TempData["SweetAlertMessage"] = "Please Select at least one file.";
                        TempData["SweetAlertType"] = "Alert";
                        return RedirectToAction("ShowSweetAlert", "Exam");
                    }
                }
                else
                {
                    TempData["UseSweetAlert6SP"] = true;
                    TempData["SweetAlertTitle"] = "Thanks";
                    TempData["SweetAlertMessage"] = "You have already uploaded";
                    TempData["SweetAlertType"] = "Thanks";
                    return RedirectToAction("ShowSweetAlert", "Exam");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                TempData["UseSweetAlert11SP"] = true;
                TempData["SweetAlertTitle"] = "Alert";
                TempData["SweetAlertMessage"] = ex.Message;
                TempData["SweetAlertType"] = "Alert";
            }
            return RedirectToAction("ShowSweetAlert", "Exam");
        }

        [HttpPost]
        public async Task<IActionResult> Creativity1(IFormFile UploadDoc, string CourseName, int PractID)
        {
            try
            {
                int userID = HttpContext.Session.GetInt32("UserID") ?? 1;
                string courseName = CourseName;

                bool hasAlreadyUploaded = false;
                string firstName = "";
                string lastName = "";
                int instituteID = 0;
                int courseID = 1;
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // 1. Check if already uploaded
                    using (var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM hf_uploadanubhuti WHERE iscreatedby = @UserID AND coursename = @CourseName", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserID", userID);
                        cmd.Parameters.AddWithValue("@CourseName", courseName);
                        long count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                        hasAlreadyUploaded = (count > 0);
                    }

                    // 2. Fetch User First Name & Last Name
                    using (var cmd = new NpgsqlCommand("SELECT firstname, lastname FROM users WHERE userid = @UserID", conn))
                    {
                        cmd.Parameters.AddWithValue("@UserID", userID);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                firstName = reader["firstname"]?.ToString() ?? "";
                                lastName = reader["lastname"]?.ToString() ?? "";
                            }
                        }
                    }
                    using (var cmd = new NpgsqlCommand("SELECT courseid FROM courses WHERE coursename = @CourseName LIMIT 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@CourseName", courseName);
                        var courseIdResult = await cmd.ExecuteScalarAsync();
                        if (courseIdResult != null && int.TryParse(courseIdResult.ToString(), out int cId))
                        {
                            courseID = cId;
                        }
                    }
                    // 3. Fetch Institute ID if applicable
                    using (var cmd = new NpgsqlCommand("SELECT instituteid FROM wj_studentassociates WHERE studentid = @StudentID", conn))
                    {
                        cmd.Parameters.AddWithValue("@StudentID", userID);
                        var resultVal = await cmd.ExecuteScalarAsync();
                        if (resultVal != null && int.TryParse(resultVal.ToString(), out int instId))
                        {
                            instituteID = instId;
                        }
                    }
                }

                if (!hasAlreadyUploaded)
                {
                    if (UploadDoc != null && UploadDoc.Length > 0)
                    {
                        string extension = Path.GetExtension(UploadDoc.FileName).ToLower();

                        if (IsExtensionAllowed(extension))
                        {
                            string fileName = GetSafeFileName(UploadDoc.FileName);
                            string path = instituteID != 0 ? GetFilePath(instituteID, userID) : GetFilePath(userID);

                            if (!Directory.Exists(path))
                            {
                                Directory.CreateDirectory(path);
                            }

                            string filePath = Path.Combine(path, fileName + extension);
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await UploadDoc.CopyToAsync(stream);
                            }

                            byte[] bytes = [];
                            //using (var memoryStream = new MemoryStream())
                            //{
                            //    await UploadDoc.CopyToAsync(memoryStream);
                            //    bytes = memoryStream.ToArray();
                            //}

                            using (var conn = new NpgsqlConnection(_connectionString))
                            {
                                await conn.OpenAsync();
                                using (var cmd = new NpgsqlCommand("CALL public.sp_saveuplodedfiles(@p_courseid, @p_practicalid, @p_uploadedfiles, @p_iscreatedby, @p_filename, @p_filelink, @p_fileextension, @p_firstname, @p_lastname, @p_coursename)", conn))
                                {
                                    // Now passing the actual fetched firstName and lastName instead of ""
                                    ConfigureNpgsqlParameterswithpracticalID(cmd, courseID, bytes, userID, firstName, lastName, courseName, fileName, filePath, extension, PractID);

                                    int result = await cmd.ExecuteNonQueryAsync();
                                    SetSuccessSweetAlert(result);

                                    return RedirectToAction("ShowSweetAlert", "Exam");
                                }
                            }
                        }
                        else
                        {
                            TempData["UseSweetAlert5SP"] = true;
                            TempData["SweetAlertTitle"] = "Alert";
                            TempData["SweetAlertMessage"] = "Only .jpg, .jpeg, .png, .txt, and .pdf extension files are allowed to upload.";
                            TempData["SweetAlertType"] = "Alert";
                            return RedirectToAction("ShowSweetAlert", "Exam");
                        }
                    }
                    else
                    {
                        TempData["UseSweetAlert11SP"] = true;
                        TempData["SweetAlertTitle"] = "Alert";
                        TempData["SweetAlertMessage"] = "Please Select at least one file.";
                        TempData["SweetAlertType"] = "Alert";
                        return RedirectToAction("ShowSweetAlert", "Exam");
                    }
                }
                else
                {
                    TempData["UseSweetAlert6SP"] = true;
                    TempData["SweetAlertTitle"] = "Thanks";
                    TempData["SweetAlertMessage"] = "You have already uploaded";
                    TempData["SweetAlertType"] = "Thanks";
                    return RedirectToAction("ShowSweetAlert", "Exam");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                TempData["UseSweetAlert11SP"] = true;
                TempData["SweetAlertTitle"] = "Alert";
                TempData["SweetAlertMessage"] = ex.Message;
                TempData["SweetAlertType"] = "Alert";
            }
            return RedirectToAction("ShowSweetAlert", "Exam");
        }
    }

    public class AnubhutiSubmissionRequest
    {
        public string AnuType { get; set; }
        public int PracID { get; set; }
        public int CourseID { get; set; }
        public Cls_Anubhuti[] cls_Anubhuti { get; set; }
    }

    public class Cls_Anubhuti
    {
        public int AnbID { get; set; }
        public string Action1 { get; set; }
        public string Action2 { get; set; }
        public string Comments1 { get; set; }
        public string Comments2 { get; set; }
        public string Comments3 { get; set; }
    }
}