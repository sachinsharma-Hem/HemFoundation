//using DocumentFormat.OpenXml.InkML;
//using DocumentFormat.OpenXml.Office.SpreadSheetML.Y2023.MsForms;
//using DocumentFormat.OpenXml.Spreadsheet;
//using DocumentFormat.OpenXml.Wordprocessing;
using HemFoundation.Data;
using HemFoundation.DTOs;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HemFoundation.Controllers
{
    [ApiController]
    [Route("api/course-authoring")]
    public class CourseAuthoringController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public CourseAuthoringController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _context = context;
            _environment = environment;
            _configuration = configuration;
        }

        // ============================================================
        // COURSE DROPDOWNS
        // ============================================================
        [HttpGet("dropdowns/{courseId}")]
        public async Task<IActionResult> GetCourseDropdowns(int courseId)
        {
            try
            {
                var topics = await _context.Topics
                    .AsNoTracking()
                    .Where(x => x.CourseID == courseId && x.ParentTopicID == null && x.IsActive == "1")
                    .OrderBy(x => x.Order)
                    .Select(x => new { id = x.TopicID, topicName = x.TopicName })
                    .ToListAsync();

                var subTopics = await _context.Topics
                    .AsNoTracking()
                    .Where(x => x.CourseID == courseId && x.ParentTopicID != null && x.IsActive == "1")
                    .OrderBy(x => x.Order)
                    .Select(x => new { id = x.TopicID, subTopicName = x.TopicName, courseTopicId = x.ParentTopicID, orderNo = x.Order })
                    .ToListAsync();

                var standards = await _context.StandardMasters
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.StandardName)
                    .Select(x => new { id = x.Id, name = x.StandardName })
                    .ToListAsync();

                var languages = await _context.Languages
                    .AsNoTracking()
                    .OrderBy(x => x.LanguageName)
                    .Select(x => new { id = x.LanguageID, name = x.LanguageName })
                    .ToListAsync();

                return Ok(new { success = true, topics, subTopics, standards, languages });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("search-courses")]
        public async Task<IActionResult> SearchCourses([FromQuery] string? query)
        {
            try
            {
                query = query?.Trim();
                if (string.IsNullOrWhiteSpace(query))
                {
                    return Ok(new { success = true, data = new List<object>() });
                }

                var courses = await _context.Courses
                    .AsNoTracking()
                    .Where(x => x.CourseName != null && x.CourseName.ToLower().Contains(query.ToLower()))
                    .OrderBy(x => x.CourseName)
                    .Take(50)
                    .Select(x => new { courseID = x.CourseID, courseName = x.CourseName })
                    .ToListAsync();

                return Ok(new { success = true, data = courses });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // TOPIC LIST
        // ============================================================
        [HttpGet("topic/list/{courseId}")]
        public async Task<IActionResult> GetTopics(int courseId)
        {
            try
            {
                var topics = await _context.Topics
                    .AsNoTracking()
                    .Where(x => x.CourseID == courseId && x.ParentTopicID == null && x.IsActive == "1")
                    .OrderBy(x => x.Order)
                    .ThenBy(x => x.TopicID)
                    .Select(x => new
                    {
                        id = x.TopicID,
                        topicName = x.TopicName,
                        description = x.Description,
                        orderNo = x.Order,
                        languageID = x.languageID,
                        trainerResource = x.TrainerResource,
                        trainerExercise = x.TrainerExercise,
                        subTopicCount = _context.Topics.Count(st => st.CourseID == courseId && st.ParentTopicID == x.TopicID && st.IsActive == "1")
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = topics });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("subtopic/list/{courseId}")]
        public async Task<IActionResult> GetSubTopics(int courseId)
        {
            try
            {
                var subTopics = await _context.Topics
                    .AsNoTracking()
                    .Where(x => x.CourseID == courseId && x.ParentTopicID != null)
                    .OrderBy(x => x.Order)
                    .ThenBy(x => x.TopicID)
                    .Select(x => new
                    {
                        id = x.TopicID,
                        subTopicName = x.TopicName,
                        courseTopicId = x.ParentTopicID,
                        orderNo = x.Order,
                        description = x.Description,
                        languageID = x.languageID,
                        isActive = x.IsActive,
                        trainerResource = x.TrainerResource,
                        trainerExercise = x.TrainerExercise,
                        trainerVideo = x.TrainerVideo,
                        numberOfQuestions = x.NoOfQuestion
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = subTopics });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // SAVE TOPIC
        // ============================================================
        public class SaveTopicRequest
        {
            public int Id { get; set; }
            public int CourseId { get; set; }
            public string TopicName { get; set; } = string.Empty;
            public string? Description { get; set; }
            public int OrderNo { get; set; }
            public int? LanguageID { get; set; }
            public IFormFile? ResourceFile { get; set; }
            public IFormFile? ExerciseFile { get; set; }
        }

        [HttpPost("topic/save")]
        [RequestSizeLimit(100 * 1024 * 1024)]
        public async Task<IActionResult> SaveTopic([FromForm] SaveTopicRequest model)
        {
            try
            {
                if (model.CourseId <= 0) return BadRequest(new { success = false, message = "Invalid Course." });
                if (string.IsNullOrWhiteSpace(model.TopicName)) return BadRequest(new { success = false, message = "Topic name is required." });
                if (model.OrderNo <= 0) return BadRequest(new { success = false, message = "Order number must be greater than 0." });

                Topics topic;
                if (model.Id > 0)
                {
                    topic = await _context.Topics.FirstOrDefaultAsync(x => x.TopicID == model.Id && x.CourseID == model.CourseId);
                    if (topic == null) return NotFound(new { success = false, message = "Topic not found." });
                    topic.ModifiedDate = DateTime.UtcNow;
                }
                else
                {
                    topic = new Topics { CourseID = model.CourseId, ParentTopicID = null, IsActive = "1", CreatedDate = DateTime.UtcNow };
                    _context.Topics.Add(topic);
                }

                topic.TopicName = model.TopicName.Trim();
                topic.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
                topic.Order = model.OrderNo;
                topic.languageID = model.LanguageID;

                if (model.ResourceFile != null && model.ResourceFile.Length > 0)
                {
                    topic.TrainerResource = await SaveTopicFileAsync(model.ResourceFile, "resource");
                }

                if (model.ExerciseFile != null && model.ExerciseFile.Length > 0)
                {
                    topic.TrainerExercise = await SaveTopicFileAsync(model.ExerciseFile, "exercise");
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = model.Id > 0 ? "Topic updated successfully." : "Topic created successfully.", id = topic.TopicID });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        private async Task<string> SaveTopicFileAsync(IFormFile file, string type)
        {
            var extension = Path.GetExtension(file.FileName);
            var folder = Path.Combine(_environment.WebRootPath, "uploads", "topics", type);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(folder, uniqueFileName);

            await using var stream = new FileStream(physicalPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/topics/{type}/{uniqueFileName}";
        }

        [HttpDelete("topic/delete/{id}")]
        public async Task<IActionResult> DeleteTopic(int id)
        {
            try
            {
                var topic = await _context.Topics.FirstOrDefaultAsync(x => x.TopicID == id && x.ParentTopicID == null);
                if (topic == null) return NotFound(new { success = false, message = "Topic not found." });

                var hasSubTopics = await _context.Topics.AnyAsync(x => x.ParentTopicID == id && x.IsActive == "1");
                if (hasSubTopics) return BadRequest(new { success = false, message = "This topic has sub-topics. Please remove or deactivate them first." });

                topic.IsActive = "0";
                topic.ModifiedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Topic deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // MCQ LIST
        // ============================================================
        [HttpGet("mcq/list/{courseId}")]
        public async Task<IActionResult> GetMcqs(int courseId)
        {
            try
            {
                var mcqs = await _context.Questions
                    .AsNoTracking()
                    .Where(q => q.CourseID == courseId && (q.isDeleted == false || q.isDeleted == null))
                    .OrderBy(q => q.QuestionID)
                    .Join(_context.Topics, q => q.TopicID, t => t.TopicID, (q, t) => new { q, t })
                    .Join(_context.Topics, qt => qt.q.SubTopicID, st => st.TopicID, (qt, st) => new { qt.q, qt.t, st })
                    .GroupJoin(_context.Languages, qst => qst.q.languageID, l => l.LanguageID, (qst, l) => new { qst.q, qst.t, qst.st, Languages = l })
                    .SelectMany(x => x.Languages.DefaultIfEmpty(), (x, l) => new
                    {
                        id = x.q.QuestionID,
                        questionName = x.q.QuestionName,
                        topicName = x.t.TopicName,
                        subTopicName = x.st.TopicName,
                        createdDate = x.q.CreatedDate,
                        createdBy = x.q.CreatedBy,
                        modifiedDate = x.q.ModifiedDate,
                        modifiedBy = x.q.ModifiedBy,
                        questionLevel = x.q.QuestionLevel,
                        complexityLevel = x.q.ComplexityLevel,
                        languageName = l != null ? l.LanguageName : "-",
                        answerDescription = x.q.AnswerDescription,
                        topicId = x.q.TopicID,
                        subTopicId = x.q.SubTopicID,
                        languageId = x.q.languageID,
                        options = x.q.QuestionOption.Where(o => o.Options != null && o.Options.Trim() != "").Select(o => new
                        {
                            optionId = o.OptionID,
                            optionText = o.Options,
                            isAnswer = o.IsAnswer ?? false
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = mcqs });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // PRACTICAL LABS LIST & SAVE
        // ============================================================
        [HttpGet("lab/list/{courseId}")]
        public async Task<IActionResult> GetPracticals(int courseId)
        {
            try
            {
                var practicals = await _context.WJ_Practicals
                    .AsNoTracking()
                    .Where(x => x.CourseID == courseId)
                    .OrderBy(x => x.OrderNo)
                    .ThenBy(x => x.PracticalID)
                    .Select(x => new
                    {
                        id = x.PracticalID,
                        courseId = x.CourseID,
                        title = x.Title,
                        practicalName = x.Title,
                        description = x.Description,
                        orderNo = x.OrderNo,
                        coins = x.Coins,
                        gems = x.Gems,
                        fileType = x.FileType,
                        videoLink = x.VideoLink,
                        thumbnail = x.Thumbnail,
                        practicalLevel = x.PracticalLevel,
                        code = x.Code,
                        student_Group = x.Student_Group,
                        resources = x.Resources,
                        practicalTopicID = x.PracticalTopicID,
                        practicalSubtopicID = x.PracticalSubtopicID,
                        practicalStandard = x.PracticalStandard,
                        isTask = x.isTask ?? false, // Fixed: safely maps bit/int to boolean
                        languageID = x.languageID,
                        type = x.Type
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = practicals });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class SavePracticalRequest
        {
            public int Id { get; set; }
            public int CourseRegistrationId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
            public int OrderNo { get; set; }
            public int Coins { get; set; } = 10;
            public int Gems { get; set; } = 5;
            public string? FileType { get; set; }
            public string? VideoLink { get; set; }
            public int? PracticalLevel { get; set; }
            public string? Code { get; set; }
            public string? Student_Group { get; set; }
            public int? PracticalTopicID { get; set; }
            public int? PracticalSubtopicID { get; set; }
            public string? PracticalStandard { get; set; }
            public bool IsTask { get; set; }
            public string? Type { get; set; }
            public IFormFile? ThumbnailFile { get; set; }
            public IFormFile? ResourceFile { get; set; }
        }

        public class InHouseUploadResponse
        {
            public bool success { get; set; }
            public string message { get; set; }
            public string filePath { get; set; }
            public string fileName { get; set; }
        }

        private async Task<string> UploadFileToInHouseServerAsync(IFormFile uploadDoc, string dynamicFolderPath)
        {
            if (uploadDoc == null || uploadDoc.Length == 0)
                return string.Empty;

            string apiKey = _configuration["FileSettings:ApiKey"] ?? "YOUR_API_KEY";
            string inHouseApiUrl = _configuration["FileSettings:InHouseUploadUrl"] ?? "https://your-inhouse-server-url/api/upload";

            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

                using var content = new MultipartFormDataContent();
                using var fileStream = uploadDoc.OpenReadStream();
                using var streamContent = new StreamContent(fileStream);

                content.Add(streamContent, "file", uploadDoc.FileName);
                content.Add(new StringContent(dynamicFolderPath), "folderPath");

                var response = await client.PostAsync(inHouseApiUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Failed to upload file to the in-house server storage.");
                }

                var apiResult = await response.Content.ReadFromJsonAsync<InHouseUploadResponse>();
                if (apiResult == null || !apiResult.success)
                {
                    throw new Exception("In-house server returned an unsuccessful upload response.");
                }

                return apiResult.filePath;
            }
        }

        [HttpPost("lab/save")]
        [RequestSizeLimit(100 * 1024 * 1024)]
        public async Task<IActionResult> SavePracticalLab([FromForm] SavePracticalRequest model)
        {
            try
            {
                if (model.CourseRegistrationId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid Course ID." });
                }

                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    return BadRequest(new { success = false, message = "Title is required." });
                }

                WJ_Practicals lab;

                if (model.Id > 0)
                {
                    lab = await _context.WJ_Practicals
                        .FirstOrDefaultAsync(x => x.PracticalID == model.Id && x.CourseID == model.CourseRegistrationId);

                    if (lab == null)
                    {
                        return NotFound(new { success = false, message = "Practical lab not found." });
                    }

                    lab.ModifiedDate = DateTime.UtcNow;
                    lab.ModifiedBy = 1;
                }
                else
                {
                    lab = new WJ_Practicals
                    {
                        CourseID = model.CourseRegistrationId,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = 1
                    };
                    _context.WJ_Practicals.Add(lab);
                }

                lab.Title = model.Title.Trim();
                lab.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
                lab.OrderNo = model.OrderNo;
                lab.Coins = model.Coins;
                lab.Gems = model.Gems;
                lab.FileType = model.FileType;
                lab.VideoLink = model.VideoLink;
                lab.PracticalLevel = model.PracticalLevel;
                lab.Code = model.Code;
                lab.Student_Group = string.IsNullOrWhiteSpace(model.Student_Group) ? "All" : model.Student_Group;
                lab.PracticalTopicID = model.PracticalTopicID;
                lab.PracticalSubtopicID = model.PracticalSubtopicID;
                lab.PracticalStandard = model.PracticalStandard;
                lab.isTask = model.IsTask;
                lab.Type = string.IsNullOrWhiteSpace(model.Type) ? "0" : model.Type;

                if (model.ThumbnailFile != null && model.ThumbnailFile.Length > 0)
                {
                    string thumbFolder = "Upload/Image/practical/Thumbnail";
                    lab.Thumbnail = await UploadFileToInHouseServerAsync(model.ThumbnailFile, thumbFolder);
                }

                if (model.ResourceFile != null && model.ResourceFile.Length > 0)
                {
                    string resFolder = "Upload/Practical/Resources";
                    lab.Resources = await UploadFileToInHouseServerAsync(model.ResourceFile, resFolder);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = model.Id > 0 ? "Practical lab updated successfully." : "Practical lab created successfully.",
                    id = lab.PracticalID
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("lab/delete/{id}")]
        public async Task<IActionResult> DeletePractical(int id)
        {
            try
            {
                var lab = await _context.WJ_Practicals.FirstOrDefaultAsync(x => x.PracticalID == id);
                if (lab == null)
                {
                    return NotFound(new { success = false, message = "Practical lab not found." });
                }

                _context.WJ_Practicals.Remove(lab);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Practical lab deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("subtopic/save")]
        [RequestSizeLimit(100 * 1024 * 1024)]
        public async Task<IActionResult> SaveSubTopic(
      [FromForm] SaveSubTopicRequest model)
        {
            try
            {
                if (model.CourseRegistrationId <= 0)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid Course ID."
                    });

                if (model.CourseTopicId <= 0)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Please select a topic."
                    });

                if (string.IsNullOrWhiteSpace(model.SubTopicName))
                    return BadRequest(new
                    {
                        success = false,
                        message = "Sub-topic name is required."
                    });

                if (model.DisplayOrderId <= 0)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Order number must be greater than 0."
                    });

                var parentTopic = await _context.Topics.FirstOrDefaultAsync(
                    x => x.TopicID == model.CourseTopicId
                      && x.CourseID == model.CourseRegistrationId
                      && x.ParentTopicID == null
                      && x.IsActive == "1");

                if (parentTopic == null)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Selected topic is invalid."
                    });

                Topics subTopic;

                if (model.Id > 0)
                {
                    subTopic = await _context.Topics.FirstOrDefaultAsync(
                        x => x.TopicID == model.Id
                          && x.CourseID == model.CourseRegistrationId
                          && x.ParentTopicID != null);

                    if (subTopic == null)
                        return NotFound(new
                        {
                            success = false,
                            message = "Sub-topic not found."
                        });

                    subTopic.ModifiedDate = DateTime.UtcNow;
                }
                else
                {
                    subTopic = new Topics
                    {
                        CourseID = model.CourseRegistrationId,
                        ParentTopicID = model.CourseTopicId,
                        IsActive = "1",
                        CreatedDate = DateTime.UtcNow
                    };

                    _context.Topics.Add(subTopic);
                }

                subTopic.ParentTopicID = model.CourseTopicId;
                subTopic.TopicName = model.SubTopicName.Trim();
                subTopic.Order = model.DisplayOrderId;
                subTopic.TrainerVideo = model.VideoLink;
                subTopic.languageID = model.LanguageID;
                subTopic.NoOfQuestion = model.NumberOfQuestions;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = model.Id > 0
                        ? "Sub-topic updated successfully."
                        : "Sub-topic created successfully.",
                    id = subTopic.TopicID
                });
            }
            catch (Exception ex)
            {
                // Replace with your existing exception logging mechanism if available.
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to save sub-topic."
                });
            }
        }

        [HttpDelete("subtopic/delete/{id}")]
        public async Task<IActionResult> DeleteSubTopic(int id)
        {
            try
            {
                var subTopic = await _context.Topics.FirstOrDefaultAsync(
                    x => x.TopicID == id && x.ParentTopicID != null);

                if (subTopic == null)
                    return NotFound(new
                    {
                        success = false,
                        message = "Sub-topic not found."
                    });

                // Do not remove a sub-topic that is referenced by questions.
                var hasQuestions = await _context.Questions.AnyAsync(
                    q => q.SubTopicID == id
                      && (q.isDeleted == false || q.isDeleted == null));

                if (hasQuestions)
                    return BadRequest(new
                    {
                        success = false,
                        message = "This sub-topic has MCQs. Delete or reassign those MCQs first."
                    });

                subTopic.IsActive = "0";
                subTopic.ModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Sub-topic deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to delete sub-topic."
                });
            }
        }

        [HttpPost("mcq/save")]
        public async Task<IActionResult> SaveMcq([FromBody] SaveMcqRequest model)
        {
            try
            {
                if (model.CourseId <= 0 ||
                    model.TopicId <= 0 ||
                    model.SubTopicId <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Select a valid course, topic and sub-topic."
                    });
                }

                if (string.IsNullOrWhiteSpace(model.QuestionName))
                    return BadRequest(new
                    {
                        success = false,
                        message = "Question is required."
                    });

                if (string.IsNullOrWhiteSpace(model.ComplexityLevel))
                    return BadRequest(new
                    {
                        success = false,
                        message = "Select question complexity."
                    });

                if (model.LanguageId == null)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Select a language."
                    });

                var topic = await _context.Topics.AnyAsync(
                    t => t.TopicID == model.TopicId
                      && t.CourseID == model.CourseId
                      && t.ParentTopicID == null
                      && t.IsActive == "1");

                var subTopic = await _context.Topics.AnyAsync(
                    t => t.TopicID == model.SubTopicId
                      && t.CourseID == model.CourseId
                      && t.ParentTopicID == model.TopicId
                      && t.IsActive == "1");

                if (!topic || !subTopic)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid topic or sub-topic selection."
                    });

                if (!await _context.Languages.AnyAsync(
                    l => l.LanguageID == model.LanguageId.Value))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid language."
                    });
                }

                var options = model.Options
                    .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
                    .ToList();

                if (options.Count < 2)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Enter at least two options."
                    });

                if (options.Count(o => o.IsAnswer) != 1)
                    return BadRequest(new
                    {
                        success = false,
                        message = "Select exactly one correct answer."
                    });

                Questions question;

                if (model.QuestionId > 0)
                {
                    question = await _context.Questions
                        .Include(q => q.QuestionOption)
                        .FirstOrDefaultAsync(q =>
                            q.QuestionID == model.QuestionId &&
                            q.CourseID == model.CourseId &&
                            (q.isDeleted == false || q.isDeleted == null));

                    if (question == null)
                        return NotFound(new
                        {
                            success = false,
                            message = "MCQ not found."
                        });

                    question.ModifiedDate = DateTime.UtcNow;
                    question.ModifiedBy = 1;

                    // Replace existing options with the submitted options.
                    _context.RemoveRange(question.QuestionOption.ToList());
                    question.QuestionOption.Clear();
                }
                else
                {
                    question = new Questions
                    {
                        CourseID = model.CourseId,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = 1,
                        isDeleted = false,
                        Status=true,
                        Type="HTML",
                        isAdminStatus=true,
                        QuestionLevel=1

                    };

                    _context.Questions.Add(question);
                }

                question.TopicID = model.TopicId;
                question.SubTopicID = model.SubTopicId;
                question.QuestionName = model.QuestionName.Trim();
                question.ComplexityLevel = model.ComplexityLevel;
                question.languageID = model.LanguageId;
                question.AnswerDescription = model.AnswerDescription;
                question.isDeleted = false;


                foreach (var option in options)
                {
                    question.QuestionOption.Add(new()
                    {
                        Options = option.OptionText.Trim(),
                        IsAnswer = option.IsAnswer
                    });
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    id = question.QuestionID,
                    message = model.QuestionId > 0
                        ? "MCQ updated successfully."
                        : "MCQ created successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to save MCQ."
                });
            }
        }

        [HttpDelete("mcq/delete/{id}")]
        public async Task<IActionResult> DeleteMcq(int id)
        {
            try
            {
                var question = await _context.Questions.FirstOrDefaultAsync(
                    q => q.QuestionID == id &&
                         (q.isDeleted == false || q.isDeleted == null));

                if (question == null)
                    return NotFound(new
                    {
                        success = false,
                        message = "MCQ not found."
                    });

                // Soft delete to preserve historical references.
                question.isDeleted = true;
                question.ModifiedDate = DateTime.UtcNow;
                question.ModifiedBy = 1;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "MCQ deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to delete MCQ."
                });
            }
        }


    }
}