using HemFoundation.Data;
using HemFoundation.Models.HemSampark;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Security.Claims;

namespace HemFoundation.Controllers
{
    [Route("api/hem-sampark")]
    [ApiController]
    public class HemSamparkApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public HemSamparkApiController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        private async Task<string> SaveLogoFileAsync(IFormFile file, string existingUrl)
        {
            if (file == null || file.Length == 0) return existingUrl;

            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "HemSampark");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return "/images/HemSampark/" + uniqueFileName;
        }

        [HttpPost("master")]
        public async Task<IActionResult> SaveMasterSettings(
            [FromForm] IFormFile? logo1File,
            [FromForm] IFormFile? logo2File,
            [FromForm] IFormFile? logo3File,
            [FromForm] string morningMantraEn,
            [FromForm] string morningMantraHi,
            [FromForm] string valuesInFocusEn,
            [FromForm] string valuesInFocusHi,
            [FromForm] string footerText,
            [FromForm] string footerLink)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "AdminUser";
            var existing = await _context.mastersettings.FirstOrDefaultAsync();

            string logo1Url = existing?.Logo1Url;
            string logo2Url = existing?.Logo2Url;
            string logo3Url = existing?.Logo3Url;

            if (logo1File != null) logo1Url = await SaveLogoFileAsync(logo1File, logo1Url);
            if (logo2File != null) logo2Url = await SaveLogoFileAsync(logo2File, logo2Url);
            if (logo3File != null) logo3Url = await SaveLogoFileAsync(logo3File, logo3Url);

            if (existing == null)
            {
                var newMaster = new MasterSetting
                {
                    Logo1Url = logo1Url,
                    Logo2Url = logo2Url,
                    Logo3Url = logo3Url,
                    MorningMantraEn = morningMantraEn,
                    MorningMantraHi = morningMantraHi,
                    ValuesInFocusEn = valuesInFocusEn,
                    ValuesInFocusHi = valuesInFocusHi,
                    FooterText = footerText,
                    FooterLink = footerLink,
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.Now
                };
                _context.mastersettings.Add(newMaster);
            }
            else
            {
                existing.Logo1Url = logo1Url;
                existing.Logo2Url = logo2Url;
                existing.Logo3Url = logo3Url;
                existing.MorningMantraEn = morningMantraEn;
                existing.MorningMantraHi = morningMantraHi;
                existing.ValuesInFocusEn = valuesInFocusEn;
                existing.ValuesInFocusHi = valuesInFocusHi;
                existing.FooterText = footerText;
                existing.FooterLink = footerLink;
                existing.UpdatedAt = DateTime.Now;
                existing.CreatedByUserId = userId;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Master settings and logos saved successfully!" });
        }

        [HttpGet("master")]
        public async Task<IActionResult> GetMasterSettings()
        {
            var settings = await _context.mastersettings.FirstOrDefaultAsync();
            return Ok(settings);
        }

        [HttpPost("daily-entry")]
        public async Task<IActionResult> SaveDailyEntry([FromBody] DailySamparkEntry model)
        {
            ModelState.Remove("CreatedByUserId");
            if (model.Questions != null)
            {
                for (int i = 0; i < model.Questions.Count; i++)
                {
                    ModelState.Remove($"Questions[{i}].CreatedByUserId");
                }
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "AdminUser";
            model.CreatedByUserId = userId;
            model.CreatedAt = DateTime.Now;

            if (model.Questions != null)
            {
                foreach (var q in model.Questions)
                {
                    q.CreatedByUserId = userId;
                    q.CreatedAt = DateTime.Now;
                }
            }

            _context.dailysamparkentries.Add(model);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Daily entry and questions saved successfully!" });
        }

        [HttpGet("entries-list")]
        public async Task<IActionResult> GetEntriesList()
        {
            var list = await _context.dailysamparkentries
                .OrderByDescending(x => x.EntryDate)
                .Select(x => new {
                    x.Id,
                    EntryDate = x.EntryDate.ToString("yyyy-MM-dd"),
                    x.DayHindi,
                    x.IssueNumber,
                    x.CelebrationHeadingEn,
                    x.PdfUrl,
                    x.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpGet("preview/{id}/{regenerate?}")]
        public async Task<IActionResult> GetPreviewData(int id, bool regenerate = false)
        {
            var entry = await _context.dailysamparkentries
                .Include(x => x.Questions)
                .FirstOrDefaultAsync(x => x.Id == id);

            var master = await _context.mastersettings.FirstOrDefaultAsync();

            if (entry == null) return NotFound(new { success = false, message = "Entry not found" });

            if (string.IsNullOrEmpty(entry.PdfUrl) || regenerate)
            {
                entry.PdfUrl = await GenerateHemSamparkPdfAsync(entry, master);
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, pdfUrl = entry.PdfUrl });
        }



        private async Task<string> GenerateHemSamparkPdfAsync(
            DailySamparkEntry entry,
            MasterSetting master)
        {
            string E(string value) =>
                System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

            string GetBase64Image(string relativePath)
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    return string.Empty;

                try
                {
                    string cleanPath = relativePath
                        .TrimStart('/', '\\')
                        .Replace('/', Path.DirectorySeparatorChar);

                    string path = Path.Combine(
                        _webHostEnvironment.WebRootPath,
                        cleanPath);

                    if (!System.IO.File.Exists(path))
                        return string.Empty;

                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    string ext = Path.GetExtension(path).ToLowerInvariant();

                    string mime = ext switch
                    {
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".svg" => "image/svg+xml",
                        ".webp" => "image/webp",
                        ".gif" => "image/gif",
                        _ => "image/png"
                    };

                    return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                }
                catch
                {
                    return string.Empty;
                }
            }

            string GetLogo(string path, string fallback)
            {
                string image = GetBase64Image(path);

                return string.IsNullOrEmpty(image)
                    ? fallback
                    : $"<img src='{image}' alt='Logo' />";
            }

            string logo1 = GetLogo(master?.Logo1Url ?? "", "<b>RR GLOBAL</b>");
            string logo2 = GetLogo(master?.Logo2Url ?? "",
                "<b class='hem-logo'>HEM सम्पर्क</b>");
            string logo3 = GetLogo(master?.Logo3Url ?? "",
                "<b>HEMA FOUNDATION</b>");

            string date = E(entry.EntryDate.ToString(
                "dddd, dd MMMM yyyy",
                System.Globalization.CultureInfo.GetCultureInfo("en-IN")));

            string samvat = E(Convert.ToString(entry.VikramSamvat) ?? "");
            string dayHindi = E(Convert.ToString(entry.DayHindi) ?? "");
            string issue = E(Convert.ToString(entry.IssueNumber) ?? "");

            // Build question list without options.
            var questionsBuilder = new System.Text.StringBuilder();

            if (entry.Questions != null)
            {
                int index = 1;

                foreach (var q in entry.Questions)
                {
                    questionsBuilder.Append($@"
                <p class='question'>
                    <b>Q{index}. {E(q.QuestionText ?? "")}</b>
                </p>");

                    index++;
                }
            }

            string footerText = E(master?.FooterText ?? "");
            string footerLink = E(master?.FooterLink ?? "");
            string rawLink = (master?.FooterLink ?? "").Trim();

            string footerLinkHtml = footerLink;

            if (!string.IsNullOrWhiteSpace(rawLink))
            {
                string href =
                    rawLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || rawLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                        ? rawLink
                        : "https://" + rawLink;

                footerLinkHtml = $"<a href='{E(href)}'>{footerLink}</a>";
            }

            string htmlContent = $@"<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8' /><style>
    @page {{
        size: 140mm 297mm; /* Page size ko exact .page ki width ke barabar karein */
        margin: 0;
    }}
    *, *:before, *:after {{
        box-sizing: border-box;
    }}
    html, body {{
        margin: 0;
        padding: 0;
        width: 140mm;
        font-family: Arial, 'Noto Sans Devanagari', sans-serif;
        color: #202020;
        -webkit-print-color-adjust: exact;
        print-color-adjust: exact;
    }}
    body {{
        background: #fff7f5;
    }}
    .page {{ 
        width: 140mm;
        min-height: 297mm;
        padding: 3mm;
        /* Yahan border-image se half color split kiya gaya hai */
        border: 3mm solid;
        border-image: linear-gradient(to right, #29266f 50%, #00b4ff 50%) 1;
        background: #fff7f5;
        display: flex;
        flex-direction: column;
    }}

    /* Centered three-logo header */
    .header-table {{
        width: 100%;
        table-layout: fixed;
        border-collapse: collapse;
        border-bottom: 2px solid #29266f;
        margin-bottom: 5px;
    }}

    .header-table td {{
        width: 33.333%;
        text-align: center;
        vertical-align: middle;
        padding: 2px 3px 5px;
        font-size: 11px;
        font-weight: bold;
    }}

    .header-table img {{
        max-width: 100%;
        max-height: 42px;
        object-fit: contain;
    }}

    .hem-logo {{
        font-size: 17px;
        color: #29266f;
    }}

    /* Date on left, Vikram Samvat / Friday / Issue on right */
    .date-table {{
        width: 100%;
        table-layout: fixed;
        border-collapse: collapse;
        margin-bottom: 7px;
        font-size: 10px;
        font-weight: bold;
        line-height: 1.25;
    }}

    .date-table td {{
        vertical-align: top;
        padding: 2px 0;
    }}

    /* Section heading strips */
    .strip-heading {{
        background: #ffd9d3;
        color: #282020;
        text-align: center;
        font-family: Georgia, serif;
        font-size: 17px;
        font-style: italic;
        font-weight: bold;
        padding: 3px 5px;
        margin: 4px 0;
    }}

    .mantra-box {{
        background: #fce4ec;
        border: 1px solid #efbfd0;
        padding: 6px 8px;
        margin-bottom: 6px;
    }}

    .mantra-title {{
        background: #ffd9d3;
        text-align: center;
        font-family: Georgia, serif;
        font-size: 17px;
        font-weight: bold;
        font-style: italic;
        padding: 3px;
        margin: -6px -8px 5px;
    }}

    .mantra-en {{
        font-size: 11px;
        font-weight: bold;
        line-height: 1.3;
        margin: 0 0 3px;
    }}

    .mantra-hi {{
        color: #ad2456;
        font-size: 11px;
        font-weight: bold;
        line-height: 1.3;
        margin: 0;
    }}

    .celebration-heading {{
        text-align: center;
        font-size: 13px;
        line-height: 1.25;
        font-weight: 800;
        color: #c52218;
        margin: 3px 0;
    }}

    .content-text {{
        width: 100%;
    margin: 3px auto 8px;
    font-size: 11.5px;
    font-weight: 700;
    line-height: 1.35;
    text-align: justify;
    overflow-wrap: anywhere;
    }}

    .content-hi {{
        color: #ad2456;
    font-weight: 700;
    }}

    /* Day's Challenge */
    .challenge-box {{
        background: #f0f9e9;
        border: 1px solid #a8d79b;
        padding: 6px 8px;
        margin: 4px 0 6px;
    }}

    .challenge-title {{
        background: #ffd9d3;
        text-align: center;
        font-family: Georgia, serif;
        font-size: 17px;
        font-style: italic;
        font-weight: bold;
        color: #282020;
        padding: 3px;
        margin: -6px -8px 5px;
    }}

    .question {{
        font-size: 10.8px;
        font-weight: 700;
        line-height: 1.32;
        margin: 0 0 5px;
        overflow-wrap: anywhere;
    }}

    .question:last-child {{
        margin-bottom: 0;
    }}

    /* Values in Focus */
    .values-title {{
        background: #ffd9d3;
        text-align: center;
        font-family: Georgia, serif;
        font-size: 17px;
        font-style: italic;
        font-weight: bold;
        padding: 3px;
        margin: -6px -8px 5px;
    }}

    .values-box {{
        background: #f0f9e9;
        border: 1px solid #a8d79b;
        padding: 6px 8px;
        margin-top: 2px;
    }}

    .values-en {{
        font-size: 10.5px;
        font-weight: bold;
        text-align: center;
        line-height: 1.3;
        margin: 0 0 3px;
    }}

    .values-hi {{
        color: #ad2456;
        font-size: 10.5px;
        font-weight: bold;
        text-align: center;
        line-height: 1.3;
        margin: 0;
    }}

    .footer-box {{
        text-align: center;
        margin-top: auto;
        padding: 7px 0 0;
        border-top: 1px solid #8d9fc6;
        font-size: 11px;
        font-weight: bold;
    }}

    .footer-box a {{
        color: #202f83;
        text-decoration: underline;
        overflow-wrap: anywhere;
    }}

    .footer-box p {{
        color: #c52218;
        font-size: 11px;
        font-weight: bold;
        margin: 3px 0 0;
    }}
</style>
</head>

<body>
<div class='page'>

    <table class='header-table'>
        <tr>
            <td>{logo1}</td>
            <td>{logo2}</td>
            <td>{logo3}</td>
        </tr>
    </table>

    <table class='date-table'>
        <tr>
            <td style='width:42%; text-align:left'>
                {date}<br />Issue No: {issue}
            </td>
            <td style='width:22%; text-align:right'>
                {samvat} 
            </td>
        </tr>
    </table>

    <div class='mantra-box'>
        <div class='mantra-title'>Morning Mantra</div>
        <p class='mantra-en'>
            {E(master?.MorningMantraEn ?? "")}
        </p>
        <p class='mantra-hi'>
            {E(master?.MorningMantraHi ?? "")}
        </p>
    </div>

    <div class='strip-heading'>Celebrations Today</div>

    <h4 class='celebration-heading'>
        {E(entry.CelebrationHeadingEn ?? "")}
    </h4>

    <div class='content-text'>
        {entry.CelebrationContentEn ?? ""}
    </div>

    <h4 class='strip-heading'>
        {E(entry.CelebrationHeadingHi ?? "")}
    </h4>

    <div class='content-text content-hi'>
        {entry.CelebrationContentHi ?? ""}
    </div>

    <div class='challenge-box'>
        <div class='challenge-title'>Day's Challenge</div>
        {questionsBuilder}
    </div>

    <div class='values-box'>
        <div class='values-title'>Values in Focus</div>
        <p class='values-en'>
            {E(master?.ValuesInFocusEn ?? "")}
        </p>
        <p class='values-hi'>
            {E(master?.ValuesInFocusHi ?? "")}
        </p>
    </div>

    <div class='footer-box'>
        {footerLinkHtml}
        <p>{footerText}</p>
    </div>

</div>
</body>
</html>";

            string folderName = "generateHemPdf";

            string uploadDir = Path.Combine(
                _webHostEnvironment.WebRootPath,
                folderName);

            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            string fileName =
                $"HemSampark_{entry.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            string filePath = Path.Combine(uploadDir, fileName);

            using var browser = await Puppeteer.LaunchAsync(
                new LaunchOptions
                {
                    Headless = true
                });

            using var page = await browser.NewPageAsync();

            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 794,
                Height = 1123,
                DeviceScaleFactor = 1
            });

            await page.SetContentAsync(
                htmlContent,
                new NavigationOptions
                {
                    WaitUntil = new[]
                    {
                WaitUntilNavigation.Networkidle0
                    }
                });

            await page.EmulateMediaTypeAsync(MediaType.Print);

            // Fit content into one A4 page only when it can fit without clipping.
            bool fitsOnOnePage = await page.EvaluateExpressionAsync<bool>(
                "document.querySelector('.page').scrollHeight <= " +
                "document.querySelector('.page').clientHeight + 1");

            if (!fitsOnOnePage)
            {
                // Reduce spacing and typography slightly for longer daily content.
                await page.AddStyleTagAsync(new AddTagOptions
                {
                    Content = @"
                .page {
                    height: auto !important;
                    min-height: 297mm;
                }

                .content-text {
                    font-size: 10.5px;
                    line-height: 1.25;
                    margin-bottom: 4px;
                }

                .question {
                    font-size: 10px;
                    line-height: 1.25;
                    margin-bottom: 3px;
                }

                .mantra-box {
                    padding: 5px 7px;
                    margin-bottom: 4px;
                }

                .challenge-box {
                    padding: 5px 7px;
                    margin: 3px 0 4px;
                }

                .values-box {
                    padding: 5px 7px;
                }

                .footer-box {
                    margin-top: 8px;
                }"
                });
            }

            await page.PdfAsync(filePath, new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                PreferCSSPageSize = true,
                DisplayHeaderFooter = false,
                MarginOptions = new MarginOptions
                {
                    Top = "0",
                    Bottom = "0",
                    Left = "0",
                    Right = "0"
                }
            });

            return $"/{folderName}/{fileName}";
        }


        [HttpGet("today-entry")]
        public async Task<IActionResult> GetTodayEntry()
        {
            try
            {
                var today = DateTime.Now.Date;
                var entry = await _context.dailysamparkentries
                    .Where(x => x.EntryDate.Date == today)
                    .FirstOrDefaultAsync()
                    ?? await _context.dailysamparkentries.OrderByDescending(x => x.Id).FirstOrDefaultAsync();

                if (entry == null)
                    return NotFound(new { success = false, message = "No entry found" });

                var questions = await _context.Set<DailyQuestion>()
                    .Where(q => q.DailyEntryId == entry.Id)
                    .Select(q => new {
                        id = q.Id,
                        questionText = q.QuestionText,
                        option1 = q.Option1,
                        option2 = q.Option2,
                        option3 = q.Option3,
                        option4 = q.Option4
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = entry.Id,
                        entryDate = entry.EntryDate.ToString("yyyy-MM-dd"),
                        dayHindi = entry.DayHindi,
                        issueNumber = entry.IssueNumber,
                        vikramSamvat = entry.VikramSamvat,
                        celebrationHeadingEn = entry.CelebrationHeadingEn,
                        celebrationContentEn = entry.CelebrationContentEn,
                        celebrationHeadingHi = entry.CelebrationHeadingHi,
                        celebrationContentHi = entry.CelebrationContentHi,
                        pdfUrl = entry.PdfUrl,
                        questions = questions
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("submit-quiz")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmissionDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "StudentUser";

            var submission = new StudentQuizSubmission
            {
                UserId = userId,
                DailyEntryId = model.DailyEntryId,
                Score = model.Score,
                TotalQuestions = model.TotalQuestions,
                SubmittedAt = DateTime.Now
            };

            _context.Set<StudentQuizSubmission>().Add(submission);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Quiz submitted successfully!" });
        }

        public class QuizSubmissionDto
        {
            public int DailyEntryId { get; set; }
            public int Score { get; set; }
            public int TotalQuestions { get; set; }
        }

        public class StudentQuizSubmission
        {
            public int Id { get; set; }
            public string UserId { get; set; }
            public int DailyEntryId { get; set; }
            public int Score { get; set; }
            public int TotalQuestions { get; set; }
            public DateTime SubmittedAt { get; set; }
        }


    }
}