using DocumentFormat.OpenXml.InkML;
using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore; // Replaces System.Data.Entity State
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;


namespace HemFoundation.Controllers
{

    
    public class ExamController : Controller
    {
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        // Replace ApplicationDbContext with your actual EF Core context class name
        // private readonly ApplicationDbContext _context;
        private readonly ExamBAL _examBAL;
        public ExamController(IConfiguration configuration, ExamBAL examBAL, ApplicationDbContext context)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
            _examBAL = examBAL;
            _context = context;
        }

        [HttpGet]
        public IActionResult Index(int QuizId, int LevelID, int TopicID, string IsInstituteQuiz, string Iscertificate, int LanguageID, bool IsAttempt, int ExamID, string IsDB)
        {
            int currentLoggedInUserId = HttpContext.Session.GetInt32("TempStudentID") ?? 1;
            ViewBag.UserID = currentLoggedInUserId;

            ExamModel model = new ExamModel
            {
                LanguageID = LanguageID,
                QuizTime = 10,
                QuizId = QuizId,
                LevelID = LevelID,
                TopicID = TopicID,
                IsInstituteQuiz = IsInstituteQuiz,
                Iscertificate = Iscertificate,
                IsAttempts = IsAttempt,
                GetExamID = ExamID
            };

            ViewBag.isTeacherQuiz = false;
            ViewBag.IsExamID = ExamID;
            ViewBag.GoBackURL = (TopicID >= 6256 && TopicID <= 6259) ? 10 : 0;
            ViewBag.IsDB = IsDB;

            return View(model);
        }

       // [Authorize]
        [HttpGet]
        public JsonResult StartExamQuiz(int QuizId, int TopicID, int LevelID, string IsInstituteQuiz, string Iscertificate)
        {
            var result = new JsonResponse<ExamModel>();
            try
            {
                ExamModel model = new ExamModel
                {
                    QuizTime = string.IsNullOrEmpty(Iscertificate) ? 10 : 30,
                    QuizId = QuizId,
                    LevelID = LevelID,
                    IsInstituteQuiz = IsInstituteQuiz,
                    Iscertificate = Iscertificate,
                    QuestionCount = string.IsNullOrEmpty(Iscertificate) ? 10 : 30
                };

                result.Data = model;
                result.Message = "Data Loaded Successfully";
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Data = null;
                result.Message = ex.Message;
                result.Success = false;
            }
            return Json(result);
        }

        [HttpGet]
        public JsonResult GetQuestions(int id, int LevelID, int TopicID, string Iscertificate, int LangId)
        {
            int userID = HttpContext.Session.GetInt32("TempStudentID") ?? 1;
            var result = new JsonResponse<List<QuestionViewModel>>();

            try
            {
                int examId = _examBAL.CreateQuiz(id, TopicID, userID, LevelID, Iscertificate, LangId);
                var questions = _examBAL.GetExamQuestions(TopicID, examId, LangId);

                if (questions != null)
                {
                    questions.ForEach(x => x.QuestionOption?.ForEach(y => y.IsAnswer = false));
                }

                result.Data = questions;
                result.Message = examId.ToString();
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Data = null;
                result.Message = ex.Message;
                result.Success = false;
            }

            return Json(result);
        }

        [HttpPost]
       
