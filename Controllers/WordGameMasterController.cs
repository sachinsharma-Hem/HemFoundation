using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace HemFoundation.Controllers
{
    public class IdRequest { public int Id { get; set; } }

    public class TopicSaveRequest
    {
        public int TopicId { get; set; }
        public string? TopicName { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class QuestionSaveRequest
    {
        public int QuestionId { get; set; }
        public int TopicId { get; set; }
        public int CourseId { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class TopicDto
    {
        public int TopicId { get; set; }
        public string? TopicName { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int QuestionCount { get; set; }
        public int PlayCount { get; set; }
    }

    public class QuestionDto
    {
        public int QuestionId { get; set; }
        public int TopicId { get; set; }
        public string? TopicName { get; set; }
        public int? CourseId { get; set; }
        public string? CourseName { get; set; }
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public bool IsActive { get; set; }
        public int PlayCount { get; set; }
    }

    public class CourseDto { public int CourseId { get; set; } public string? CourseName { get; set; } }
    public class UsageDto { public int Questions { get; set; } public int Plays { get; set; } }

   
    [AutoValidateAntiforgeryToken]
    public class WordGameMasterController : Controller
    {
        private readonly IConfiguration _config;
        private readonly ILogger<WordGameMasterController> _logger;

        // Always camelCase for the JS, regardless of global JSON settings
        private static readonly JsonSerializerOptions CamelJson =
            new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private const string ActiveBit = "CASE WHEN @IsActive THEN B'1' ELSE B'0' END";

        public WordGameMasterController(IConfiguration config, ILogger<WordGameMasterController> logger)
        {
            _config = config;
            _logger = logger;
        }

        private NpgsqlConnection Db() => new(_config.GetConnectionString("DefaultConnection"));

        private JsonResult Result(bool success, string message, object? data = null) =>
            Json(new { success, message, data }, CamelJson);

        [HttpGet]
        public IActionResult Index() => View();

        // ---------- Lookups ----------
        [HttpGet]
        public async Task<IActionResult> Courses()
        {
            try
            {
                await using var db = Db();
                var rows = await db.QueryAsync<CourseDto>(
                    "SELECT courseid AS CourseId, coursename AS CourseName FROM courses ORDER BY coursename");
                return Result(true, "OK", rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load courses");
                return Result(false, "Failed to load courses.");
            }
        }

        // ---------- Topics ----------
        [HttpGet]
        public async Task<IActionResult> Topics()
        {
            try
            {
                await using var db = Db();
                var rows = await db.QueryAsync<TopicDto>(@"
                    SELECT t.wg_topicid                       AS TopicId,
                           t.wg_topicname                     AS TopicName,
                           t.description                      AS Description,
                           COALESCE(t.isactive = B'1', false) AS IsActive,
                           (SELECT COUNT(*) FROM wj_wordgamequestions q
                             WHERE q.wgtopicid = t.wg_topicid)::int AS QuestionCount,
                           (SELECT COUNT(*) FROM wordgamesolution s
                              JOIN wj_wordgamequestions q ON q.wgid = s.wgid
                             WHERE q.wgtopicid = t.wg_topicid)::int AS PlayCount
                    FROM wj_wordgametopic t
                    ORDER BY t.wg_topicname");
                return Result(true, "OK", rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load topics");
                return Result(false, "Failed to load topics.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveTopic([FromBody] TopicSaveRequest req)
        {
            var name = req.TopicName?.Trim();
            var desc = req.Description?.Trim();

            if (string.IsNullOrEmpty(name)) return Result(false, "Topic name is required.");
            if (name.Length > 100) return Result(false, "Topic name must be 100 characters or fewer.");
            if (desc?.Length > 500) return Result(false, "Description must be 500 characters or fewer.");

            try
            {
                await using var db = Db();

                var duplicate = await db.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) FROM wj_wordgametopic
                    WHERE lower(btrim(wg_topicname)) = lower(@name) AND wg_topicid <> @id",
                    new { name, id = req.TopicId });
                if (duplicate > 0) return Result(false, "A topic with this name already exists.");

                if (req.TopicId == 0)
                {
                    await db.ExecuteAsync($@"
                        INSERT INTO wj_wordgametopic (wg_topicname, description, isactive)
                        VALUES (@name, @desc, {ActiveBit})",
                        new { name, desc, req.IsActive });
                    return Result(true, "Topic added.");
                }

                var n = await db.ExecuteAsync($@"
                    UPDATE wj_wordgametopic
                       SET wg_topicname = @name, description = @desc, isactive = {ActiveBit}
                     WHERE wg_topicid = @TopicId",
                    new { name, desc, req.IsActive, req.TopicId });

                return n > 0 ? Result(true, "Topic updated.") : Result(false, "Topic not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save topic");
                return Result(false, "Failed to save topic.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTopic([FromBody] IdRequest req)
        {
            try
            {
                await using var db = Db();

                var u = await db.QuerySingleAsync<UsageDto>(@"
                    SELECT (SELECT COUNT(*) FROM wj_wordgamequestions WHERE wgtopicid = @id)::int AS Questions,
                           (SELECT COUNT(*) FROM wordgamesolution s
                              JOIN wj_wordgamequestions q ON q.wgid = s.wgid
                             WHERE q.wgtopicid = @id)::int AS Plays",
                    new { id = req.Id });

                if (u.Questions > 0)
                {
                    return Result(false, u.Plays > 0
                        ? $"This topic is in use: it has {u.Questions} question(s) played {u.Plays} time(s) by students. Deactivate it instead of deleting."
                        : $"This topic is in use by {u.Questions} question(s). Move or remove those questions first, or deactivate the topic.");
                }

                // The NOT EXISTS repeats the check atomically in case a question was added a moment ago
                var n = await db.ExecuteAsync(@"
                    DELETE FROM wj_wordgametopic t
                     WHERE t.wg_topicid = @id
                       AND NOT EXISTS (SELECT 1 FROM wj_wordgamequestions q WHERE q.wgtopicid = t.wg_topicid)",
                    new { id = req.Id });

                return n > 0
                    ? Result(true, "Topic deleted.")
                    : Result(false, "Topic not found, or it was just put to use.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete topic {Id}", req.Id);
                return Result(false, "Failed to delete topic.");
            }
        }

        // ---------- Questions ----------
        [HttpGet]
        public async Task<IActionResult> Questions()
        {
            try
            {
                await using var db = Db();
                var rows = await db.QueryAsync<QuestionDto>(@"
                    SELECT q.wgid                                AS QuestionId,
                           q.wgtopicid                           AS TopicId,
                           t.wg_topicname                        AS TopicName,
                           q.courseid                            AS CourseId,
                           c.coursename                          AS CourseName,
                           q.wg_question                         AS Question,
                           q.wg_answer                           AS Answer,
                           COALESCE(q.gameisactive = B'1', false) AS IsActive,
                           (SELECT COUNT(*) FROM wordgamesolution s WHERE s.wgid = q.wgid)::int AS PlayCount
                    FROM wj_wordgamequestions q
                    LEFT JOIN wj_wordgametopic t ON t.wg_topicid = q.wgtopicid
                    LEFT JOIN courses c          ON c.courseid   = q.courseid
                    ORDER BY t.wg_topicname, q.wgid");
                return Result(true, "OK", rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load questions");
                return Result(false, "Failed to load questions.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveQuestion([FromBody] QuestionSaveRequest req)
        {
            var question = req.Question?.Trim();
            var answer = req.Answer?.Trim();

            if (req.TopicId <= 0) return Result(false, "Please select a topic.");
            if (req.CourseId <= 0) return Result(false, "Please select a course.");
            if (string.IsNullOrEmpty(question)) return Result(false, "Question is required.");
            if (question.Length > 500) return Result(false, "Question must be 500 characters or fewer.");
            if (string.IsNullOrEmpty(answer)) return Result(false, "Answer is required.");
            if (answer.Length > 50) return Result(false, "Answer must be 50 characters or fewer.");

            try
            {
                await using var db = Db();

                if (await db.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM wj_wordgametopic WHERE wg_topicid = @TopicId", new { req.TopicId }) == 0)
                    return Result(false, "Selected topic does not exist.");

                if (await db.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM courses WHERE courseid = @CourseId", new { req.CourseId }) == 0)
                    return Result(false, "Selected course does not exist.");

                var duplicate = await db.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) FROM wj_wordgamequestions
                    WHERE wgtopicid = @TopicId AND courseid = @CourseId
                      AND lower(btrim(wg_question)) = lower(@question)
                      AND wgid <> @QuestionId",
                    new { req.TopicId, req.CourseId, question, req.QuestionId });
                if (duplicate > 0) return Result(false, "This question already exists for the selected topic and course.");

                if (req.QuestionId == 0)
                {
                    await db.ExecuteAsync($@"
                        INSERT INTO wj_wordgamequestions (wgtopicid, courseid, wg_question, wg_answer, gameisactive)
                        VALUES (@TopicId, @CourseId, @question, @answer, {ActiveBit})",
                        new { req.TopicId, req.CourseId, question, answer, req.IsActive });
                    return Result(true, "Question added.");
                }

                var n = await db.ExecuteAsync($@"
                    UPDATE wj_wordgamequestions
                       SET wgtopicid = @TopicId, courseid = @CourseId,
                           wg_question = @question, wg_answer = @answer,
                           gameisactive = {ActiveBit}
                     WHERE wgid = @QuestionId",
                    new { req.TopicId, req.CourseId, question, answer, req.IsActive, req.QuestionId });

                return n > 0 ? Result(true, "Question updated.") : Result(false, "Question not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save question");
                return Result(false, "Failed to save question.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteQuestion([FromBody] IdRequest req)
        {
            try
            {
                await using var db = Db();

                var plays = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM wordgamesolution WHERE wgid = @id", new { id = req.Id });

                if (plays > 0)
                    return Result(false, $"This question has been played {plays} time(s) by students and cannot be deleted. Deactivate it instead.");

                var n = await db.ExecuteAsync(@"
                    DELETE FROM wj_wordgamequestions q
                     WHERE q.wgid = @id
                       AND NOT EXISTS (SELECT 1 FROM wordgamesolution s WHERE s.wgid = q.wgid)",
                    new { id = req.Id });

                return n > 0
                    ? Result(true, "Question deleted.")
                    : Result(false, "Question not found, or it was just played.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete question {Id}", req.Id);
                return Result(false, "Failed to delete question.");
            }
        }
    }
}