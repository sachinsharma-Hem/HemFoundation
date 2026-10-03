using Dapper;
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
}