        public JsonResult SubmitAnswer_new([FromBody] viewModel model)
        {
            var result = new JsonResponse<object>();
            try
            {
                // Retrieve student ID from session matching your existing pattern
                int userID = HttpContext.Session.GetInt32("TempStudentID") ?? 0;
                if (userID <= 0)
                {
                    // Fallback or handle unauthenticated user session logic if necessary
                    userID = 1; // Or pull from user manager if needed
                }

                var user = _context.Users.Find(userID);
                if (user != null)
                {
                    var Examinationcontent = _context.Examination.Where(x => x.ExaminationID == model.ExamID).FirstOrDefault();

                    if (Examinationcontent != null)
                    {
                        // Check if the exam section/quiz has already been ended/submitted
                        bool hasRecord = _context.Examination.Any(x => x.QuizID == Examinationcontent.QuizID && x.ExamTopicID == Examinationcontent.ExamTopicID && x.EndTime != null && x.UserID == userID);

                        if (hasRecord)
                        {
                            result.Success = false;
                            result.Message = "Answers already submitted for this Section. You cannot submit again.";
                            return Json(result);
                        }
                    }

                    // Retrieve the correct answer flag for the selected option using QuestionOption table
                    var rightAnswer = _context.QuestionOption
                                              .Where(x => x.OptionID == model.selectedOption)
                                              .Select(x => x.IsAnswer)
                                              .FirstOrDefault();

                    // Fetch the specific question record row for this exam
                    var examRecord = _context.ExaminationRecords
                                             .Where(x => x.ExaminationID == model.ExamID && x.QuestionID == model.questionID)
                                             .FirstOrDefault();

                    if (examRecord != null)
                    {
                        examRecord.Answer = model.selectedOption;
                        // Safely assign boolean status
                        examRecord.isCorrect = (rightAnswer == true);
                        examRecord.SubmitTiming = DateTime.Now;
                        examRecord.ModifiedBy = userID;
                        examRecord.ModifiedDate = DateTime.Now;

                        // EF Core state tracking update
                        _context.Entry(examRecord).State = EntityState.Modified;
                        _context.SaveChanges();

                        result.Success = true;
                        result.Message = "Answer submitted successfully.";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Examination record not found for this question.";
                    }
                }
                else
                {
                    result.Success = false;
                    result.Message = "User session or record not found.";
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                result.Success = false;
            }

            return Json(result);
        }
        [HttpGet]
        public JsonResult GetExamQuizResult_new(int ExamID, string IsIQuiz, string Iscertificate, string IsAttempt)
        {
            var result = new JsonResponse<ExamResultModel>();
            ExamResultModel em = new ExamResultModel();

            try
            {
                // Retrieve user ID from session matching your project patterns
                int userID = HttpContext.Session.GetInt32("TempStudentID") ?? 0;
                if (userID <= 0)
                {
                    userID = 1; // Fallback user session handler if needed
                }

                bool IsAttempted = Convert.ToBoolean(IsAttempt);

                if (IsAttempted == false)
                {
                    var Examinationcontent = _context.Examination.Where(x => x.ExaminationID == ExamID).FirstOrDefault();

                    if (Examinationcontent != null)
                    {
                        bool hasRecord = _context.Examination.Any(x => x.QuizID == Examinationcontent.QuizID && x.ExamTopicID == Examinationcontent.ExamTopicID && x.EndTime != null && x.UserID == userID);

                        if (hasRecord)
                        {
                            result.Success = false;
                            result.Message = "Answers already submitted for this Section. You cannot submit again.";
                            return Json(result);
                        }
                    }
                }

                int ID = ExamID;
                var Examination = _context.Examination.Find(ID);

                if (Examination == null)
                {
                    result.Success = false;
                    result.Message = "Examination record not found.";
                    return Json(result);
                }

                var count = _context.ExaminationRecords.Where(x => x.ExaminationID == ID && x.isCorrect == true).Count();
                var incorrectcount = _context.ExaminationRecords.Where(x => x.ExaminationID == ID && x.isCorrect == false).Count();
                var unattemptedcount = _context.ExaminationRecords.Where(x => x.ExaminationID == ID && x.isCorrect == null).Count();

                em.CorrectQuestion = count;
                em.InCorrectQuestion = incorrectcount;
                em.UnattemptedQuestion = unattemptedcount;
                em.ExamID = ID;

                if (IsAttempted == false)
                {
                    Examination.MarksObtain = count;
                    Examination.EndTime = DateTime.Now;
                    Examination.ModifiedDate = DateTime.Now;
                    _context.Entry(Examination).State = EntityState.Modified;
                    _context.SaveChanges();

                    if (Examination.StartTime != null)
                    {
                        TimeSpan diffTime = DateTime.Now - Examination.StartTime.Value;
                        em.ExamTime = string.Format(CultureInfo.CurrentCulture, "{0} Mins {1} Sec", diffTime.Minutes, diffTime.Seconds);
                    }
                    else
                    {
                        em.ExamTime = "0 Mins 0 Sec";
                    }
                }
                else
                {
                    var dateDiffTime = _context.Examination.Where(x => x.ExaminationID == ID).FirstOrDefault();
                    if (dateDiffTime != null && dateDiffTime.EndTime != null && dateDiffTime.StartTime != null)
                    {
                        TimeSpan diff = Convert.ToDateTime(dateDiffTime.EndTime) - Convert.ToDateTime(dateDiffTime.StartTime);
                        em.ExamTime = string.Format(CultureInfo.CurrentCulture, "{0} Mins {1} Sec", diff.Minutes, diff.Seconds);
                    }
                    else
                    {
                        em.ExamTime = "0 Mins 0 Sec";
                    }
                }

                var quizDetails = _context.Quiz.Where(x => x.QuizID == Examination.QuizID).FirstOrDefault();
                var levelDetails = _context.Levels.Where(z => z.LevelID == Examination.LevelID).FirstOrDefault();
                em.QuestionCount = _context.ExaminationRecords.Where(x => x.ExaminationID == ID).Count();

                var topicid = _context.ExaminationRecords.Where(x => x.ExaminationID == ID).FirstOrDefault();
                if (topicid != null)
                {
                    var questionItem = _context.Questions.Find(topicid.QuestionID);
                    if (questionItem != null)
                    {
                        var topicdetails = _context.View_WJ_Course_Topics.Where(x => x.TopicID == (questionItem.TopicID > 0 ? questionItem.TopicID : questionItem.SubTopicID)).FirstOrDefault();
                        if (topicdetails != null)
                        {
                            em.TopicName = topicdetails.TopicName;
                            em.TopicID = topicdetails.TopicID;
                        }
                    }
                }

                // Handle Certification or Standard Quiz configurations
                if (string.IsNullOrEmpty(Iscertificate))
                {
                    var Coinsearned = Convert.ToInt32((Examination.MarksObtain ?? 0) * 1);
                    em.QuizCoins = Coinsearned;
                }
                else
                {
                    int[] CertificationQuizIdList = { 0 };
                    if (CertificationQuizIdList != null && CertificationQuizIdList.Contains(Examination.QuizID))
                    {
                        var rightCount = _context.ExaminationRecords.Where(x => x.ExaminationID == ID && x.isCorrect == true).Count();
                        var wrongCount = _context.ExaminationRecords.Where(x => x.ExaminationID == ID && x.isCorrect == false).Count();
                        int RightMarks = Convert.ToInt32(rightCount * 2);
                        int NegativeMarks = Convert.ToInt32(wrongCount);

                        em.CorrectQuestion = rightCount;
                        em.InCorrectQuestion = wrongCount;
                        em.UnattemptedQuestion = _context.ExaminationRecords.Where(x => x.ExaminationID == ID && x.isCorrect == null).Count();
                    }
                }

                result.Data = em;
                result.Message = "Exam result updated successfully";
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                result.Success = false;
                result.Data = null;
            }

            return Json(result);
        }

        [HttpGet]
       // [Authorize]
        public JsonResult ReviewQuiz(int id)
        {
            var result = new JsonResponse<ExamReviewVM>();
            try
            {
                result.Data = _examBAL.GetUserExamDetials(id);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                result.Success = false;
            }
            return Json(result);
        }


        [AllowAnonymous]
        [HttpGet("Exam/Survey")]
        [HttpGet("Exam/SurveyNew")]
        public IActionResult Survey(int courseID, int topicID, int languageID = 1)
        {
            // 1. Session check for .NET Core
            int? studentId = HttpContext.Session.GetInt32("UserID");
            if (!studentId.HasValue)
            {
                studentId = HttpContext.Session.GetInt32("TempStudentID");
            }

            if (!studentId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            int studentID = studentId.Value;
            ViewData["SurveyData"] = null;

            QuizModel obj = new QuizModel();
            obj.LanguageID = languageID > 0 ? languageID : 1;
            obj.StudentID = studentID;

            string studentStandard = "";
            string courseName = "";

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();

                // Get Student Standard
                using (var cmdStd = new NpgsqlCommand("SELECT standard FROM wj_students WHERE studentid = @StudentID", conn))
                {
                    cmdStd.Parameters.AddWithValue("@StudentID", studentID);
                    var resStd = cmdStd.ExecuteScalar();
                    studentStandard = resStd?.ToString() ?? "";
                }

                // Get Course Name
                using (var cmdCourse = new NpgsqlCommand("SELECT coursename FROM courses WHERE courseid = @CourseID", conn))
                {
                    cmdCourse.Parameters.AddWithValue("@CourseID", courseID);
                    var resCourse = cmdCourse.ExecuteScalar();
                    courseName = resCourse?.ToString() ?? "";
                }
            }

            // 2. Check if survey was already submitted
            object getSurvey = null;
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("SELECT 1 FROM surveyresponse WHERE userid = @UserID AND courseid = @CourseID LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@UserID", studentID);
                    cmd.Parameters.AddWithValue("@CourseID", courseID);
                    getSurvey = cmd.ExecuteScalar();
                }
            }

