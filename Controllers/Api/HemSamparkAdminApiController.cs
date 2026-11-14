using HemFoundation.Data;
using HemFoundation.Models;
using HemFoundation.Models.HemSampark;
using HemFoundation.Services.HemSampark;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace HemFoundation.Controllers
{
    /// <summary>
    /// HEM Sampark administration: master settings, publications (draft →
    /// generated → published → archived), their quizzes, and participation
    /// reports. Every endpoint requires HEM Sampark admin rights.
    /// </summary>
    [Route("api/hem-sampark/admin")]
    [ApiController]
    [HemSamparkAdmin]
    public class HemSamparkAdminApiController : ControllerBase
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const string DateTimeDisplay = "dd MMM yyyy, hh:mm tt";
        private const long MaxLogoBytes = 2 * 1024 * 1024;

        private static readonly string[] AllowedLogoExtensions = { ".png", ".jpg", ".jpeg", ".webp" };

        private static readonly string[] HindiDays =
        {
            "रविवार", "सोमवार", "मंगलवार", "बुधवार", "गुरुवार", "शुक्रवार", "शनिवार"
        };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly HemSamparkPdfService _pdf;
        private readonly HemSamparkClock _clock;
        private readonly HemSamparkAccess _access;
        private readonly IConfiguration _config;
        private readonly ILogger<HemSamparkAdminApiController> _logger;

        public HemSamparkAdminApiController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            HemSamparkPdfService pdf,
            HemSamparkClock clock,
            HemSamparkAccess access,
            IConfiguration config,
            ILogger<HemSamparkAdminApiController> logger)
        {
            _context = context;
            _env = env;
            _pdf = pdf;
            _clock = clock;
            _access = access;
            _config = config;
            _logger = logger;
        }

        private int PassPercentage =>
            Math.Clamp(_config.GetValue<int?>("HemSampark:PassPercentage") ?? 50, 0, 100);

        private string CurrentUserKey => _access.Current?.Key ?? "AdminUser";

        private IActionResult Fail(int statusCode, string message) =>
            StatusCode(statusCode, new { success = false, message });

        private static string? Clean(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        private static string? Display(DateTime? value) =>
            value?.ToString(DateTimeDisplay, CultureInfo.InvariantCulture);

        private static string LikePattern(string term) =>
            "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

        private static bool TryParseDate(string? value, out DateTime date) =>
            DateTime.TryParseExact(
                value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        // =====================================================================
        // MASTER SETTINGS  (routes kept from the original module)
        // =====================================================================

        private async Task<string?> SaveLogoFileAsync(IFormFile file)
        {
            string folder = Path.Combine(_env.WebRootPath, "images", "HemSampark");
            Directory.CreateDirectory(folder);

            // Never trust the uploaded name: store under a generated one.
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            string fileName = $"{Guid.NewGuid():N}{extension}";

            await using var stream = new FileStream(Path.Combine(folder, fileName), FileMode.CreateNew);
            await file.CopyToAsync(stream);

            return "/images/HemSampark/" + fileName;
        }

        private static string? ValidateLogo(IFormFile? file, string label)
        {
            if (file == null || file.Length == 0) return null;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedLogoExtensions.Contains(extension))
                return $"{label}: only PNG, JPG or WEBP images are allowed.";

            if (file.Length > MaxLogoBytes)
                return $"{label}: the image must be 2 MB or smaller.";

            return null;
        }

        [HttpGet("~/api/hem-sampark/master")]
        public async Task<IActionResult> GetMasterSettings()
        {
            var settings = await _context.mastersettings.AsNoTracking()
                .OrderBy(x => x.Id).FirstOrDefaultAsync();

            if (settings == null) return Ok(null);

            return Ok(new
            {
                settings.Id,
                settings.Logo1Url,
                settings.Logo2Url,
                settings.Logo3Url,
                settings.MorningMantraEn,
                settings.MorningMantraHi,
                settings.ValuesInFocusEn,
                settings.ValuesInFocusHi,
                settings.FooterText,
                settings.FooterLink,
                updatedAt = Display(settings.UpdatedAt ?? settings.CreatedAt)
            });
        }

        [HttpPost("~/api/hem-sampark/master")]
        public async Task<IActionResult> SaveMasterSettings(
            [FromForm] IFormFile? logo1File,
            [FromForm] IFormFile? logo2File,
            [FromForm] IFormFile? logo3File,
            [FromForm] string? morningMantraEn,
            [FromForm] string? morningMantraHi,
            [FromForm] string? valuesInFocusEn,
            [FromForm] string? valuesInFocusHi,
            [FromForm] string? footerText,
            [FromForm] string? footerLink)
        {
            var logoError = ValidateLogo(logo1File, "RR Global logo")
                            ?? ValidateLogo(logo2File, "HEM Sampark logo")
                            ?? ValidateLogo(logo3File, "Hema Foundation logo");

            if (logoError != null) return Fail(400, logoError);

            var existing = await _context.mastersettings.OrderBy(x => x.Id).FirstOrDefaultAsync();
            bool isNew = existing == null;

            if (existing == null)
            {
                existing = new MasterSetting { CreatedAt = _clock.Now };
                _context.mastersettings.Add(existing);
            }

            string Snapshot() => string.Join('\u001f',
                existing.Logo1Url, existing.Logo2Url, existing.Logo3Url,
                existing.MorningMantraEn, existing.MorningMantraHi,
                existing.ValuesInFocusEn, existing.ValuesInFocusHi,
                existing.FooterText, existing.FooterLink);

            string before = Snapshot();

            if (logo1File is { Length: > 0 }) existing.Logo1Url = await SaveLogoFileAsync(logo1File);
            if (logo2File is { Length: > 0 }) existing.Logo2Url = await SaveLogoFileAsync(logo2File);
            if (logo3File is { Length: > 0 }) existing.Logo3Url = await SaveLogoFileAsync(logo3File);

            existing.MorningMantraEn = Clean(morningMantraEn);
            existing.MorningMantraHi = Clean(morningMantraHi);
            existing.ValuesInFocusEn = Clean(valuesInFocusEn);
            existing.ValuesInFocusHi = Clean(valuesInFocusHi);
            existing.FooterText = Clean(footerText);
            existing.FooterLink = Clean(footerLink);

            // Only a real change marks today's and upcoming PDFs as outdated.
            if (!isNew && before == Snapshot())
                return Ok(new { success = true, message = "No changes to save." });

            if (!isNew) existing.UpdatedAt = _clock.Now;
            existing.CreatedByUserId = CurrentUserKey;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Master settings saved. Regenerate upcoming PDFs to apply them."
            });
        }

        // =====================================================================
        // PUBLICATION STATE HELPERS
        // =====================================================================

        private async Task<DateTime?> GetMasterChangedAtAsync()
        {
            var master = await _context.mastersettings.AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new { x.CreatedAt, x.UpdatedAt })
                .FirstOrDefaultAsync();

            return master == null ? null : master.UpdatedAt ?? master.CreatedAt;
        }

        /// <summary>
        /// The working PDF no longer matches the content: the publication (or,
        /// for today's and upcoming issues, the master data) changed after it
        /// was generated.
        /// </summary>
        private static bool IsPdfOutdated(
            string? pdfUrl, DateTime? generatedAt, DateTime? contentUpdatedAt,
            DateTime entryDate, DateTime? masterChangedAt, DateTime today)
        {
            if (string.IsNullOrEmpty(pdfUrl) || generatedAt == null) return false;
            if (contentUpdatedAt != null && contentUpdatedAt > generatedAt) return true;
            return masterChangedAt != null && masterChangedAt > generatedAt && entryDate >= today;
        }

        private bool IsPdfOutdated(DailySamparkEntry e, DateTime? masterChangedAt) =>
            IsPdfOutdated(e.PdfUrl, e.PdfGeneratedAt, e.ContentUpdatedAt,
                e.EntryDate, masterChangedAt, _clock.Today);

        /// <summary>Everything that feeds the PDF or the quiz, for change detection.</summary>
        private static string Fingerprint(DailySamparkEntry e)
        {
            var sb = new StringBuilder();

            void Add(object? value) => sb.Append(value?.ToString() ?? "").Append('\u001f');

            Add(e.Title); Add(e.EntryDate.ToString(DateFormat)); Add(e.DayHindi);
            Add(e.IssueNumber); Add(e.VikramSamvat);
            Add(e.CelebrationHeadingEn); Add(e.CelebrationContentEn);
            Add(e.CelebrationHeadingHi); Add(e.CelebrationContentHi);
            Add(e.MorningMantraEn); Add(e.MorningMantraHi);
            Add(e.ValuesInFocusEn); Add(e.ValuesInFocusHi);

            foreach (var q in e.Questions.OrderBy(q => q.DisplayOrder).ThenBy(q => q.Id))
            {
                Add(q.Id); Add(q.DisplayOrder); Add(q.IsActive); Add(q.QuestionText);
                Add(q.Option1); Add(q.Option2); Add(q.Option3); Add(q.Option4);
                Add(q.CorrectOption); Add(q.Explanation);
            }

            return sb.ToString();
        }

        private object PdfLinks(DailySamparkEntry e)
        {
            string baseUrl = $"/api/hem-sampark/admin/publications/{e.Id}/pdf";

            return new
            {
                draft = string.IsNullOrEmpty(e.PdfUrl)
                    ? null
                    : $"{baseUrl}?version=draft&v={e.PdfGeneratedAt?.Ticks ?? 0}",
                published = e.Status != HemSamparkStatus.Published || string.IsNullOrEmpty(e.PublishedPdfUrl)
                    ? null
                    : $"{baseUrl}?version=published&v={e.PublishedAt?.Ticks ?? 0}"
            };
        }

        /// <summary>Creates a fresh PDF file for the entry; the old draft file is removed.</summary>
        private async Task GeneratePdfAsync(DailySamparkEntry entry)
        {
            var master = await _context.mastersettings.AsNoTracking()
                .OrderBy(x => x.Id).FirstOrDefaultAsync();

            string? previous = entry.PdfUrl;

            entry.PdfUrl = await _pdf.GenerateAsync(entry, master);
            entry.PdfGeneratedAt = _clock.Now;

            if (entry.Status == HemSamparkStatus.Draft)
                entry.Status = HemSamparkStatus.Generated;

            await _context.SaveChangesAsync();

            // The published file is only ever replaced by an explicit publish.
            if (!string.IsNullOrEmpty(previous) && previous != entry.PublishedPdfUrl)
                _pdf.DeleteGenerated(previous);
        }

        private async Task<object> BuildDetailAsync(DailySamparkEntry e)
        {
            var masterChangedAt = await GetMasterChangedAtAsync();
            bool outdated = IsPdfOutdated(e, masterChangedAt);

            int attempts = await _context.hemsampark_quiz_submissions
                .CountAsync(s => s.DailyEntryId == e.Id);

            return new
            {
                e.Id,
                title = e.Title,
                displayTitle = HemSamparkSnapshot.DisplayTitle(e),
                entryDate = e.EntryDate.ToString(DateFormat),
                e.DayHindi,
                e.IssueNumber,
                e.VikramSamvat,
                e.CelebrationHeadingEn,
                e.CelebrationContentEn,
                e.CelebrationHeadingHi,
                e.CelebrationContentHi,
                e.MorningMantraEn,
                e.MorningMantraHi,
                e.ValuesInFocusEn,
                e.ValuesInFocusHi,
                e.Status,
                hasPdf = !string.IsNullOrEmpty(e.PdfUrl),
                pdfOutdated = outdated,
                hasUnpublishedChanges = e.Status == HemSamparkStatus.Published
                                        && (outdated || e.PdfUrl != e.PublishedPdfUrl),
                pdfGeneratedAt = Display(e.PdfGeneratedAt),
                publishedAt = Display(e.PublishedAt),
                pdf = PdfLinks(e),
                attempts,
                questions = e.Questions
                    .OrderBy(q => q.DisplayOrder).ThenBy(q => q.Id)
                    .Select(q => new
                    {
                        q.Id,
                        q.QuestionText,
                        q.Option1,
                        q.Option2,
                        q.Option3,
                        q.Option4,
                        q.CorrectOption,
                        q.Explanation,
                        q.IsActive
                    })
            };
        }

        // =====================================================================
        // DASHBOARD
        // =====================================================================

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var today = _clock.Today;
            var tomorrow = today.AddDays(1);

            var byStatus = await _context.dailysamparkentries
                .GroupBy(x => x.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            int Count(string status) => byStatus.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

            int scheduled = await _context.dailysamparkentries.CountAsync(x =>
                x.Status == HemSamparkStatus.Published && x.EntryDate >= tomorrow);

            int totalQuestions = await _context.dailyquestions.CountAsync(q =>
                q.IsActive && q.DailySamparkEntry!.Status != HemSamparkStatus.Archived);

            var submissions = _context.hemsampark_quiz_submissions.AsNoTracking();
            int attempts = await submissions.CountAsync();
            int participants = await submissions.Select(s => s.UserId).Distinct().CountAsync();

            double averagePercent = attempts == 0
                ? 0
                : await submissions
                      .Where(s => s.TotalQuestions > 0)
                      .Select(s => (double?)(s.Score * 100.0 / s.TotalQuestions))
                      .AverageAsync() ?? 0;

            var todayEntry = await _context.dailysamparkentries.AsNoTracking()
                .Where(x => x.EntryDate >= today && x.EntryDate < tomorrow
                            && x.Status != HemSamparkStatus.Archived)
                .OrderByDescending(x => x.Status == HemSamparkStatus.Published)
                .FirstOrDefaultAsync();

            object? todayInfo = null;

            if (todayEntry != null)
            {
                var masterChangedAt = await GetMasterChangedAtAsync();
                bool outdated = IsPdfOutdated(todayEntry, masterChangedAt);

                todayInfo = new
                {
                    todayEntry.Id,
                    title = HemSamparkSnapshot.DisplayTitle(todayEntry),
                    todayEntry.Status,
                    hasUnpublishedChanges = todayEntry.Status == HemSamparkStatus.Published
                        && (outdated || todayEntry.PdfUrl != todayEntry.PublishedPdfUrl),
                    attempts = await submissions.CountAsync(s => s.DailyEntryId == todayEntry.Id)
                };
            }

            return Ok(new
            {
                success = true,
                businessDate = today.ToString("dddd, dd MMMM yyyy", CultureInfo.InvariantCulture),
                timeZone = _clock.ZoneId,
                total = byStatus.Sum(x => x.Count),
                draft = Count(HemSamparkStatus.Draft),
                generated = Count(HemSamparkStatus.Generated),
                published = Count(HemSamparkStatus.Published),
                archived = Count(HemSamparkStatus.Archived),
                scheduled,
                totalQuestions,
                attempts,
                participants,
                averagePercent = Math.Round(averagePercent, 1),
                today = todayInfo
            });
        }

        /// <summary>Sensible defaults for the "New publication" form.</summary>
        [HttpGet("publications/defaults")]
        public async Task<IActionResult> GetDefaults()
        {
            var today = _clock.Today;

            var latest = await _context.dailysamparkentries.AsNoTracking()
                .Where(x => x.Status != HemSamparkStatus.Archived)
                .OrderByDescending(x => x.EntryDate)
                .Select(x => new { x.VikramSamvat })
                .FirstOrDefaultAsync();

            int maxIssue = await _context.dailysamparkentries
                .Select(x => (int?)x.IssueNumber).MaxAsync() ?? 0;

            // First date from today onwards that has no publication yet.
            var taken = (await _context.dailysamparkentries.AsNoTracking()
                    .Where(x => x.Status != HemSamparkStatus.Archived && x.EntryDate >= today)
                    .Select(x => x.EntryDate)
                    .ToListAsync())
                .Select(d => d.Date)
                .ToHashSet();

            var nextDate = today;
            while (taken.Contains(nextDate)) nextDate = nextDate.AddDays(1);

            return Ok(new
            {
                success = true,
                entryDate = nextDate.ToString(DateFormat),
                today = today.ToString(DateFormat),
                dayHindi = HindiDays[(int)nextDate.DayOfWeek],
                issueNumber = maxIssue + 1,
                vikramSamvat = latest?.VikramSamvat ?? ""
            });
        }

        // =====================================================================
        // PUBLICATION LISTING
        // =====================================================================

        [HttpGet("publications")]
        public async Task<IActionResult> GetPublications(
            [FromQuery] string? status,
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);

            var today = _clock.Today;
            var tomorrow = today.AddDays(1);
            var query = _context.dailysamparkentries.AsNoTracking().AsQueryable();

            if (string.Equals(status, "Scheduled", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    x.Status == HemSamparkStatus.Published && x.EntryDate >= tomorrow);
            }
            else if (!string.IsNullOrWhiteSpace(status))
            {
                var match = HemSamparkStatus.All.FirstOrDefault(s =>
                    s.Equals(status, StringComparison.OrdinalIgnoreCase));

                if (match == null) return Fail(400, "Unknown status filter.");
                query = query.Where(x => x.Status == match);
            }
            else
            {
                // Archived publications only show when asked for.
                query = query.Where(x => x.Status != HemSamparkStatus.Archived);
            }

            if (TryParseDate(from, out var fromDate))
                query = query.Where(x => x.EntryDate >= fromDate);

            if (TryParseDate(to, out var toDate))
            {
                var end = toDate.AddDays(1);
                query = query.Where(x => x.EntryDate < end);
            }

            search = search?.Trim();
            if (!string.IsNullOrEmpty(search))
            {
                var pattern = LikePattern(search);
                bool isNumber = int.TryParse(search, out var number);

                query = query.Where(x =>
                    EF.Functions.ILike(x.Title ?? "", pattern, "\\") ||
                    EF.Functions.ILike(x.CelebrationHeadingEn ?? "", pattern, "\\") ||
                    EF.Functions.ILike(x.CelebrationHeadingHi ?? "", pattern, "\\") ||
                    (isNumber && x.IssueNumber == number));
            }

            int total = await query.CountAsync();

            var rows = await query
                .OrderByDescending(x => x.EntryDate)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.CelebrationHeadingEn,
                    x.EntryDate,
                    x.DayHindi,
                    x.IssueNumber,
                    x.Status,
                    x.PdfUrl,
                    x.PublishedPdfUrl,
                    x.PdfGeneratedAt,
                    x.PublishedAt,
                    x.ContentUpdatedAt,
                    Questions = x.Questions.Count(q => q.IsActive)
                })
                .ToListAsync();

            var ids = rows.Select(r => r.Id).ToList();

            var attempts = await _context.hemsampark_quiz_submissions
                .Where(s => ids.Contains(s.DailyEntryId))
                .GroupBy(s => s.DailyEntryId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var masterChangedAt = await GetMasterChangedAtAsync();

            var items = rows.Select(r =>
            {
                bool outdated = IsPdfOutdated(r.PdfUrl, r.PdfGeneratedAt, r.ContentUpdatedAt,
                    r.EntryDate, masterChangedAt, today);

                bool published = r.Status == HemSamparkStatus.Published;

                return new
                {
                    r.Id,
                    title = !string.IsNullOrWhiteSpace(r.Title) ? r.Title
                        : !string.IsNullOrWhiteSpace(r.CelebrationHeadingEn) ? r.CelebrationHeadingEn
                        : $"HEM Sampark – {r.EntryDate:dd MMM yyyy}",
                    entryDate = r.EntryDate.ToString(DateFormat),
                    entryDateDisplay = r.EntryDate.ToString("ddd, dd MMM yyyy", CultureInfo.InvariantCulture),
                    r.DayHindi,
                    r.IssueNumber,
                    r.Status,
                    isToday = r.EntryDate.Date == today,
                    isScheduled = published && r.EntryDate.Date > today,
                    hasPdf = !string.IsNullOrEmpty(r.PdfUrl),
                    pdfOutdated = outdated,
                    hasUnpublishedChanges = published && (outdated || r.PdfUrl != r.PublishedPdfUrl),
                    pdfGeneratedAt = Display(r.PdfGeneratedAt),
                    publishedAt = Display(r.PublishedAt),
                    questions = r.Questions,
                    attempts = attempts.GetValueOrDefault(r.Id)
                };
            });

            return Ok(new { success = true, total, page, pageSize, items });
        }

        [HttpGet("publications/{id:int}")]
        public async Task<IActionResult> GetPublication(int id)
        {
            var entry = await _context.dailysamparkentries.AsNoTracking()
                .Include(x => x.Questions)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entry == null) return Fail(404, "Publication not found.");

            return Ok(new { success = true, publication = await BuildDetailAsync(entry) });
        }

        // =====================================================================
        // CREATE / EDIT (content + quiz in one save)
        // =====================================================================

        private static string? ValidateQuestions(List<HemSamparkQuestionSaveDto> questions)
        {
            if (questions.Count > 50) return "A publication can have at most 50 questions.";

            for (int i = 0; i < questions.Count; i++)
            {
                var q = questions[i];
                string label = $"Question {i + 1}";

                if (string.IsNullOrWhiteSpace(q.QuestionText))
                    return $"{label}: the question text is required.";

                var options = new[] { q.Option1, q.Option2, q.Option3, q.Option4 };
                int filled = options.TakeWhile(o => !string.IsNullOrWhiteSpace(o)).Count();

                if (filled < 2)
                    return $"{label}: enter at least two options (Option A and Option B).";

                if (options.Skip(filled).Any(o => !string.IsNullOrWhiteSpace(o)))
                    return $"{label}: fill the options in order without leaving a gap.";

                if (q.CorrectOption < 1 || q.CorrectOption > filled)
                    return $"{label}: choose the correct answer from the options you entered.";
            }

            return null;
        }

        [HttpPost("publications")]
        public async Task<IActionResult> SavePublication([FromBody] HemSamparkPublicationSaveDto model)
        {
            if (model == null) return Fail(400, "Invalid request.");

            if (!TryParseDate(model.EntryDate, out var entryDate))
                return Fail(400, "Please choose a valid publication date.");

            if (string.IsNullOrWhiteSpace(model.CelebrationHeadingEn))
                return Fail(400, "The English heading is required.");

            if (string.IsNullOrWhiteSpace(model.CelebrationContentEn))
                return Fail(400, "The English content is required.");

            if (model.IssueNumber < 0)
                return Fail(400, "The issue number cannot be negative.");

            if ((model.Title?.Trim().Length ?? 0) > 250)
                return Fail(400, "The title must be 250 characters or fewer.");

            model.Questions ??= new();
            var questionError = ValidateQuestions(model.Questions);
            if (questionError != null) return Fail(400, questionError);

            var now = _clock.Now;
            DailySamparkEntry entry;
            bool isNew = model.Id <= 0;

            if (isNew)
            {
                entry = new DailySamparkEntry
                {
                    Status = HemSamparkStatus.Draft,
                    CreatedAt = now,
                    CreatedByUserId = CurrentUserKey
                };
            }
            else
            {
                var found = await _context.dailysamparkentries
                    .Include(x => x.Questions)
                    .FirstOrDefaultAsync(x => x.Id == model.Id);

                if (found == null) return Fail(404, "Publication not found.");
                entry = found;

                if (entry.Status == HemSamparkStatus.Archived)
                    return Fail(400, "This publication is archived. Restore it before editing.");

            }

            // One publication per date (archived ones do not count).
            var dayEnd = entryDate.AddDays(1);
            var clash = await _context.dailysamparkentries.AsNoTracking()
                .Where(x => x.Id != model.Id &&
                            x.Status != HemSamparkStatus.Archived &&
                            x.EntryDate >= entryDate && x.EntryDate < dayEnd)
                .Select(x => new { x.Title, x.CelebrationHeadingEn, x.Status })
                .FirstOrDefaultAsync();

            if (clash != null)
            {
                string clashTitle = !string.IsNullOrWhiteSpace(clash.Title) ? clash.Title!
                    : clash.CelebrationHeadingEn ?? "another publication";

                return StatusCode(409, new
                {
                    success = false,
                    dateConflict = true,
                    message = $"{entryDate:dd MMM yyyy} already has a publication: \"{clashTitle}\" " +
                              $"({clash.Status}). Only one publication is allowed per date. " +
                              "Choose another date, or archive or re-date that publication first."
                });
            }

            // ---- Moving an existing publication to another date ----
            // The record (and so its quiz questions and every saved quiz
            // result) stays the same; only its date changes. A published
            // publication is taken offline first, so users never see it under
            // a date its PDF was not generated and approved for.
            DateTime? previousDate = null;
            string? dateNotice = null;

            if (!isNew && entry.EntryDate.Date != entryDate)
            {
                previousDate = entry.EntryDate.Date;
                bool wasPublished = entry.Status == HemSamparkStatus.Published;

                int attempts = await _context.hemsampark_quiz_submissions
                    .CountAsync(s => s.DailyEntryId == entry.Id);

                if ((wasPublished || attempts > 0) && !model.ConfirmDateChange)
                {
                    var parts = new List<string>();

                    if (wasPublished)
                        parts.Add("This publication is published. Changing its date will unpublish it; " +
                                  "users will not see it until you regenerate the PDF and publish it again.");

                    if (attempts > 0)
                        parts.Add($"{attempts} user(s) already took its quiz. Their results are kept and stay " +
                                  "linked to this publication (shown under the new date), and they cannot take it again.");

                    return StatusCode(409, new
                    {
                        success = false,
                        requiresDateConfirmation = true,
                        isPublished = wasPublished,
                        attempts,
                        fromDate = previousDate.Value.ToString("dd MMM yyyy"),
                        toDate = entryDate.ToString("dd MMM yyyy"),
                        message = string.Join(" ", parts)
                    });
                }

                if (wasPublished)
                {
                    WithdrawFromUsers(entry);
                    entry.Status = string.IsNullOrEmpty(entry.PdfUrl)
                        ? HemSamparkStatus.Draft
                        : HemSamparkStatus.Generated;
                    entry.PublishedAt = null;

                    dateNotice = "The publication was unpublished because its date changed. " +
                                 "Review the regenerated PDF and publish it again.";
                }
            }

            string before = isNew ? "" : Fingerprint(entry);

            entry.Title = Clean(model.Title);
            entry.EntryDate = entryDate;
            var dayHindi = Clean(model.DayHindi);
            if (previousDate != null && dayHindi == HindiDays[(int)previousDate.Value.DayOfWeek])
                dayHindi = null;   // it was the old date's weekday: follow the new date
            entry.DayHindi = dayHindi ?? HindiDays[(int)entryDate.DayOfWeek];
            entry.IssueNumber = model.IssueNumber;
            entry.VikramSamvat = Clean(model.VikramSamvat);
            entry.CelebrationHeadingEn = Clean(model.CelebrationHeadingEn);
            entry.CelebrationContentEn = Clean(model.CelebrationContentEn);
            entry.CelebrationHeadingHi = Clean(model.CelebrationHeadingHi);
            entry.CelebrationContentHi = Clean(model.CelebrationContentHi);
            entry.MorningMantraEn = Clean(model.MorningMantraEn);
            entry.MorningMantraHi = Clean(model.MorningMantraHi);
            entry.ValuesInFocusEn = Clean(model.ValuesInFocusEn);
            entry.ValuesInFocusHi = Clean(model.ValuesInFocusHi);

            // ---- Quiz: update in place, add new, remove what was dropped ----
            var existing = entry.Questions.ToDictionary(q => q.Id);
            var keptIds = new HashSet<int>();
            string? notice = null;

            for (int i = 0; i < model.Questions.Count; i++)
            {
                var dto = model.Questions[i];
                DailyQuestion question;

                if (dto.Id > 0)
                {
                    if (!existing.TryGetValue(dto.Id, out question!))
                        return Fail(400, $"Question {i + 1} does not belong to this publication.");

                    keptIds.Add(dto.Id);
                }
                else
                {
                    question = new DailyQuestion { CreatedAt = now, CreatedByUserId = CurrentUserKey };
                    entry.Questions.Add(question);
                }

                question.QuestionText = Clean(dto.QuestionText);
                question.Option1 = Clean(dto.Option1);
                question.Option2 = Clean(dto.Option2);
                question.Option3 = Clean(dto.Option3);
                question.Option4 = Clean(dto.Option4);
                question.CorrectOption = dto.CorrectOption;
                question.Explanation = Clean(dto.Explanation);
                question.IsActive = dto.IsActive;
                question.DisplayOrder = i + 1;
            }

            var removed = existing.Values.Where(q => !keptIds.Contains(q.Id)).ToList();

            if (removed.Count > 0)
            {
                var removedIds = removed.Select(q => q.Id).ToList();

                // Questions users already answered are kept (disabled) so their
                // saved results stay intact.
                var answered = await _context.hemsampark_quiz_answers
                    .Where(a => removedIds.Contains(a.QuestionId))
                    .Select(a => a.QuestionId)
                    .Distinct()
                    .ToListAsync();

                int order = model.Questions.Count;

                foreach (var question in removed)
                {
                    if (answered.Contains(question.Id))
                    {
                        question.IsActive = false;
                        question.DisplayOrder = ++order;
                    }
                    else
                    {
                        entry.Questions.Remove(question);
                        _context.dailyquestions.Remove(question);
                    }
                }

                if (answered.Count > 0)
                    notice = $"{answered.Count} question(s) already answered by users were " +
                             "disabled instead of deleted, to keep their results.";
            }

            bool changed = isNew || before != Fingerprint(entry);

            if (changed)
            {
                entry.ContentUpdatedAt = now;
                if (!isNew) entry.UpdatedAt = now;
            }

            if (isNew) _context.dailysamparkentries.Add(entry);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "HEM Sampark: saving publication {Id} failed", model.Id);
                return Fail(500, "The publication could not be saved. Please try again.");
            }

            // ---- PDF: regenerate so the draft PDF always matches the content ----
            string? pdfError = null;
            bool wantsPdf = model.GeneratePdf && (changed || string.IsNullOrEmpty(entry.PdfUrl)
                                                  || IsPdfOutdated(entry, await GetMasterChangedAtAsync()));

            if (wantsPdf)
            {
                try
                {
                    await GeneratePdfAsync(entry);
                }
                catch (HemSamparkPdfLayoutException ex)
                {
                    pdfError = "Saved, but the PDF was not generated. " + ex.Message;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "HEM Sampark: PDF generation failed for {Id}", entry.Id);
                    pdfError = "Saved, but the PDF could not be generated. Use Regenerate PDF to retry.";
                }
            }

            string message = pdfError
                ?? (isNew ? "Publication created" + (wantsPdf ? " and PDF generated." : " as a draft.")
                    : changed ? "Changes saved" + (wantsPdf ? " and PDF regenerated." : ".")
                    : wantsPdf ? "PDF regenerated."
                    : "No changes to save.");

            if (pdfError == null && entry.Status == HemSamparkStatus.Published && changed)
                message += " Users still see the previously published version until you republish.";

            if (dateNotice != null) notice = notice == null ? dateNotice : dateNotice + " " + notice;

            return Ok(new
            {
                success = true,
                pdfFailed = pdfError != null,
                message,
                notice,
                publication = await BuildDetailAsync(entry)
            });
        }

        // =====================================================================
        // WORKFLOW ACTIONS
        // =====================================================================

        private Task<DailySamparkEntry?> FindWithQuestionsAsync(int id) =>
            _context.dailysamparkentries
                .Include(x => x.Questions)
                .FirstOrDefaultAsync(x => x.Id == id);

        [HttpPost("publications/{id:int}/generate")]
        public async Task<IActionResult> Generate(int id)
        {
            var entry = await FindWithQuestionsAsync(id);
            if (entry == null) return Fail(404, "Publication not found.");

            if (entry.Status == HemSamparkStatus.Archived)
                return Fail(400, "This publication is archived. Restore it first.");

            try
            {
                await GeneratePdfAsync(entry);
            }
            catch (HemSamparkPdfLayoutException ex)
            {
                return Fail(400, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HEM Sampark: PDF generation failed for {Id}", id);
                return Fail(500, "The PDF could not be generated. Please try again.");
            }

            return Ok(new
            {
                success = true,
                message = entry.Status == HemSamparkStatus.Published
                    ? "PDF regenerated. Review it and click Republish to make it live."
                    : "PDF generated. Review it and publish when ready.",
                publication = await BuildDetailAsync(entry)
            });
        }

        [HttpPost("publications/{id:int}/publish")]
        public async Task<IActionResult> Publish(int id)
        {
            var entry = await FindWithQuestionsAsync(id);
            if (entry == null) return Fail(404, "Publication not found.");

            if (entry.Status == HemSamparkStatus.Archived)
                return Fail(400, "This publication is archived. Restore it first.");

            if (_pdf.ResolvePdfPath(entry.PdfUrl) == null)
                return Fail(400, "Generate the PDF before publishing.");

            if (IsPdfOutdated(entry, await GetMasterChangedAtAsync()))
                return Fail(400,
                    "The content changed after the PDF was generated. Regenerate the PDF, review it, then publish.");

            var day = entry.EntryDate.Date;
            var dayEnd = day.AddDays(1);

            bool otherPublished = await _context.dailysamparkentries.AnyAsync(x =>
                x.Id != entry.Id && x.Status == HemSamparkStatus.Published &&
                x.EntryDate >= day && x.EntryDate < dayEnd);

            if (otherPublished)
                return Fail(409, $"Another publication is already published for {day:dd MMM yyyy}.");

            var master = await _context.mastersettings.AsNoTracking()
                .OrderBy(x => x.Id).FirstOrDefaultAsync();

            bool republish = entry.Status == HemSamparkStatus.Published;
            string? previousPublished = entry.PublishedPdfUrl;

            entry.PublishedPdfUrl = entry.PdfUrl;
            entry.PublishedSnapshot = HemSamparkSnapshot.FromEntry(entry, master).ToJson();
            entry.PublishedAt = _clock.Now;
            entry.Status = HemSamparkStatus.Published;

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(previousPublished) && previousPublished != entry.PublishedPdfUrl)
                _pdf.DeleteGenerated(previousPublished);

            string when = day == _clock.Today
                ? "It is live for users now."
                : day > _clock.Today
                    ? $"Users will see it on {day:dd MMM yyyy}."
                    : "It is available in past issues.";

            return Ok(new
            {
                success = true,
                message = (republish ? "Republished. " : "Published. ") + when,
                publication = await BuildDetailAsync(entry)
            });
        }

        [HttpPost("publications/{id:int}/unpublish")]
        public async Task<IActionResult> Unpublish(int id)
        {
            var entry = await FindWithQuestionsAsync(id);
            if (entry == null) return Fail(404, "Publication not found.");

            if (entry.Status != HemSamparkStatus.Published)
                return Fail(400, "This publication is not published.");

            WithdrawFromUsers(entry);
            entry.Status = string.IsNullOrEmpty(entry.PdfUrl)
                ? HemSamparkStatus.Draft
                : HemSamparkStatus.Generated;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Unpublished. Users can no longer see this publication.",
                publication = await BuildDetailAsync(entry)
            });
        }

        [HttpPost("publications/{id:int}/archive")]
        public async Task<IActionResult> Archive(int id)
        {
            var entry = await FindWithQuestionsAsync(id);
            if (entry == null) return Fail(404, "Publication not found.");

            if (entry.Status == HemSamparkStatus.Archived)
                return Fail(400, "This publication is already archived.");

            WithdrawFromUsers(entry);
            entry.Status = HemSamparkStatus.Archived;
            entry.UpdatedAt = _clock.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Archived. Quiz results already recorded are kept.",
                publication = await BuildDetailAsync(entry)
            });
        }

        [HttpPost("publications/{id:int}/restore")]
        public async Task<IActionResult> Restore(int id)
        {
            var entry = await FindWithQuestionsAsync(id);
            if (entry == null) return Fail(404, "Publication not found.");

            if (entry.Status != HemSamparkStatus.Archived)
                return Fail(400, "Only archived publications can be restored.");

            var day = entry.EntryDate.Date;
            var dayEnd = day.AddDays(1);

            bool dateTaken = await _context.dailysamparkentries.AnyAsync(x =>
                x.Id != entry.Id && x.Status != HemSamparkStatus.Archived &&
                x.EntryDate >= day && x.EntryDate < dayEnd);

            if (dateTaken)
                return Fail(409, $"Another publication now exists for {day:dd MMM yyyy}.");

            entry.Status = _pdf.ResolvePdfPath(entry.PdfUrl) != null
                ? HemSamparkStatus.Generated
                : HemSamparkStatus.Draft;
            entry.UpdatedAt = _clock.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Restored as a draft. Publish it again to make it visible to users.",
                publication = await BuildDetailAsync(entry)
            });
        }

        /// <summary>Drops the published copy; the working PDF is kept.</summary>
        private void WithdrawFromUsers(DailySamparkEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.PublishedPdfUrl) && entry.PublishedPdfUrl != entry.PdfUrl)
                _pdf.DeleteGenerated(entry.PublishedPdfUrl);

            entry.PublishedPdfUrl = null;
            entry.PublishedSnapshot = null;
        }

        // =====================================================================
        // PDF PREVIEW / DOWNLOAD (admin)
        // =====================================================================

        [HttpGet("publications/{id:int}/pdf")]
        public async Task<IActionResult> GetPdf(
            int id, [FromQuery] string? version, [FromQuery] bool download = false)
        {
            var entry = await _context.dailysamparkentries.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.EntryDate, x.PdfUrl, x.PublishedPdfUrl })
                .FirstOrDefaultAsync();

            if (entry == null) return NotFound();

            bool published = string.Equals(version, "published", StringComparison.OrdinalIgnoreCase);
            var path = _pdf.ResolvePdfPath(published ? entry.PublishedPdfUrl : entry.PdfUrl);

            if (path == null) return NotFound();

            Response.Headers.CacheControl = "private, no-store";

            return download
                ? PhysicalFile(path, "application/pdf",
                    $"HEM-Sampark-{entry.EntryDate:yyyy-MM-dd}{(published ? "" : "-draft")}.pdf")
                : PhysicalFile(path, "application/pdf");
        }

        /// <summary>Page image of a generated PDF, for screens that cannot show PDFs inline.</summary>
        [HttpGet("publications/{id:int}/image")]
        public async Task<IActionResult> GetImage(int id, [FromQuery] string? version)
        {
            var entry = await _context.dailysamparkentries.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.PdfUrl, x.PublishedPdfUrl })
                .FirstOrDefaultAsync();

            if (entry == null) return NotFound();

            bool published = string.Equals(version, "published", StringComparison.OrdinalIgnoreCase);
            var path = _pdf.ResolvePreviewImagePath(published ? entry.PublishedPdfUrl : entry.PdfUrl);

            if (path == null) return NotFound();

            Response.Headers.CacheControl = "private, no-store";
            return PhysicalFile(path, "image/png");
        }

        // =====================================================================
        // REPORTS: user participation and performance
        // =====================================================================

        private class AttemptRow
        {
            public int SubmissionId { get; set; }
            public string UserKey { get; set; } = "";
            public DateTime EntryDate { get; set; }
            public string? Title { get; set; }
            public string? Heading { get; set; }
            public int Score { get; set; }
            public int Total { get; set; }
            public DateTime SubmittedAt { get; set; }
        }

        private async Task<IQueryable<AttemptRow>> BuildAttemptQueryAsync(
            string? from, string? to, string? search, int? publicationId)
        {
            var query =
                from s in _context.hemsampark_quiz_submissions.AsNoTracking()
                join e in _context.dailysamparkentries.AsNoTracking() on s.DailyEntryId equals e.Id
                select new { s, e };

            if (publicationId is > 0)
                query = query.Where(x => x.e.Id == publicationId);

            if (TryParseDate(from, out var fromDate))
                query = query.Where(x => x.e.EntryDate >= fromDate);

            if (TryParseDate(to, out var toDate))
            {
                var end = toDate.AddDays(1);
                query = query.Where(x => x.e.EntryDate < end);
            }

            search = search?.Trim();
            if (!string.IsNullOrEmpty(search))
            {
                var pattern = LikePattern(search);

                // Match on the user's name, mobile, code or id.
                var userKeys = await _context.Users.AsNoTracking()
                    .Where(u =>
                        EF.Functions.ILike((u.FirstName ?? "") + " " + (u.LastName ?? ""), pattern, "\\") ||
                        EF.Functions.ILike(u.MobileNo ?? "", pattern, "\\") ||
                        EF.Functions.ILike(u.UserCode ?? "", pattern, "\\"))
                    .OrderBy(u => u.UserID)
                    .Select(u => u.UserID)
                    .Take(2000)
                    .ToListAsync();

                var keys = userKeys.Select(k => k.ToString()).ToList();
                keys.Add(search);

                query = query.Where(x => keys.Contains(x.s.UserId));
            }

            return query.Select(x => new AttemptRow
            {
                SubmissionId = x.s.Id,
                UserKey = x.s.UserId,
                EntryDate = x.e.EntryDate,
                Title = x.e.Title,
                Heading = x.e.CelebrationHeadingEn,
                Score = x.s.Score,
                Total = x.s.TotalQuestions,
                SubmittedAt = x.s.SubmittedAt
            });
        }

        private record UserInfo(string Name, string? Code, string? Mobile);

        private async Task<Dictionary<string, UserInfo>> LoadUsersAsync(IEnumerable<string> keys)
        {
            var result = new Dictionary<string, UserInfo>();
            var distinct = keys.Distinct().ToList();

            var userIds = distinct
                .Select(k => int.TryParse(k, out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();

            if (userIds.Count > 0)
            {
                var users = await _context.Users.AsNoTracking()
                    .Where(u => userIds.Contains(u.UserID))
                    .Select(u => new { u.UserID, u.FirstName, u.LastName, u.UserCode, u.MobileNo })
                    .ToListAsync();

                foreach (var u in users)
                {
                    var name = $"{u.FirstName} {u.LastName}".Trim();
                    result[u.UserID.ToString()] = new UserInfo(
                        name.Length > 0 ? name : $"User {u.UserID}", u.UserCode, u.MobileNo);
                }
            }

            var adminIds = distinct
                .Where(k => k.StartsWith("admin:", StringComparison.Ordinal))
                .Select(k => long.TryParse(k.AsSpan(6), out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();

            if (adminIds.Count > 0)
            {
                var admins = await _context.Admins.AsNoTracking()
                    .Where(a => adminIds.Contains(a.Id))
                    .Select(a => new { a.Id, a.FirstName, a.LastName, a.MobileNo })
                    .ToListAsync();

                foreach (var a in admins)
                {
                    result[$"admin:{a.Id}"] = new UserInfo(
                        $"{a.FirstName} {a.LastName}".Trim() + " (Admin)", null, a.MobileNo);
                }
            }

            return result;
        }

        private async Task<List<object[]>> BuildReportRowsAsync(List<AttemptRow> rows)
        {
            var submissionIds = rows.Select(r => r.SubmissionId).ToList();
            var userKeys = rows.Select(r => r.UserKey).Distinct().ToList();

            var answered = await _context.hemsampark_quiz_answers
                .Where(a => submissionIds.Contains(a.SubmissionId))
                .GroupBy(a => a.SubmissionId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var totalAttempts = await _context.hemsampark_quiz_submissions
                .Where(s => userKeys.Contains(s.UserId))
                .GroupBy(s => s.UserId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var users = await LoadUsersAsync(userKeys);
            int pass = PassPercentage;

            return rows.Select(r =>
            {
                users.TryGetValue(r.UserKey, out var user);

                int answeredCount = Math.Min(r.Total, answered.GetValueOrDefault(r.SubmissionId, r.Total));
                int incorrect = Math.Max(0, answeredCount - r.Score);
                double percentage = HemSamparkScore.Percentage(r.Score, r.Total);

                string title = !string.IsNullOrWhiteSpace(r.Title) ? r.Title!
                    : !string.IsNullOrWhiteSpace(r.Heading) ? r.Heading!
                    : "HEM Sampark";

                return new object[]
                {
                    user?.Name ?? "Unknown user",                           // 0
                    r.UserKey,                                              // 1
                    user?.Code ?? "",                                       // 2
                    user?.Mobile ?? "",                                     // 3
                    r.EntryDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), // 4
                    title,                                                  // 5
                    totalAttempts.GetValueOrDefault(r.UserKey, 1),          // 6
                    r.Score,                                                // 7
                    incorrect,                                              // 8
                    Math.Max(0, r.Total - answeredCount),                   // 9
                    $"{r.Score}/{r.Total}",                                 // 10
                    percentage,                                             // 11
                    HemSamparkScore.Status(percentage, pass),               // 12
                    r.SubmittedAt.ToString(DateTimeDisplay, CultureInfo.InvariantCulture) // 13
                };
            }).ToList();
        }

        [HttpGet("reports/attempts")]
        public async Task<IActionResult> GetAttempts(
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] string? search,
            [FromQuery] int? publicationId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);

            var query = await BuildAttemptQueryAsync(from, to, search, publicationId);

            int total = await query.CountAsync();
            int participants = await query.Select(x => x.UserKey).Distinct().CountAsync();

            double averagePercent = total == 0
                ? 0
                : await query.Where(x => x.Total > 0)
                      .Select(x => (double?)(x.Score * 100.0 / x.Total))
                      .AverageAsync() ?? 0;

            var rows = await query
                .OrderByDescending(x => x.SubmittedAt)
                .ThenByDescending(x => x.SubmissionId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = (await BuildReportRowsAsync(rows)).Select(r => new
            {
                userName = r[0],
                userId = r[1],
                userCode = r[2],
                mobile = r[3],
                publicationDate = r[4],
                publicationTitle = r[5],
                attempts = r[6],
                correct = r[7],
                incorrect = r[8],
                unanswered = r[9],
                score = r[10],
                percentage = r[11],
                status = r[12],
                completedAt = r[13]
            });

            return Ok(new
            {
                success = true,
                total,
                page,
                pageSize,
                participants,
                averagePercent = Math.Round(averagePercent, 1),
                items
            });
        }

        [HttpGet("reports/attempts/export")]
        public async Task<IActionResult> ExportAttempts(
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] string? search,
            [FromQuery] int? publicationId)
        {
            const int maxRows = 20000;

            var query = await BuildAttemptQueryAsync(from, to, search, publicationId);

            var rows = await query
                .OrderByDescending(x => x.SubmittedAt)
                .Take(maxRows)
                .ToListAsync();

            // Keeps spreadsheet apps from treating user-entered text as a formula.
            static string Cell(object? value)
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                if (value is string && text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }

            var csv = new StringBuilder();
            csv.AppendLine(
                "User Name,User ID,User Code,Mobile,Publication Date,Publication Title,Quiz Attempts," +
                "Correct Answers,Incorrect Answers,Unanswered,Total Score,Percentage,Result,Completion Date");

            foreach (var row in await BuildReportRowsAsync(rows))
            {
                // "3/4" would be read as a date; write it as text.
                row[10] = " " + row[10];
                csv.AppendLine(string.Join(",", row.Select(Cell)));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

            return File(bytes, "text/csv",
                $"HEM-Sampark-Quiz-Report-{_clock.Now:yyyyMMdd-HHmm}.csv");
        }
    }
}
