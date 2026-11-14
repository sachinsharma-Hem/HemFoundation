using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.HemSampark;
using HemFoundation.Services.HemSampark;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HemFoundation.Controllers
{
    /// <summary>
    /// HEM Sampark for logged-in users: today's published publication, its
    /// quiz, and the user's own results. Admin endpoints live in
    /// HemSamparkAdminApiController.
    /// </summary>
    [Route("api/hem-sampark")]
    [ApiController]
    [HemSamparkUser]
    public class HemSamparkApiController : ControllerBase
    {
        private const string DateTimeDisplay = "dd MMM yyyy, hh:mm tt";

        private readonly ApplicationDbContext _context;
        private readonly HemSamparkPdfService _pdf;
        private readonly HemSamparkClock _clock;
        private readonly HemSamparkAccess _access;
        private readonly IConfiguration _config;
        private readonly ILogger<HemSamparkApiController> _logger;

        public HemSamparkApiController(
            ApplicationDbContext context,
            HemSamparkPdfService pdf,
            HemSamparkClock clock,
            HemSamparkAccess access,
            IConfiguration config,
            ILogger<HemSamparkApiController> logger)
        {
            _context = context;
            _pdf = pdf;
            _clock = clock;
            _access = access;
            _config = config;
            _logger = logger;
        }

        // The [HemSamparkUser] filter guarantees a logged-in user.
        private HemSamparkIdentity Me => _access.Current!;

        private int PassPercentage =>
            Math.Clamp(_config.GetValue<int?>("HemSampark:PassPercentage") ?? 50, 0, 100);

        private IActionResult Fail(int statusCode, string message) =>
            StatusCode(statusCode, new { success = false, message });

        private static string LongDate(DateTime date) =>
            date.ToString("dddd, dd MMMM yyyy", CultureInfo.InvariantCulture);

        private Task<MasterSetting?> GetMasterAsync() =>
            _context.mastersettings.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();

        /// <summary>
        /// The publication users may see for a business date: published, with
        /// its published PDF. Drafts and archived items never match.
        /// </summary>
        private Task<DailySamparkEntry?> FindPublishedForDateAsync(DateTime date)
        {
            var end = date.AddDays(1);

            return _context.dailysamparkentries.AsNoTracking()
                .Include(x => x.Questions)
                .Where(x => x.Status == HemSamparkStatus.Published
                            && x.PublishedPdfUrl != null
                            && x.EntryDate >= date && x.EntryDate < end)
                .OrderByDescending(x => x.PublishedAt)
                .FirstOrDefaultAsync();
        }

        private static string PlainText(string? html, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";

            var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
        }

        /// <summary>Result of one submission, including the per-question review.</summary>
        private async Task<object> BuildResultAsync(
            hemsampark_quiz_submission submission, DailySamparkEntry entry, MasterSetting? master)
        {
            var answers = await _context.hemsampark_quiz_answers.AsNoTracking()
                .Where(a => a.SubmissionId == submission.Id)
                .ToListAsync();

            var snapshot = HemSamparkSnapshot.Load(entry, master);
            var byQuestion = answers
                .GroupBy(a => a.QuestionId)
                .ToDictionary(g => g.Key, g => g.First());

            int total = submission.TotalQuestions;
            int correct = submission.Score;
            int answered = Math.Min(total, answers.Count);
            double percentage = HemSamparkScore.Percentage(correct, total);

            // The review is revealed only after submission, so showing the
            // correct option here is safe.
            var review = snapshot.Questions
                .Where(q => byQuestion.ContainsKey(q.Id) || snapshot.Questions.Count == total)
                .Select((q, index) =>
                {
                    byQuestion.TryGetValue(q.Id, out var answer);

                    return new
                    {
                        number = index + 1,
                        text = q.Text,
                        options = q.Options,
                        selected = answer?.SelectedOption ?? 0,
                        correct = q.Correct,
                        isCorrect = answer?.IsCorrect ?? false,
                        explanation = q.Explanation
                    };
                })
                .ToList();

            return new
            {
                submissionId = submission.Id,
                publicationId = entry.Id,
                publicationTitle = snapshot.Title,
                publicationDate = LongDate(entry.EntryDate),
                totalQuestions = total,
                correct,
                incorrect = Math.Max(0, answered - correct),
                unanswered = Math.Max(0, total - answered),
                score = $"{correct}/{total}",
                percentage,
                status = HemSamparkScore.Status(percentage, PassPercentage),
                passed = percentage >= PassPercentage,
                completedAt = submission.SubmittedAt.ToString(DateTimeDisplay, CultureInfo.InvariantCulture),
                review
            };
        }

        // =====================================================================
        // DASHBOARD: today's publication + the user's summary
        // =====================================================================

        [HttpGet("today")]
        public async Task<IActionResult> GetToday()
        {
            var me = Me;
            var now = _clock.Now;
            var today = now.Date;

            var master = await GetMasterAsync();
            var entry = await FindPublishedForDateAsync(today);

            object? publication = null;

            if (entry != null)
            {
                var snapshot = HemSamparkSnapshot.Load(entry, master);

                var submission = await _context.hemsampark_quiz_submissions.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.DailyEntryId == entry.Id && s.UserId == me.Key);

                bool pdfAvailable = _pdf.ResolvePdfPath(entry.PublishedPdfUrl) != null;
                string version = (entry.PublishedAt?.Ticks ?? 0).ToString();
                string baseUrl = $"/api/hem-sampark/publications/{entry.Id}";

                publication = new
                {
                    id = entry.Id,
                    title = snapshot.Title,
                    date = LongDate(entry.EntryDate),
                    dayHindi = snapshot.DayHindi,
                    issueNumber = snapshot.IssueNumber,
                    headingEn = snapshot.HeadingEn,
                    headingHi = snapshot.HeadingHi,
                    summary = PlainText(snapshot.ContentEn, 260),
                    morningMantra = snapshot.MorningMantraEn,
                    valuesInFocus = snapshot.ValuesInFocusEn,
                    pdfAvailable,
                    pdfUrl = pdfAvailable ? $"{baseUrl}/pdf?v={version}" : null,
                    downloadUrl = pdfAvailable ? $"{baseUrl}/pdf?download=true&v={version}" : null,
                    imageUrl = _pdf.ResolvePreviewImagePath(entry.PublishedPdfUrl) != null
                        ? $"{baseUrl}/image?v={version}"
                        : null,
                    quiz = new
                    {
                        totalQuestions = snapshot.Questions.Count,
                        available = snapshot.Questions.Count > 0,
                        attempted = submission != null,
                        result = submission == null
                            ? null
                            : await BuildResultAsync(submission, entry, master)
                    }
                };
            }

            return Ok(new
            {
                success = true,
                userName = string.IsNullOrWhiteSpace(me.DisplayName) ? "Champion" : me.DisplayName,
                greeting = now.Hour < 12 ? "Good Morning" : now.Hour < 17 ? "Good Afternoon" : "Good Evening",
                today = LongDate(today),
                publication,
                message = publication == null ? "Today's publication is not available yet." : null,
                performance = await BuildPerformanceAsync(me.Key, today)
            });
        }

        /// <summary>Totals, current streak and the last seven days for one user.</summary>
        private async Task<object> BuildPerformanceAsync(string userKey, DateTime today)
        {
            var mine = _context.hemsampark_quiz_submissions.AsNoTracking()
                .Where(s => s.UserId == userKey);

            int attempts = await mine.CountAsync();

            double average = attempts == 0
                ? 0
                : await mine.Where(s => s.TotalQuestions > 0)
                      .Select(s => (double?)(s.Score * 100.0 / s.TotalQuestions))
                      .AverageAsync() ?? 0;

            double best = attempts == 0
                ? 0
                : await mine.Where(s => s.TotalQuestions > 0)
                      .Select(s => (double?)(s.Score * 100.0 / s.TotalQuestions))
                      .MaxAsync() ?? 0;

            // Published days in the recent window, and which of them I completed.
            var windowStart = today.AddDays(-60);
            var windowEnd = today.AddDays(1);

            var published = await _context.dailysamparkentries.AsNoTracking()
                .Where(x => x.Status == HemSamparkStatus.Published
                            && x.EntryDate >= windowStart && x.EntryDate < windowEnd)
                .Select(x => new { x.Id, x.EntryDate })
                .ToListAsync();

            var publishedIds = published.Select(p => p.Id).ToList();

            var doneIds = (await mine
                    .Where(s => publishedIds.Contains(s.DailyEntryId))
                    .Select(s => s.DailyEntryId)
                    .ToListAsync())
                .ToHashSet();

            var byDate = published
                .GroupBy(p => p.EntryDate.Date)
                .ToDictionary(g => g.Key, g => g.Any(p => doneIds.Contains(p.Id)));

            // Days without a publication neither add to nor break the streak;
            // today only counts once it has been completed.
            int streak = 0;
            for (var day = today; day >= windowStart; day = day.AddDays(-1))
            {
                if (!byDate.TryGetValue(day, out var done)) continue;
                if (done) { streak++; continue; }
                if (day == today) continue;
                break;
            }

            var week = Enumerable.Range(0, 7)
                .Select(offset => today.AddDays(offset - 6))
                .Select(day => new
                {
                    label = day.ToString("ddd", CultureInfo.InvariantCulture),
                    date = day.ToString("dd MMM", CultureInfo.InvariantCulture),
                    state = !byDate.TryGetValue(day, out var done) ? "none"
                        : done ? "done"
                        : day == today ? "today"
                        : "missed"
                })
                .ToList();

            return new
            {
                attempts,
                averagePercent = Math.Round(average, 1),
                bestPercent = Math.Round(best, 1),
                streak,
                week
            };
        }

        // =====================================================================
        // PUBLISHED PDF (and its image preview)
        // =====================================================================

        /// <summary>
        /// A user may open the published PDF of today's publication, or of a
        /// past one they took the quiz for. Never a draft, never a future date.
        /// </summary>
        private async Task<DailySamparkEntry?> FindReadableAsync(int id)
        {
            var today = _clock.Today;
            var tomorrow = today.AddDays(1);

            var entry = await _context.dailysamparkentries.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id
                                          && x.Status == HemSamparkStatus.Published
                                          && x.PublishedPdfUrl != null
                                          && x.EntryDate < tomorrow);

            if (entry == null) return null;
            if (entry.EntryDate.Date == today) return entry;

            var key = Me.Key;
            bool attempted = await _context.hemsampark_quiz_submissions
                .AnyAsync(s => s.DailyEntryId == id && s.UserId == key);

            return attempted ? entry : null;
        }

        [HttpGet("publications/{id:int}/pdf")]
        public async Task<IActionResult> GetPdf(int id, [FromQuery] bool download = false)
        {
            var entry = await FindReadableAsync(id);
            var path = entry == null ? null : _pdf.ResolvePdfPath(entry.PublishedPdfUrl);

            if (path == null) return NotFound();

            Response.Headers.CacheControl = "private, max-age=300";

            return download
                ? PhysicalFile(path, "application/pdf", $"HEM-Sampark-{entry!.EntryDate:yyyy-MM-dd}.pdf")
                : PhysicalFile(path, "application/pdf");
        }

        [HttpGet("publications/{id:int}/image")]
        public async Task<IActionResult> GetImage(int id)
        {
            var entry = await FindReadableAsync(id);
            var path = entry == null ? null : _pdf.ResolvePreviewImagePath(entry.PublishedPdfUrl);

            if (path == null) return NotFound();

            Response.Headers.CacheControl = "private, max-age=300";
            return PhysicalFile(path, "image/png");
        }

        // =====================================================================
        // QUIZ
        // =====================================================================

        /// <summary>Today's questions. Correct answers are never sent.</summary>
        [HttpGet("quiz")]
        public async Task<IActionResult> GetQuiz()
        {
            var entry = await FindPublishedForDateAsync(_clock.Today);
            if (entry == null) return Fail(404, "Today's publication is not available yet.");

            var key = Me.Key;
            bool attempted = await _context.hemsampark_quiz_submissions
                .AnyAsync(s => s.DailyEntryId == entry.Id && s.UserId == key);

            if (attempted)
                return StatusCode(409, new
                {
                    success = false,
                    alreadySubmitted = true,
                    message = "You have already completed today's quiz."
                });

            var snapshot = HemSamparkSnapshot.Load(entry, await GetMasterAsync());

            if (snapshot.Questions.Count == 0)
                return Fail(404, "No quiz is available for today's publication.");

            return Ok(new
            {
                success = true,
                publicationId = entry.Id,
                title = snapshot.Title,
                questions = snapshot.Questions.Select(q => new
                {
                    id = q.Id,
                    text = q.Text,
                    options = q.Options
                        .Select((text, index) => new { value = index + 1, text })
                        .Where(o => !string.IsNullOrWhiteSpace(o.text))
                })
            });
        }

        [HttpPost("submit-quiz")]
        public async Task<IActionResult> SubmitQuiz([FromBody] HemSamparkSubmitDto model)
        {
            if (model == null || model.DailyEntryId <= 0)
                return Fail(400, "Invalid quiz submission.");

            var key = Me.Key;

            // Only today's published publication can be attempted.
            var entry = await FindPublishedForDateAsync(_clock.Today);

            if (entry == null || entry.Id != model.DailyEntryId)
                return Fail(400, "This quiz is no longer available. Please refresh the page.");

            var master = await GetMasterAsync();
            var snapshot = HemSamparkSnapshot.Load(entry, master);

            if (snapshot.Questions.Count == 0)
                return Fail(400, "No quiz is available for today's publication.");

            var answers = model.Answers ?? new List<HemSamparkAnswerDto>();
            var questions = snapshot.Questions.ToDictionary(q => q.Id);

            if (answers.Count > questions.Count ||
                answers.Select(a => a.QuestionId).Distinct().Count() != answers.Count)
                return Fail(400, "Each question can be answered only once.");

            foreach (var answer in answers)
            {
                if (!questions.TryGetValue(answer.QuestionId, out var question) ||
                    answer.SelectedOption < 1 || answer.SelectedOption > 4 ||
                    string.IsNullOrWhiteSpace(question.Options[answer.SelectedOption - 1]))
                    return Fail(400, "Invalid question or selected option.");
            }

            async Task<IActionResult> AlreadySubmitted()
            {
                var existing = await _context.hemsampark_quiz_submissions.AsNoTracking()
                    .FirstAsync(s => s.DailyEntryId == entry.Id && s.UserId == key);

                return StatusCode(409, new
                {
                    success = false,
                    alreadySubmitted = true,
                    message = "You have already completed today's quiz.",
                    result = await BuildResultAsync(existing, entry, master)
                });
            }

            if (await _context.hemsampark_quiz_submissions
                    .AnyAsync(s => s.DailyEntryId == entry.Id && s.UserId == key))
                return await AlreadySubmitted();

            // Score on the server, against the published answers.
            int correctCount = answers.Count(a => questions[a.QuestionId].Correct == a.SelectedOption);

            var submission = new hemsampark_quiz_submission
            {
                UserId = key,
                DailyEntryId = entry.Id,
                Score = correctCount,
                TotalQuestions = questions.Count,
                SubmittedAt = _clock.Now
            };

            try
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                _context.hemsampark_quiz_submissions.Add(submission);
                await _context.SaveChangesAsync();

                _context.hemsampark_quiz_answers.AddRange(answers.Select(a => new hemsampark_quiz_answer
                {
                    SubmissionId = submission.Id,
                    QuestionId = a.QuestionId,
                    SelectedOption = a.SelectedOption,
                    IsCorrect = questions[a.QuestionId].Correct == a.SelectedOption
                }));

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                _context.ChangeTracker.Clear();

                // A double click / second tab lost the race on the unique
                // (daily_entry_id, user_id) index: report the saved result.
                if (await _context.hemsampark_quiz_submissions
                        .AnyAsync(s => s.DailyEntryId == entry.Id && s.UserId == key))
                    return await AlreadySubmitted();

                _logger.LogError(ex, "HEM Sampark: quiz submission failed for entry {Id}", entry.Id);
                return Fail(500, "Unable to submit the quiz. Please try again.");
            }

            return Ok(new
            {
                success = true,
                message = "Quiz submitted successfully!",
                result = await BuildResultAsync(submission, entry, master)
            });
        }

        // =====================================================================
        // MY PERFORMANCE / HISTORY (always scoped to the logged-in user)
        // =====================================================================

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);

            var key = Me.Key;

            var query =
                from s in _context.hemsampark_quiz_submissions.AsNoTracking()
                join e in _context.dailysamparkentries.AsNoTracking() on s.DailyEntryId equals e.Id
                where s.UserId == key
                select new
                {
                    s.Id,
                    s.Score,
                    s.TotalQuestions,
                    s.SubmittedAt,
                    e.EntryDate,
                    e.Title,
                    e.CelebrationHeadingEn
                };

            int total = await query.CountAsync();

            var rows = await query
                .OrderByDescending(x => x.SubmittedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var ids = rows.Select(r => r.Id).ToList();

            var answered = await _context.hemsampark_quiz_answers
                .Where(a => ids.Contains(a.SubmissionId))
                .GroupBy(a => a.SubmissionId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            int pass = PassPercentage;

            var items = rows.Select(r =>
            {
                int answeredCount = Math.Min(r.TotalQuestions, answered.GetValueOrDefault(r.Id, r.TotalQuestions));
                double percentage = HemSamparkScore.Percentage(r.Score, r.TotalQuestions);

                return new
                {
                    submissionId = r.Id,
                    publicationDate = r.EntryDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                    publicationTitle = !string.IsNullOrWhiteSpace(r.Title) ? r.Title
                        : !string.IsNullOrWhiteSpace(r.CelebrationHeadingEn) ? r.CelebrationHeadingEn
                        : "HEM Sampark",
                    attemptDate = r.SubmittedAt.ToString(DateTimeDisplay, CultureInfo.InvariantCulture),
                    totalQuestions = r.TotalQuestions,
                    correct = r.Score,
                    incorrect = Math.Max(0, answeredCount - r.Score),
                    unanswered = Math.Max(0, r.TotalQuestions - answeredCount),
                    score = $"{r.Score}/{r.TotalQuestions}",
                    percentage,
                    status = HemSamparkScore.Status(percentage, pass),
                    passed = percentage >= pass
                };
            });

            return Ok(new { success = true, total, page, pageSize, items });
        }

        [HttpGet("results/{submissionId:int}")]
        public async Task<IActionResult> GetResult(int submissionId)
        {
            var key = Me.Key;

            // Filtering by the owner makes other users' results look non-existent.
            var submission = await _context.hemsampark_quiz_submissions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == submissionId && s.UserId == key);

            if (submission == null) return Fail(404, "Result not found.");

            var entry = await _context.dailysamparkentries.AsNoTracking()
                .Include(x => x.Questions)
                .FirstOrDefaultAsync(x => x.Id == submission.DailyEntryId);

            if (entry == null) return Fail(404, "Result not found.");

            bool readable = entry.Status == HemSamparkStatus.Published
                            && _pdf.ResolvePdfPath(entry.PublishedPdfUrl) != null;

            return Ok(new
            {
                success = true,
                result = await BuildResultAsync(submission, entry, await GetMasterAsync()),
                pdfUrl = readable
                    ? $"/api/hem-sampark/publications/{entry.Id}/pdf?v={entry.PublishedAt?.Ticks ?? 0}"
                    : null
            });
        }
    }
}