            if (getSurvey == null)
            {
                // 3. PostgreSQL query to fetch questions and translations
                string query = @"
    SELECT 
        e.id,
        e.courseid, e.topicid, e.subtopicid,
        e.topicname,
        COALESCE(t.question, e.question) AS question,
        COALESCE(t.option1, e.option1) AS option1,
        COALESCE(t.option2, e.option2) AS option2,
        COALESCE(t.option3, e.option3) AS option3,
        COALESCE(t.option4, e.option4) AS option4,
        COALESCE(t.option5, e.option5) AS option5    
    FROM public.hf_surveydata e
    LEFT JOIN public.hf_tbl_surveydatatranslation t 
        ON e.id = t.parentid AND t.languageid = @LanguageID
    WHERE e.courseid::text = @CourseID::text AND e.subtopicid::text = @TopicID::text";

                DataTable dataTable = new DataTable();
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@LanguageID", obj.LanguageID);
                        cmd.Parameters.AddWithValue("@CourseID", courseID);
                        cmd.Parameters.AddWithValue("@TopicID", topicID.ToString());

                        using (var adapter = new NpgsqlDataAdapter(cmd))
                        {
                            adapter.Fill(dataTable);
                        }
                    }
                }

                if (dataTable.Rows.Count > 0)
                {
                    ViewData["SurveyData"] = dataTable.AsEnumerable();
                }
                else
                {
                    ViewData["SurveyData"] = null;
                }

                ViewBag.CourseID = courseID;
                obj.CourseID = courseID;
                obj.CourseName1 = courseName;

                TempData["ID"] = courseID.ToString();
                TempData["TopicID"] = topicID.ToString();
            }
            else
            {
                TempData["UseSweetAlert2SP"] = true;
                TempData["SweetAlertTitle"] = "Success";
                TempData["SweetAlertMessage"] = "You have already submitted the survey.";
                TempData["SweetAlertType"] = "success";
                return RedirectToAction("Showalert", "Exam", new { courseName = courseName });
            }

            return View(obj);
        }
        [AllowAnonymous]
        [HttpPost]
        public IActionResult SaveSurvey(IEnumerable<SurveyResponse> responses)
        {
            // 1. Retrieve session and temp data safely in .NET Core
            int courseId = 0;
            if (TempData["ID"] != null)
            {
                int.TryParse(TempData["ID"].ToString(), out courseId);
                TempData.Keep("ID"); // Keep for downstream redirects if needed
            }

            int topicId = 0;
            if (TempData["TopicID"] != null)
            {
                int.TryParse(TempData["TopicID"].ToString(), out topicId);
                TempData.Keep("TopicID");
            }

            int? userIdNullable = HttpContext.Session.GetInt32("UserID");
            if (!userIdNullable.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }
            int userId = userIdNullable.Value;

            try
            {
                string topicName = "";
                string courseName = "";

                // 2. Fetch TopicName and CourseName via PostgreSQL Npgsql
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    using (var cmdTopic = new NpgsqlCommand("SELECT topicname FROM topics WHERE topicid = @TopicID LIMIT 1", conn))
                    {
                        cmdTopic.Parameters.AddWithValue("@TopicID", topicId);
                        topicName = cmdTopic.ExecuteScalar()?.ToString() ?? "";
                    }

                    using (var cmdCourse = new NpgsqlCommand("SELECT coursename FROM courses WHERE courseid = @CourseID LIMIT 1", conn))
                    {
                        cmdCourse.Parameters.AddWithValue("@CourseID", courseId);
                        courseName = cmdCourse.ExecuteScalar()?.ToString() ?? "";
                    }
                }

                TempData["TopicName"] = topicName;

                if (responses != null && responses.Any())
                {
                    int questionNumber = 1;
                    bool insertSuccessful = true;

                    using (var conn = new NpgsqlConnection(_connectionString))
                    {
                        conn.Open();

                        foreach (var response in responses)
                        {
                            string questionText = response.QuestionText ?? "";
                            string selectedOption = response.SelectedOption ?? "";

                            // 3. PostgreSQL Parameterized Insert Query (Safe from SQL injection)
                            string insertQuery = @"
                                INSERT INTO public.surveyresponse 
                                (courseid, topicid, topicname, questionnumber, questiontext, selectedoption, userid) 
                                VALUES (@CourseID, @TopicID, @TopicName, @QuestionNumber, @QuestionText, @SelectedOption, @UserID)";

                            using (var cmd = new NpgsqlCommand(insertQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@CourseID", courseId);
                                cmd.Parameters.AddWithValue("@TopicID", topicId);
                                cmd.Parameters.AddWithValue("@TopicName", topicName);
                                cmd.Parameters.AddWithValue("@QuestionNumber", questionNumber);
                                cmd.Parameters.AddWithValue("@QuestionText", questionText);
                                cmd.Parameters.AddWithValue("@SelectedOption", selectedOption);
                                cmd.Parameters.AddWithValue("@UserID", userId);

                                int rowsAffected = cmd.ExecuteNonQuery();
                                if (rowsAffected <= 0)
                                {
                                    insertSuccessful = false;
                                }
                            }

                            questionNumber++;
                        }
                    }

                    if (insertSuccessful)
                    {
                        TempData["UseSweetAlert1"] = true;
                        TempData["SweetAlertTitle"] = "Success";
                        TempData["SweetAlertMessage"] = "Congratulations! You have submitted the survey.";
                        TempData["SweetAlertType"] = "success";
                    }
                    else
                    {
                        TempData["Error"] = "Something went wrong.";
                    }

                    return RedirectToAction("Showalert", "Exam", new { courseName = courseName });
                }
            }
            catch (Exception ex)
            {
                // Replace with your .NET Core exception logging handler
                // AddExceptionLogger.Exhandlexception(ex);
            }

            return RedirectToAction("ShowSweetAlert", "Exam");
        }
        [AllowAnonymous]
        public IActionResult ShowSweetAlert()
        {
            // If you need language fallback here as well, you can add it, 
            // otherwise returning the View directly is correct.
            return View();
        }
        [AllowAnonymous]
        public IActionResult Showalert(string courseName)
        {
            int? langId = HttpContext.Session.GetInt32("LanguageID");

            if (langId.HasValue)
            {
                ViewBag.LanguageID = langId.Value;
            }
            else
            {
                // Fallback if session is expired or empty (e.g. Default to English)
                ViewBag.LanguageID = 1;
            }

            ViewBag.CourseName = courseName;
            return View();
        }
    }
}
// Generic Response wrapper for ASP.NET Core JSON endpoints
public class JsonResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T Data { get; set; }
}

public class viewModel
{
    public int questionID { get; set; }
    public int selectedOption { get; set; }
    public int ExamID { get; set; }

}