using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using HemFoundation.Models.Entities;

namespace HemFoundation.Models
{
    public class ExamBAL
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ExamBAL(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public int CreateQuiz(int QuizID, int TopicID, int UserID, int LevelID, string Iscertificate, int LangId)
        {
            int parsedUserId = Convert.ToInt32(UserID);
            var user = _context.Users.Find(parsedUserId);
            int examId = 0;

            var quiz = _context.Quiz.Find(QuizID);
            var levels = _context.Levels.Find(LevelID);

            if (quiz != null)
            {
                var courseId = _context.QuizCourseLevels.Where(x => x.QuizID == QuizID).Select(x => x.CourseID).ToList();

                Examination exam = new Examination
                {
                    QuizID = QuizID,
                    UserID = UserID,
                    LevelID = LevelID,
                    ExamTopicID = TopicID,
                    StartTime = DateTime.Now,
                    CreatedBy = UserID,
                    CreatedDate = DateTime.Now
                };

                _context.Examination.Add(exam);
                _context.SaveChanges();
                examId = exam.ExaminationID;

                // Single unified condition based only on Language
                List<int> questionIds;
                if (LangId == 0 || LangId == 1)
                {
                    questionIds = _context.Questions.Where(x => courseId.Contains(x.CourseID ?? 0) && x.SubTopicID == TopicID && x.languageID < 2).Select(x => x.QuestionID).ToList();
                }
                else
                {
                    questionIds = _context.Questions.Where(x => courseId.Contains(x.CourseID ?? 0) && x.SubTopicID == TopicID && x.languageID == LangId).Select(x => x.QuestionID).ToList();
                }

                // Randomize and take up to the total marks required by the quiz
                var questIDs = questionIds.OrderBy(x => Guid.NewGuid()).ToList().Take(quiz.TotalMarks ?? 0);

                int counter = 0;
                foreach (var questionIdList in questIDs)
                {
                    ExaminationRecords examRecord = new ExaminationRecords
                    {
                        ExaminationID = examId,
                        QuestionID = questionIdList,
                        CreatedDate = DateTime.Now,
                        CreatedBy = UserID,
                        OrderNo = counter++
                    };
                    _context.ExaminationRecords.Add(examRecord);
                }
                _context.SaveChanges();
            }
            return examId;
        }

        public List<QuestionViewModel> GetExamQuestions(int TopicID, int ExamID, int LangId)
        {
            List<QuestionViewModel> questions = new List<QuestionViewModel>();

            if (ExamID != 0)
            {
                var questIDs = _context.ExaminationRecords.Where(x => x.ExaminationID == ExamID).Select(x => x.QuestionID).ToList();
                List<Questions> questionList;

                if (LangId == 0 || LangId == 1)
                {
                    var query = (from q in _context.Questions
                                 join t in _context.Topics on q.SubTopicID equals t.TopicID
                                 select new { q.QuestionID, t.NoOfQuestion, q.languageID })
                                .Where(x => questIDs.Contains(x.QuestionID) && x.languageID < 2).ToList();

                    var noofquestion = query.Select(x => x.NoOfQuestion).FirstOrDefault();

                    // ADD .Include(q => q.QuestionOption) HERE:
                    questionList = _context.Questions
                                           .Include(q => q.QuestionOption)
                                           .Where(x => questIDs.Contains(x.QuestionID) && x.languageID < 2)
                                           .Take(noofquestion == null ? 10 : Convert.ToInt32(noofquestion))
                                           .ToList();
                }
                else
                {
                    var query = (from q in _context.Questions
                                 join t in _context.Topics on q.SubTopicID equals t.TopicID
                                 select new { q.QuestionID, t.NoOfQuestion, q.languageID })
                                .Where(x => questIDs.Contains(x.QuestionID) && x.languageID == LangId).ToList();

                    var noofquestion = query.Select(x => x.NoOfQuestion).FirstOrDefault();

                    // AND ADD .Include(q => q.QuestionOption) HERE AS WELL:
                    questionList = _context.Questions
                                           .Include(q => q.QuestionOption)
                                           .Where(x => questIDs.Contains(x.QuestionID) && x.languageID == LangId)
                                           .Take(noofquestion == null ? 10 : Convert.ToInt32(noofquestion))
                                           .ToList();
                }

                foreach (var item in questionList)
                {
                    QuestionViewModel qvm = new QuestionViewModel
                    {
                        QuestionID = item.QuestionID,
                        QuestionName = item.QuestionName,
                        Marks = item.Marks,
                        Type = item.Type,
                        ComplexityLevel = item.ComplexityLevel,
                        AnswerDescription = item.AnswerDescription,
                        CourseID = item.CourseID,
                        TopicID = item.TopicID,
                        QuestionOption = new List<QuestionOptionViewModel>()
                    };

                    if (item.QuestionOption != null)
                    {
                        foreach (var option in item.QuestionOption)
                        {
                            if( option.Options!=null)
                            {
                                QuestionOptionViewModel qpvm = new QuestionOptionViewModel
                                {
                                    OptionID = option.OptionID,
                                    Options = option.Options,
                                    IsAnswer = Convert.ToBoolean(option.IsAnswer)
                                };
                                qvm.QuestionOption.Add(qpvm);
                            }
                           
                        }
                    }

                    questions.Add(qvm);
                }
            }
            return questions;
        }

        public ExamResultViewModel QuizAnalytics(int ExamID, List<string> Roles)
        {
            ExamResultViewModel examResult = new ExamResultViewModel();
            var examination = _context.Examination.FirstOrDefault(x => x.ExaminationID == ExamID);
            if (examination == null) return examResult;

            var quizMarksDetails = _context.Quiz.Find(examination.QuizID);
            if (quizMarksDetails == null) return examResult;

            if (examination.MarksObtain >= quizMarksDetails.PassMarks && examination.MarksObtain != null)
            {
                examResult.PassStatus = "Pass";
                if (Roles != null && Roles.Contains("Trainer"))
                {
                    AllocateCourse(ExamID);
                }
            }
            else
            {
                examResult.PassStatus = "Fail";
            }

            examResult.MarksObtain = examination.MarksObtain ?? 0;
            examResult.TotalMarks = Convert.ToInt32(quizMarksDetails.TotalMarks);
            examResult.QuizName = quizMarksDetails.Name;
            examResult.QuizID = examination.QuizID;
            examResult.LevelID = examination.LevelID ?? 0;

            var examResultList = _context.View_ExamResult.Where(x => x.ExaminationID == ExamID).ToList();

            foreach (var examResults in examResultList)
            {
                examResult.examList.Add(new ExamViewModel
                {
                    QuestionName = examResults.Question,
                    RightAnswer = examResults.CorrectAnswer,
                    YourAnswer = examResults.YourAnswer
                });
            }

            return examResult;
        }

        public void AllocateCourse(int examId)
        {
            // Implementation pending
        }

        public List<ResultCourseDetailsVM> CoursePercentageAnalyse(int ExamID)
        {
            var query = (from er in _context.ExaminationRecords
                         join q in _context.Questions on er.QuestionID equals q.QuestionID
                         join c in _context.Courses on q.CourseID equals c.CourseID
                         where er.ExaminationID == ExamID
                         select new { c.CourseName, er.isCorrect });

            var groupQuery = query.GroupBy(x => x.CourseName)
                                  .Select(x => new ResultCourseDetailsVM
                                  {
                                      CourseName = x.Key,
                                      CourseObtainMarks = x.Sum(ans => (ans.isCorrect == true ? 1 : 0)),
                                      CourseTotalCount = x.Count()
                                  }).ToList();

            return groupQuery;
        }

        public ExamReviewVM GetUserExamDetials(int ExamId)
        {
            ExamReviewVM examReviewVM = new ExamReviewVM();
            var exam = _context.Examination.Find(ExamId);

            if (exam != null)
            {
                var examRecord = _context.ExaminationRecords.Where(x => x.ExaminationID == ExamId).OrderBy(x => x.QuestionID).ToList();
                var questionIds = examRecord.Select(x => x.QuestionID).ToList();

                var entryPoint = (from ep in _context.Questions.Include(q => q.QuestionOption) // <-- Add Include here
                                  join e in _context.ExaminationRecords.Where(x => questionIds.Contains(x.QuestionID)) on ep.QuestionID equals e.QuestionID
                                  where e.ExaminationID == ExamId
                                  orderby e.QuestionID ascending
                                  select ep).ToList();

                foreach (var item in examRecord)
                {
                    examReviewVM.ExaminationRecords.Add(new ExaminationRecordVM
                    {
                        RecordID = item.RecordID,
                        ExaminationID = item.ExaminationID,
                        QuestionID = item.QuestionID,
                        RecordStatus = item.RecordStatus,
                        Answer = item.Answer,
                        isCorrect = item.isCorrect,
                        SubmitTiming = item.SubmitTiming,
                        OrderNo = item.OrderNo
                    });
                }

                foreach (var data in examRecord)
                {
                    foreach (var item in entryPoint)
                    {
                        if (data.QuestionID == item.QuestionID)
                        {
                            QuestionViewModel qvm = new QuestionViewModel
                            {
                                QuestionID = item.QuestionID,
                                QuestionName = item.QuestionName,
                                Marks = item.Marks,
                                Type = item.Type,
                                ComplexityLevel = item.ComplexityLevel,
                                AnswerDescription = item.AnswerDescription,
                                CourseID = item.CourseID,
                                TopicID = item.TopicID,
                                QuestionOption = new List<QuestionOptionViewModel>()
                            };

                            if (item.QuestionOption != null)
                            {
                                foreach (var option in item.QuestionOption)
                                {
                                    if (option.Options != null)
                                    {
                                        QuestionOptionViewModel qpvm = new QuestionOptionViewModel
                                        {
                                            OptionID = option.OptionID,
                                            Options = option.Options,
                                            IsAnswer = Convert.ToBoolean(option.IsAnswer)
                                        };
                                        qvm.QuestionOption.Add(qpvm);
                                    }
                                }
                            }
                            examReviewVM.Questions.Add(qvm);
                        }
                    }
                }
            }
            return examReviewVM;
        }
    }
}