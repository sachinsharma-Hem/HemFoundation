using Dapper;
using HemFoundation.DTOs;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace HemFoundation.Controllers
{
    [Route("survey")]
    public class SurveyApiController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SurveyApiController> _logger;

        public SurveyApiController(
            IConfiguration configuration,
            ILogger<SurveyApiController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private NpgsqlConnection CreateConnection()
        {
            var connectionString =
                _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "DefaultConnection connection string is not configured.");
            }

            return new NpgsqlConnection(connectionString);
        }


        // =========================================================
        // PAGE
        // GET: /survey/SurveyReport_stdwise
        // =========================================================

        [HttpGet("SurveyReport_stdwise")]
        public IActionResult SurveyReport_stdwise()
        {
            try
            {
                var userId =
                    HttpContext.Session.GetInt32("UserID") ?? 0;

                ViewBag.UserID = userId;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading SurveyReport_stdwise page.");

                ViewBag.Error =
                    "Unable to load survey report.";

                return View();
            }
        }


        // =========================================================
        // API: COURSES
        // GET: /api/survey/courses
        // =========================================================

        [HttpGet("/api/survey/courses")]
        public async Task<IActionResult> GetCourses()
        {
            try
            {
                using var connection = CreateConnection();

                await connection.OpenAsync();

                const string sql = @"
                   SELECT
                 distinct maincourseid AS ""Value"",
                 maincoursename AS ""Text""
             FROM view_wj_syllabus
                where standard_!='Teacher'
 
             ORDER BY maincoursename;
                ";

                var courses =
                    await connection.QueryAsync<SelectListItemDto>(
                        sql);

                return Ok(new
                {
                    success = true,
                    data = courses.Select(x => new
                    {
                        value = x.Value,
                        text = x.Text
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading survey courses.");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message =
                            "Unable to load survey courses."
                    });
            }
        }


        // =========================================================
        // API: REPORT
        // POST: /api/survey/report
        // =========================================================

        [HttpPost("/api/survey/report")]
        public async Task<IActionResult> GetSurveyReport(
    [FromBody] SurveyReportRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid request."
                    });
                }

                if (string.IsNullOrWhiteSpace(request.MainCourseName))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Please select a course."
                    });
                }

                var mainCourseName = request.MainCourseName.Trim();

                await using var connection = CreateConnection();

                await connection.OpenAsync();

                // =====================================================
                // MAX QUESTION COUNT
                // =====================================================

                const string questionCountSql = @"
            SELECT COALESCE(
                public.fn_get_max_questions_count(
                    @MainCourseName
                ),
                0
            );
        ";

                var maxQuestionCount =
                    await connection.ExecuteScalarAsync<int>(
                        questionCountSql,
                        new
                        {
                            MainCourseName = mainCourseName
                        });

                // =====================================================
                // GET SURVEY DATA
                // =====================================================

                List<SurveyModel> surveyData;

                if (mainCourseName.Contains(
                        "Survey",
                        StringComparison.OrdinalIgnoreCase))
                {
                    surveyData = await GetSurveyDataAsync(
                        connection,
                        mainCourseName,
                        "get_survey_data_by_standard_he");
                }
                else
                {
                    surveyData = await GetSurveyDataAsync(
                        connection,
                        mainCourseName,
                        "get_survey_data_by_standard");
                }

                // =====================================================
                // RESPONSE
                // =====================================================

                return Ok(new
                {
                    success = true,
                    data = surveyData,
                    maxQuestionCount = maxQuestionCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating survey report for course {Course}",
                    request?.MainCourseName);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message =
                            "An error occurred while generating the survey report.",
                        detail = ex.Message
                    });
            }
        }

        // =========================================================
        // POSTGRESQL REFCURSOR FUNCTION
        // =========================================================
        private NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));
        }
        private async Task<List<SurveyModel>> GetSurveyDataAsync(
     NpgsqlConnection connection,
     string mainCourseName,
     string functionName)
        {
            const string cursorName = "survey_result";

            await using var transaction =
                await connection.BeginTransactionAsync();

            try
            {
                // IMPORTANT:
                // Only allow known function names.
                if (functionName != "get_survey_data_by_standard" &&
                    functionName != "get_survey_data_by_standard_he")
                {
                    throw new ArgumentException(
                        "Invalid survey function name.",
                        nameof(functionName));
                }

                // PostgreSQL function call
                var sql = $@"
            SELECT public.{functionName}(
                @MainCourseName,
                @CursorName::refcursor
            );
        ";

                await connection.ExecuteAsync(
                    sql,
                    new
                    {
                        MainCourseName = mainCourseName,
                        CursorName = cursorName
                    },
                    transaction);

                // Read the cursor
                var result = await connection.QueryAsync<SurveyModel>(
                    $"FETCH ALL IN \"{cursorName}\";",
                    transaction: transaction);

                await transaction.CommitAsync();

                return result.ToList();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }


        [HttpGet("list")]
        public async Task<IActionResult> GetSurveys(
            [FromQuery] int courseId,
            [FromQuery] int? topicId = null,
            [FromQuery] int? subTopicId = null,
            [FromQuery] int? languageId = null)
        {
            try
            {
                if (courseId <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "A valid courseId is required."
                    });
                }

                const string sql = @"
                    SELECT
                        id          AS ""Id"",
                        courseid    AS ""CourseId"",
                        topicid     AS ""TopicId"",
                        topicname   AS ""TopicName"",
                        subtopicid  AS ""SubTopicId"",
                        question    AS ""Question"",
                        option1     AS ""Option1"",
                        option2     AS ""Option2"",
                        option3     AS ""Option3"",
                        option4     AS ""Option4"",
                        option5     AS ""Option5"",
                        languageid  AS ""LanguageId""
                    FROM public.hf_surveydata
                    WHERE courseid = @CourseId
                      AND (@TopicId IS NULL OR topicid = @TopicId)
                      AND (@SubTopicId IS NULL OR subtopicid = @SubTopicId)
                      AND (@LanguageId IS NULL OR languageid = @LanguageId)
                    ORDER BY id DESC;";

                await using var connection = GetConnection();

                var surveys = await connection.QueryAsync<SurveyDataDto>(
                    sql,
                    new
                    {
                        CourseId = courseId,
                        TopicId = topicId,
                        SubTopicId = subTopicId,
                        LanguageId = languageId
                    });

                return Ok(new
                {
                    success = true,
                    data = surveys
                });
            }
            catch (Exception ex)
            {
                // Log the exception using your application's existing logger.
                return StatusCode(500, new
                {
                    success = false,
                    message = "Unable to load survey questions.",
                    detail = ex.Message
                });
            }
        }

        // GET: /api/survey/91
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetSurveyById(int id)
        {
            const string sql = @"
        SELECT
            id AS ""Id"",
            courseid AS ""CourseId"",
            topicid AS ""TopicId"",
            topicname AS ""TopicName"",
            subtopicid AS ""SubTopicId"",
            question AS ""Question"",
            option1 AS ""Option1"",
            option2 AS ""Option2"",
            option3 AS ""Option3"",
            option4 AS ""Option4"",
            option5 AS ""Option5"",
            languageid AS ""LanguageId""
        FROM public.hf_surveydata
        WHERE id = @Id;";

            await using var connection = GetConnection();

            var survey = await connection.QuerySingleOrDefaultAsync<SurveyDataDto>(
                sql, new { Id = id });

            if (survey == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Survey question not found."
                });
            }

            return Ok(new { success = true, data = survey });
        }

        // POST: /api/survey/save
        [HttpPost("save")]
        public async Task<IActionResult> SaveSurvey(
      [FromBody] SaveSurveyRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid survey data.",
                    errors = ModelState
                });

            const string sql = @"
        INSERT INTO public.hf_surveydata 
        (
            courseid, topicid, topicname, subtopicid, 
            question, option1, option2, option3, 
            option4, option5, languageid
        ) 
        VALUES 
        (
            @CourseId, @TopicId, @TopicName, @SubTopicId, 
            @Question, @Option1, @Option2, @Option3, 
            @Option4, @Option5, @LanguageId
        ) 
        RETURNING id;";

            await using var connection = GetConnection();

            // Changed <int> to <long> to match PostgreSQL bigint (SERIAL / BIGSERIAL / IDENTITY)
            var newId = await connection.ExecuteScalarAsync<long>(
                sql, request);

            return Ok(new
            {
                success = true,
                message = "Survey question saved successfully.",
                id = newId
            });
        }

        // PUT: /api/survey/update/91
        [HttpPut("update/{id:int}")]
        public async Task<IActionResult> UpdateSurvey(
            int id,
            [FromBody] SaveSurveyRequest request)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Invalid survey ID." });

            if (!ModelState.IsValid)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid survey data.",
                    errors = ModelState
                });

            const string sql = @"
        UPDATE public.hf_surveydata
        SET
            courseid = @CourseId,
            topicid = @TopicId,
            topicname = @TopicName,
            subtopicid = @SubTopicId,
            question = @Question,
            option1 = @Option1,
            option2 = @Option2,
            option3 = @Option3,
            option4 = @Option4,
            option5 = @Option5,
            languageid = @LanguageId
        WHERE id = @Id;";

            await using var connection = GetConnection();

            var affected = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                request.CourseId,
                request.TopicId,
                request.TopicName,
                request.SubTopicId,
                request.Question,
                request.Option1,
                request.Option2,
                request.Option3,
                request.Option4,
                request.Option5,
                request.LanguageId
            });

            if (affected == 0)
                return NotFound(new
                {
                    success = false,
                    message = "Survey question not found."
                });

            return Ok(new
            {
                success = true,
                message = "Survey question updated successfully."
            });
        }

        // DELETE: /api/survey/delete/91
        [HttpDelete("delete/{id:int}")]
        public async Task<IActionResult> DeleteSurvey(int id)
        {
            if (id <= 0)
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid survey ID."
                });

            const string sql = @"
        DELETE FROM public.hf_surveydata
        WHERE id = @Id;";

            await using var connection = GetConnection();

            var affected = await connection.ExecuteAsync(sql, new { Id = id });

            if (affected == 0)
                return NotFound(new
                {
                    success = false,
                    message = "Survey question not found."
                });

            return Ok(new
            {
                success = true,
                message = "Survey question deleted successfully."
            });
        }

        // GET: /api/survey/dropdowns?courseId=487
        [HttpGet("dropdowns")]
        public async Task<IActionResult> GetSurveyDropdowns(
            [FromQuery] int courseId)
        {
            if (courseId <= 0)
                return BadRequest(new
                {
                    success = false,
                    message = "A valid courseId is required."
                });

            const string sql = @"
        SELECT DISTINCT
            topicname AS ""Name""
        FROM public.hf_surveydata
        WHERE courseid = @CourseId
        ORDER BY ""Name"";";

            const string subTopicSql = @"
        SELECT DISTINCT
            subtopicid AS ""Id""
        FROM public.hf_surveydata
        WHERE courseid = @CourseId
        ORDER BY ""Id"";";

            const string languageSql = @"
        SELECT DISTINCT
            languageid AS ""Id""
        FROM public.hf_surveydata
        WHERE courseid = @CourseId
        ORDER BY ""Id"";";

            await using var connection = GetConnection();

            var parameters = new { CourseId = courseId };

            var surveyTypes = await connection.QueryAsync<string>(
                sql, parameters);

            var subTopics = await connection.QueryAsync<int>(
                subTopicSql, parameters);

            var languages = await connection.QueryAsync<int>(
                languageSql, parameters);

            return Ok(new
            {
                success = true,
                data = new
                {
                    surveyTypes,
                    subTopics,
                    languages
                }
            });
        }
    }


    // =============================================================
    // REQUEST DTO
    // =============================================================

    public class SurveyReportRequest
    {
        public string? MainCourseName { get; set; }
    }


    // =============================================================
    // COURSE DTO
    // =============================================================

    public class SelectListItemDto
    {
        public int Value { get; set; }

        public string Text { get; set; } = string.Empty;
    }


    // =============================================================
    // SURVEY MODEL
    // =============================================================

    public class SurveyModel
    {
        public long UserId { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }
         public string? Gender { get; set; }

        public string? Standard { get; set; }

        public string? Division { get; set; }

        public string? InstituteName { get; set; }

        public string? MobileNo { get; set; }


        public decimal? TotalPrePoint { get; set; }

        public decimal? TotalPostPoint { get; set; }


        public decimal? PreQ1Point { get; set; }

        public decimal? PostQ1Point { get; set; }


        public decimal? PreQ2Point { get; set; }

        public decimal? PostQ2Point { get; set; }


        public decimal? PreQ3Point { get; set; }

        public decimal? PostQ3Point { get; set; }


        public decimal? PreQ4Point { get; set; }

        public decimal? PostQ4Point { get; set; }


        public decimal? PreQ5Point { get; set; }

        public decimal? PostQ5Point { get; set; }


        public decimal? PreQ6Point { get; set; }

        public decimal? PostQ6Point { get; set; }


        public decimal? PreQ7Point { get; set; }

        public decimal? PostQ7Point { get; set; }


        public decimal? PreQ8Point { get; set; }

        public decimal? PostQ8Point { get; set; }


        public decimal? PreQ9Point { get; set; }

        public decimal? PostQ9Point { get; set; }


        public decimal? PreQ10Point { get; set; }

        public decimal? PostQ10Point { get; set; }


        public decimal? PreQ11Point { get; set; }

        public decimal? PostQ11Point { get; set; }


        public decimal? PreQ12Point { get; set; }

        public decimal? PostQ12Point { get; set; }


        public decimal? PreQ13Point { get; set; }

        public decimal? PostQ13Point { get; set; }


        public decimal? PreQ14Point { get; set; }

        public decimal? PostQ14Point { get; set; }


        public decimal? PreQ15Point { get; set; }

        public decimal? PostQ15Point { get; set; }
    }
    public class SurveyReportModel
    {
        public int? UserID { get; set; }
        public int? QuestionNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Gender { get; set; }
        public string? Standard { get; set; }
        public string? Division { get; set; }
        public string? InstituteName { get; set; }
        public string? MobileNo { get; set; }
        public string? CourseName { get; set; }
        public string? TopicName { get; set; }
        public string? Questions { get; set; }
        public string? Option1 { get; set; }
        public string? Option2 { get; set; }
        public string? Option3 { get; set; }
        public string? Option4 { get; set; }
        public string? Option5 { get; set; }
        public string? UserSelectedAnswer { get; set; }
        public int? Points { get; set; }
        public int? TotalPoints { get; set; }
    }
}