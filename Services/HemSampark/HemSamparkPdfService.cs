using HemFoundation.Models.HemSampark;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace HemFoundation.Services.HemSampark
{
    /// <summary>
    /// The publication cannot be laid out on a single page at a readable size.
    /// The message is written for the admin and is safe to show as-is.
    /// </summary>
    public class HemSamparkPdfLayoutException : Exception
    {
        public HemSamparkPdfLayoutException(string message) : base(message) { }
    }

    /// <summary>
    /// Renders a HEM Sampark publication to PDF (plus a PNG preview used by the
    /// mobile-friendly reader) and owns where those files live on disk.
    /// </summary>
    public class HemSamparkPdfService
    {
        private const string LegacyWebFolder = "generateHemPdf";

        // ---- One-page layout -------------------------------------------------
        // The page is 140mm x 297mm. Every pixel size in the template is
        // multiplied by a single scale factor (--s). The generator picks the
        // largest scale at which everything fits on that one page: long issues
        // are tightened, short issues are enlarged so the page is not left
        // half empty. Nothing is ever cropped or hidden.
        private const double PageHeightPx = 297 * 96 / 25.4;   // 297mm in CSS pixels
        private const double FitSafetyPx = 3;                  // keeps clear of a spill onto page 2
        // Text is never made smaller than this. When the content does not fit
        // on 297mm at this size, the (single) page is made taller instead.
        private const double MinScale = 0.90;
        private const double MaxScale = 1.30;
        private const double PxPerMm = 96 / 25.4;

        private static readonly System.Text.RegularExpressions.Regex StyleBlock =
            new(@"<style>(.*?)</style>", System.Text.RegularExpressions.RegexOptions.Singleline);

        private static readonly System.Text.RegularExpressions.Regex PixelValue =
            new(@"(?<![\w.])(\d+(?:\.\d+)?)px");

        /// <summary>Rewrites "12px" in the template's stylesheet as "calc(12px * var(--s))".</summary>
        private static string MakeStylesScalable(string html) =>
            StyleBlock.Replace(html, match =>
                "<style>:root { --s: 1; }" +
                PixelValue.Replace(match.Groups[1].Value, "calc($1px * var(--s))") +
                "</style>", 1);

        private static int CountPdfPages(string filePath)
        {
            // Chromium writes page objects uncompressed, so a plain scan is enough.
            var text = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(filePath));
            return System.Text.RegularExpressions.Regex.Matches(text, @"/Type\s*/Page(?![a-zA-Z])").Count;
        }

        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly HemSamparkClock _clock;
        private readonly ILogger<HemSamparkPdfService> _logger;

        public HemSamparkPdfService(
            IWebHostEnvironment env,
            IConfiguration config,
            HemSamparkClock clock,
            ILogger<HemSamparkPdfService> logger)
        {
            _env = env;
            _config = config;
            _clock = clock;
            _logger = logger;
        }

        /// <summary>Private folder (outside wwwroot) for generated files.</summary>
        private string StorageRoot
        {
            get
            {
                var configured = _config["HemSampark:StoragePath"];
                var root = string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(_env.ContentRootPath, "App_Data", "HemSampark")
                    : (Path.IsPathRooted(configured)
                        ? configured
                        : Path.Combine(_env.ContentRootPath, configured));
                Directory.CreateDirectory(root);
                return root;
            }
        }

        private static bool IsLegacyWebPath(string stored) =>
            stored.StartsWith('/') || stored.StartsWith('\\');

        /// <summary>
        /// Maps a stored pdfurl value to a physical file. Returns null when the
        /// value is empty, escapes its folder, or the file is missing.
        /// </summary>
        public string? ResolvePdfPath(string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;

            string root;
            string relative;

            if (IsLegacyWebPath(stored))
            {
                // PDFs generated before the upgrade live under wwwroot.
                root = Path.Combine(_env.WebRootPath, LegacyWebFolder);
                relative = Path.GetFileName(stored.Replace('\\', '/'));
            }
            else
            {
                root = StorageRoot;
                relative = Path.GetFileName(stored);
            }

            if (string.IsNullOrEmpty(relative) ||
                !relative.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return null;

            var full = Path.GetFullPath(Path.Combine(root, relative));
            var rootFull = Path.GetFullPath(root) + Path.DirectorySeparatorChar;

            if (!full.StartsWith(rootFull, StringComparison.Ordinal)) return null;
            return File.Exists(full) ? full : null;
        }

        /// <summary>PNG preview that sits next to a generated PDF, if any.</summary>
        public string? ResolvePreviewImagePath(string? stored)
        {
            var pdf = ResolvePdfPath(stored);
            if (pdf == null) return null;
            var png = Path.ChangeExtension(pdf, ".png");
            return File.Exists(png) ? png : null;
        }

        /// <summary>
        /// Removes a generated file. Legacy wwwroot files are left untouched.
        /// </summary>
        public void DeleteGenerated(string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored) || IsLegacyWebPath(stored)) return;

            try
            {
                var pdf = ResolvePdfPath(stored);
                if (pdf == null) return;
                File.Delete(pdf);
                var png = Path.ChangeExtension(pdf, ".png");
                if (File.Exists(png)) File.Delete(png);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HEM Sampark: could not delete old PDF {File}", stored);
            }
        }

        private string? ResolveChromiumPath()
        {
            var configured = _config["HemSampark:ChromiumPath"];
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                return configured;

            var fromEnv = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");
            if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
                return fromEnv;

            // Program.cs downloads Chromium into {ContentRoot}/Chromium at startup.
            try
            {
                var fetcher = new BrowserFetcher(new BrowserFetcherOptions
                {
                    Path = Path.Combine(_env.ContentRootPath, "Chromium")
                });

                var path = fetcher.GetInstalledBrowsers()
                    .Select(b => b.GetExecutablePath())
                    .FirstOrDefault(File.Exists);

                if (path != null) return path;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "HEM Sampark: Chromium lookup failed");
            }

            return null; // let PuppeteerSharp use its default location
        }

        /// <summary>
        /// Generates a new PDF for the entry and returns the stored file name.
        /// Never overwrites an existing file.
        /// </summary>
        public async Task<string> GenerateAsync(DailySamparkEntry entry, MasterSetting? master)
        {
            string E(string? value) =>
                System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

            // Content that already contains markup is used as-is (existing
            // behaviour); plain text is encoded and keeps its line breaks.
            string Rich(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return string.Empty;
                if (value.Contains('<') && value.Contains('>')) return value;
                return E(value.Trim()).Replace("\r\n", "\n").Replace("\n", "<br />");
            }

            // English and Hindi on the same line, separated by " / ".
            // The day's English and Hindi paragraphs start with a round dot, as in the printed sheet.
            // Admin text can be plain or HTML; for HTML the dot goes inside the first paragraph.
            string WithDot(string html)
            {
                if (string.IsNullOrWhiteSpace(html)) return html;
                const string dot = "<span class='para-dot' aria-hidden='true'></span>";
                var m = System.Text.RegularExpressions.Regex.Match(html, @"^\s*<(p|div|span|strong|b)(\s[^>]*)?>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                return m.Success ? html.Insert(m.Length, dot) : dot + html;
            }

            string BilingualLine(string cssClass, string? en, string? hi)
            {
                en = en?.Trim() ?? "";
                hi = hi?.Trim() ?? "";
                if (en.Length == 0 && hi.Length == 0) return "";

                string enHtml = en.Length > 0 ? $"<span class='bl-en'>{E(en)}</span>" : "";
                string hiHtml = hi.Length > 0 ? $"<span class='bl-hi'>{E(hi)}</span>" : "";
                string sep = en.Length > 0 && hi.Length > 0 ? "<span class='bl-sep'> / </span>" : "";

                return $"<p class='{cssClass}'>{enHtml}{sep}{hiHtml}</p>";
            }

            string First(string? preferred, string? fallback) =>
                string.IsNullOrWhiteSpace(preferred) ? (fallback ?? string.Empty) : preferred;

            string GetBase64Image(string relativePath)
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    return string.Empty;

                try
                {
                    string cleanPath = relativePath
                        .TrimStart('/', '\\')
                        .Replace('/', Path.DirectorySeparatorChar);

                    string webRoot = Path.GetFullPath(_env.WebRootPath) + Path.DirectorySeparatorChar;
                    string path = Path.GetFullPath(Path.Combine(_env.WebRootPath, cleanPath));

                    if (!path.StartsWith(webRoot, StringComparison.Ordinal) || !File.Exists(path))
                        return string.Empty;

                    byte[] bytes = File.ReadAllBytes(path);
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

            string GetLogo(string? path, string fallback)
            {
                string image = GetBase64Image(path ?? "");

                return string.IsNullOrEmpty(image)
                    ? fallback
                    : $"<img src='{image}' alt='Logo' />";
            }

            string logo1 = GetLogo(master?.Logo1Url, "<b>RR GLOBAL</b>");
            string logo2 = GetLogo(master?.Logo2Url, "<b class='hem-logo'>HEM सम्पर्क</b>");
            string logo3 = GetLogo(master?.Logo3Url, "<b>HEMA FOUNDATION</b>");

            string date = E(entry.EntryDate.ToString(
                "dddd, dd MMMM yyyy",
                System.Globalization.CultureInfo.GetCultureInfo("en-IN")));

            string samvat = E(entry.VikramSamvat);
            string issue = E(entry.IssueNumber.ToString());

            // The Morning Mantra is written for each publication (no master default).
            string mantraEn = entry.MorningMantraEn?.Trim() ?? "";
            string mantraHi = entry.MorningMantraHi?.Trim() ?? "";

            string mantraHtml = mantraEn.Length == 0 && mantraHi.Length == 0
                ? ""
                : $@"<div class='mantra-box'>
        <div class='mantra-title'>Morning Mantra</div>
        {BilingualLine("mantra-line", mantraEn, mantraHi)}
    </div>";

            // Optional small picture at the right of the English celebration text.
            string celebrationImage = GetBase64Image(entry.CelebrationImageUrl ?? "");
            string celebrationImageHtml = string.IsNullOrEmpty(celebrationImage)
                ? ""
                : $"<img class='celebration-img' src='{celebrationImage}' alt='' />";

            string hindiHeadingClass = master?.HindiHeadingBackground == true
                ? "celebration-heading-hi with-bg"
                : "celebration-heading-hi";
            string valuesEn = First(entry.ValuesInFocusEn, master?.ValuesInFocusEn);
            string valuesHi = First(entry.ValuesInFocusHi, master?.ValuesInFocusHi);

            // Question list without options (answers never go into the PDF).
            var questionsBuilder = new System.Text.StringBuilder();
            int index = 1;

            foreach (var q in (entry.Questions ?? new List<DailyQuestion>())
                         .Where(q => q.IsActive)
                         .OrderBy(q => q.DisplayOrder)
                         .ThenBy(q => q.Id))
            {
                questionsBuilder.Append($@"
                <p class='question'>
                    <b>Q{index}. {E(q.QuestionText)}</b>
                </p>");
                index++;
            }

            // A different colour theme for each day of the week (from the publication date).
            string themeVars = DayTheme(entry.EntryDate.DayOfWeek);

            string footerText = E(master?.FooterText);
            string footerLink = E(master?.FooterLink);
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
    :root {{ {themeVars} }}

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
        background: var(--page-bg);
    }}
    .page {{ 
        width: 140mm;
        min-height: 297mm;
        padding: 3mm;
        /* Yahan border-image se half color split kiya gaya hai */
        border: 3mm solid;
        border-image: linear-gradient(to right, #29266f 50%, #00b4ff 50%) 1;  /* outer border: same every day */
        background: var(--page-bg);
        display: flex;
        flex-direction: column;
    }}

    /* Centered three-logo header */
    .header-table {{
        width: 100%;
        table-layout: fixed;
        border-collapse: collapse;
        border-bottom: 2px solid var(--frame1);
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
        color: var(--frame1);
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
        background: var(--strip);
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
        background: var(--mantra-bg);
        border: 1px solid var(--mantra-border);
        padding: 6px 8px;
        margin-bottom: 6px;
    }}

    .mantra-title {{
        background: var(--strip);
        text-align: center;
        font-family: Georgia, serif;
        font-size: 17px;
        font-weight: bold;
        font-style: italic;
        padding: 3px;
        margin: -6px -8px 5px;
    }}

    .mantra-line {{
        font-size: 11px;
        font-weight: bold;
        line-height: 1.35;
        margin: 0;
    }}

    .values-line {{
        font-size: 10.5px;
        font-weight: bold;
        text-align: center;
        line-height: 1.35;
        margin: 0;
    }}

    .bl-en {{ color: #202020; }}
    .bl-sep {{ color: #6b6b6b; }}
    .bl-hi {{ color: var(--hi); }}

    .mantra-en {{
        font-size: 11px;
        font-weight: bold;
        line-height: 1.3;
        margin: 0 0 3px;
    }}

    .mantra-hi {{
        color: var(--hi);
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
        color: var(--heading);
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

    /* Hindi celebration heading: same weight and colour family as the
       English heading, larger, no background unless enabled in Master Settings. */
    .celebration-heading-hi {{
        text-align: center;
        font-family: Arial, 'Noto Sans Devanagari', sans-serif;
        font-size: 17px;
        line-height: 1.3;
        font-weight: 800;
        color: var(--hi);
        margin: 6px 0 3px;
        padding: 0;
    }}

    .celebration-heading-hi.with-bg {{
        background: var(--strip);
        padding: 3px 5px;
    }}

    .celebration-img {{
        float: right;
        width: 34mm;
        max-height: 42mm;
        object-fit: cover;
        margin: 2px 0 4px 8px;
        border-radius: 4px;
        border: 1px solid var(--img-border);
        image-rendering: auto;
    }}

    /* Round dot before the English and Hindi paragraphs. */
    .para-dot {{
        display: inline-block;
        width: 0.62em;
        height: 0.62em;
        border-radius: 50%;
        background: currentColor;
        margin: 0 0.4em 0.08em 0;
        vertical-align: middle;
    }}

    .content-text::after {{
        content: '';
        display: block;
        clear: both;
    }}

    .content-hi {{
        color: var(--hi);
    font-weight: 700;
    }}

    /* Day's Challenge */
    .challenge-box {{
        background: var(--box-bg);
        border: 1px solid var(--box-border);
        padding: 6px 8px;
        margin: 4px 0 6px;
    }}

    .challenge-title {{
        background: var(--strip);
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

    /* Fixed instruction under the Day's Challenge title. */
    .challenge-note {{
        font-size: 10.8px;
        font-weight: 700;
        line-height: 1.35;
        margin: 0 0 6px;
        padding-bottom: 5px;
        border-bottom: 1px dashed var(--box-border);
        overflow-wrap: anywhere;
    }}

    .challenge-note .bl-hi {{
        font-family: Arial, 'Noto Sans Devanagari', sans-serif;
    }}

    .question:last-child {{
        margin-bottom: 0;
    }}

    /* Values in Focus */
    .values-title {{
        background: var(--strip);
        text-align: center;
        font-family: Georgia, serif;
        font-size: 17px;
        font-style: italic;
        font-weight: bold;
        padding: 3px;
        margin: -6px -8px 5px;
    }}

    .values-box {{
        background: var(--box-bg);
        border: 1px solid var(--box-border);
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
        color: var(--hi);
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
        border-top: 1px solid var(--footer-border);
        font-size: 13.5px;
        font-weight: bold;
    }}

    .footer-box a {{
        color: var(--link);
        text-decoration: underline;
        overflow-wrap: anywhere;
    }}

    .footer-box p {{
        color: var(--heading);
        font-size: 13.5px;
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

    {mantraHtml}

    <div class='strip-heading'>{E(HemSamparkSectionTitle.Resolve(entry.CelebrationSectionTitle))}</div>

    <h4 class='celebration-heading'>
        {E(entry.CelebrationHeadingEn ?? "")}
    </h4>

    <div class='content-text'>
        {celebrationImageHtml}{WithDot(Rich(entry.CelebrationContentEn))}
    </div>

    <h4 class='{hindiHeadingClass}'>
        {E(entry.CelebrationHeadingHi ?? "")}
    </h4>

    <div class='content-text content-hi'>
        {WithDot(Rich(entry.CelebrationContentHi))}
    </div>

    <div class='challenge-box'>
        <div class='challenge-title'>Day's Challenge</div>
        {(index > 1 ? ChallengeNote : string.Empty)}
        {questionsBuilder}
    </div>

    <div class='values-box'>
        <div class='values-title'>Values in Focus</div>
        {BilingualLine("values-line", valuesEn, valuesHi)}
    </div>

    <div class='footer-box'>
        {footerLinkHtml}
        <p>{footerText}</p>
    </div>

</div>
</body>
</html>";


            htmlContent = MakeStylesScalable(htmlContent);

            // Unguessable, collision-free file name; never reuses an old file.
            string fileName =
                $"HemSampark_{entry.Id}_{_clock.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.pdf";

            string filePath = Path.Combine(StorageRoot, fileName);

            var launchOptions = new LaunchOptions { Headless = true };

            var chromium = ResolveChromiumPath();
            if (chromium != null) launchOptions.ExecutablePath = chromium;

            if (_config.GetValue<bool>("HemSampark:ChromiumNoSandbox"))
                launchOptions.Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" };

            await using var browser = await Puppeteer.LaunchAsync(launchOptions);
            await using var page = await browser.NewPageAsync();

            // The PDF content never needs the network; block it so admin-entered
            // markup cannot make the server fetch arbitrary URLs.
            await page.SetRequestInterceptionAsync(true);
            page.Request += async (_, e) =>
            {
                try
                {
                    var url = e.Request.Url ?? "";
                    if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                        url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                        await e.Request.ContinueAsync();
                    else
                        await e.Request.AbortAsync();
                }
                catch { /* request already handled */ }
            };
            await page.SetJavaScriptEnabledAsync(false);

            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 794,
                Height = 1123,
                DeviceScaleFactor = 2
            });

            await page.SetContentAsync(
                htmlContent,
                new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Load }
                });

            await page.EmulateMediaTypeAsync(MediaType.Print);

            await SharpenSmallPicturesAsync(page);

            // ---- Fit everything on one page ----
            // Height of the content at a given scale, measured without the
            // "fill the page" minimum height.
            async Task<double> ContentHeightAsync(double scale) =>
                await page.EvaluateFunctionAsync<double>(
                    @"s => {
                        const root = document.documentElement;
                        const el = document.querySelector('.page');
                        root.style.setProperty('--s', s);
                        el.style.minHeight = '0';
                        const h = el.getBoundingClientRect().height;
                        el.style.minHeight = '';
                        return h;
                    }", scale);

            double limit = PageHeightPx - FitSafetyPx;
            double pageHeightMm = 297;

            double low = MinScale, high = MaxScale;

            if (await ContentHeightAsync(MaxScale) <= limit)
            {
                low = MaxScale;
            }
            else if (await ContentHeightAsync(MinScale) > limit)
            {
                // Too much content for 297mm at a comfortable size: keep the
                // size and make the single page as tall as the content.
                low = MinScale;
            }
            else
            {
                for (int i = 0; i < 12 && high - low > 0.004; i++)
                {
                    double mid = (low + high) / 2;
                    if (await ContentHeightAsync(mid) <= limit) low = mid; else high = mid;
                }
            }

            double finalScale = Math.Max(MinScale, Math.Floor(low * 200) / 200);   // round down to 0.005
            double contentPx = await ContentHeightAsync(finalScale);

            if (contentPx > limit)
                pageHeightMm = Math.Ceiling((contentPx + FitSafetyPx) / PxPerMm) + 2;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                // One page, exactly this size; the coloured frame fills it.
                await page.EvaluateFunctionAsync(
                    @"h => {
                        let tag = document.getElementById('hs-page-size');
                        if (!tag) {
                            tag = document.createElement('style');
                            tag.id = 'hs-page-size';
                            document.head.appendChild(tag);
                        }
                        tag.textContent = '@page { size: 140mm ' + h + 'mm; margin: 0; } ' +
                                          '.page { min-height: ' + h + 'mm !important; }';
                    }", pageHeightMm);

                await page.PdfAsync(filePath, new PdfOptions
                {
                    Width = "140mm",
                    Height = $"{pageHeightMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm",
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

                if (CountPdfPages(filePath) <= 1) break;

                // Rounding spilled a line onto a second page: allow a little more room.
                if (attempt == 2)
                {
                    File.Delete(filePath);
                    throw new HemSamparkPdfLayoutException(
                        "The PDF could not be laid out on a single page. Please try again.");
                }

                pageHeightMm += 6;
            }

            // PNG copy of the same layout: phones cannot show PDFs inline, so
            // the reader displays this image and links to the PDF.
            try
            {
                var pageElement = await page.QuerySelectorAsync(".page");
                if (pageElement != null)
                {
                    await pageElement.ScreenshotAsync(
                        Path.ChangeExtension(filePath, ".png"),
                        new ElementScreenshotOptions { Type = ScreenshotType.Png });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HEM Sampark: preview image failed for entry {Id}", entry.Id);
            }

            return fileName;
        }
    
        /// <summary>Seven colour themes for the PDF, one for each day of the week.</summary>
        private static string DayTheme(DayOfWeek day)
        {
            // frame1, frame2, page background, strip, mantra background, mantra border,
            // Hindi text, heading, box background, box border, link, footer line, picture border
            string[] t = day switch
            {
                DayOfWeek.Monday => new[] { "#29266f", "#00b4ff", "#fff7f5", "#ffd9d3", "#fce4ec", "#efbfd0", "#ad2456", "#c52218", "#f0f9e9", "#a8d79b", "#202f83", "#8d9fc6", "#efc9c0" },   // navy and sky
                DayOfWeek.Tuesday => new[] { "#9a3412", "#f59e0b", "#fffaf0", "#fde2b8", "#fff1d6", "#f3cf8f", "#9a3412", "#c2410c", "#eef6ff", "#9cc3ea", "#7c2d12", "#e0b57a", "#f3cf8f" },  // saffron
                DayOfWeek.Wednesday => new[] { "#14532d", "#22c55e", "#f6fbf4", "#cdeccf", "#e7f6e7", "#a9d8ab", "#166534", "#15803d", "#fff8e6", "#eacb84", "#14532d", "#98c9a0", "#a9d8ab" }, // green
                DayOfWeek.Thursday => new[] { "#4c1d95", "#eab308", "#faf7ff", "#e4d8fb", "#f1eafe", "#cdb8f2", "#6d28d9", "#7c3aed", "#fff9e6", "#ecd58a", "#4c1d95", "#b9a6e3", "#cdb8f2" },  // purple and gold
                DayOfWeek.Friday => new[] { "#134e4a", "#14b8a6", "#f3fbfa", "#c9efea", "#e3f6f3", "#a3dcd4", "#0f766e", "#0d9488", "#fdf2f8", "#efb4d1", "#134e4a", "#8fcfc6", "#a3dcd4" },    // teal
                DayOfWeek.Saturday => new[] { "#1e3a8a", "#3b82f6", "#f5f8ff", "#d6e4ff", "#e8efff", "#b3c8f5", "#1d4ed8", "#1e40af", "#f0f9e9", "#a8d79b", "#1e3a8a", "#9db4e6", "#b3c8f5" },  // blue
                _ => new[] { "#7f1d1d", "#ec4899", "#fff5f7", "#fbd0dc", "#fde7ee", "#f2b3c7", "#9d174d", "#b91c1c", "#f0f9ff", "#9fd0ee", "#7f1d1d", "#e3a3b5", "#f2b3c7" }                       // Sunday: maroon and pink
            };
            string[] n = { "frame1", "frame2", "page-bg", "strip", "mantra-bg", "mantra-border", "hi", "heading", "box-bg", "box-border", "link", "footer-border", "img-border" };
            return string.Join(" ", n.Select((name, i) => $"--{name}: {t[i]};"));
        }

        /// <summary>Printed under the "Day's Challenge" title, above the questions.</summary>
        private const string ChallengeNote =
            "<p class='challenge-note'><span class='bl-en'>Look at the four options given in the Google form and answer.</span>" +
            "<span class='bl-sep'> / </span><span class='bl-hi'>गूगल फॉर्म में दर्शाए गए चार विकल्पों को देखकर उत्तर दें।</span></p>";

        /// <summary>
        /// The small picture beside the English text is printed about 34 mm wide. A low-resolution
        /// upload looks soft or blocky there, so it is enlarged in steps with high-quality
        /// resampling and lightly sharpened (an unsharp mask) before the PDF is made.
        /// Pictures that are already large enough are left exactly as uploaded.
        /// </summary>
        private async Task SharpenSmallPicturesAsync(IPage page)
        {
            try
            {
                await page.EvaluateFunctionAsync(@"async () => {
                    const TARGET = 1000;                       // px across the printed width (about 750 dpi)
                    for (const img of document.querySelectorAll('img.celebration-img')) {
                        try {
                            if (!img.complete) await img.decode();
                            let w = img.naturalWidth, h = img.naturalHeight;
                            if (!w || !h || w >= TARGET) continue;

                            let canvas = document.createElement('canvas');
                            canvas.width = w; canvas.height = h;
                            canvas.getContext('2d').drawImage(img, 0, 0);

                            // Enlarge at most 2x per step: much smoother than one big jump.
                            while (w < TARGET) {
                                const nw = Math.min(TARGET, w * 2), nh = Math.round(h * nw / w);
                                const next = document.createElement('canvas');
                                next.width = nw; next.height = nh;
                                const ctx = next.getContext('2d');
                                ctx.imageSmoothingEnabled = true;
                                ctx.imageSmoothingQuality = 'high';
                                ctx.drawImage(canvas, 0, 0, nw, nh);
                                canvas = next; w = nw; h = nh;
                            }

                            // Unsharp mask: picture + 0.7 x (picture - blurred picture).
                            const ctx = canvas.getContext('2d');
                            const blur = document.createElement('canvas');
                            blur.width = w; blur.height = h;
                            const bctx = blur.getContext('2d');
                            bctx.filter = 'blur(1.4px)';
                            bctx.drawImage(canvas, 0, 0);
                            const src = ctx.getImageData(0, 0, w, h), soft = bctx.getImageData(0, 0, w, h);
                            const d = src.data, s = soft.data, amount = 0.7;
                            for (let i = 0; i < d.length; i += 4) {
                                for (let k = 0; k < 3; k++) {
                                    const v = d[i + k] + amount * (d[i + k] - s[i + k]);
                                    d[i + k] = v < 0 ? 0 : v > 255 ? 255 : v;
                                }
                            }
                            ctx.putImageData(src, 0, 0);

                            const png = img.src.startsWith('data:image/png') || img.src.startsWith('data:image/webp');
                            img.src = png ? canvas.toDataURL('image/png') : canvas.toDataURL('image/jpeg', 0.93);
                            await img.decode();
                        } catch (e) { /* keep the original picture */ }
                    }
                }");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HEM Sampark PDF: the small picture could not be sharpened; the original is used.");
            }
        }
}
}
