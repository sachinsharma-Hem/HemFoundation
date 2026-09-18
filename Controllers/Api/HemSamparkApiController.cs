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

        private async Task<string> GenerateHemSamparkPdfAsync(DailySamparkEntry entry, MasterSetting master)
        {
            string GetBase64Image(string relativePath)
            {
                if (string.IsNullOrEmpty(relativePath)) return "";
                try
                {
                    var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.TrimStart('/'));
                    if (System.IO.File.Exists(physicalPath))
                    {
                        byte[] imageArray = System.IO.File.ReadAllBytes(physicalPath);
                        string ext = Path.GetExtension(physicalPath).Replace(".", "").ToLower();
                        return $"data:image/{ext};base64,{Convert.ToBase64String(imageArray)}";
                    }
                }
                catch { }
                return "";
            }

            string logo1Base64 = GetBase64Image(master?.Logo1Url);
            string logo2Base64 = GetBase64Image(master?.Logo2Url);
            string logo3Base64 = GetBase64Image(master?.Logo3Url);

            string questionsHtml = "";
            if (entry.Questions != null)
            {
                int qIdx = 1;
                foreach (var q in entry.Questions)
                {
                    questionsHtml += $@"
                    <p style='margin: 0 0 10px 0; font-size: 14px; line-height: 1.5; color: #222;'>
                        <b>Q{qIdx}. {q.QuestionText}</b><br/>
                        1) {q.Option1} &nbsp;|&nbsp; 2) {q.Option2} &nbsp;|&nbsp; 3) {q.Option3} &nbsp;|&nbsp; 4) {q.Option4}
                    </p>";
                    qIdx++;
                }
            }

            string formattedDate = entry.EntryDate.ToString("dddd, dd MMMM yyyy");

            string htmlContent = $@"
            <html>
            <head>
                <style>
                    body {{ font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; padding: 30px; color: #1a1a1a; background: #fff; }}
                    .header-table {{ width: 100%; border-bottom: 2px solid #1a237e; padding-bottom: 10px; margin-bottom: 15px; }}
                    .mantra-box {{ background-color: #fce4ec; border: 1px solid #f8bbd0; padding: 12px; border-radius: 4px; margin-bottom: 15px; }}
                    .section-title {{ font-size: 16px; font-weight: bold; color: #b71c1c; margin-bottom: 5px; text-transform: uppercase; }}
                    .content-text {{ font-size: 13px; line-height: 1.6; text-align: justify; margin-bottom: 15px; color: #333; }}
                    .challenge-box {{ background-color: #f1f8e9; border: 1px solid #c8e6c9; padding: 12px; border-radius: 4px; margin-bottom: 15px; }}
                    .footer-box {{ text-align: center; margin-top: 25px; border-top: 1px solid #ddd; padding-top: 15px; }}
                </style>
            </head>
            <body>
                <table class='header-table'>
                    <tr>
                        <td width='33%' align='left'>
                            {(!string.IsNullOrEmpty(logo1Base64) ? $"<img src='{logo1Base64}' style='max-height:45px; object-fit:contain;'/>" : "<b>RR GLOBAL</b>")}
                        </td>
                        <td width='34%' align='center'>
                            {(!string.IsNullOrEmpty(logo2Base64) ? $"<img src='{logo2Base64}' style='max-height:45px; object-fit:contain;'/>" : "<h4 style='margin:0; color:#1a237e;'>HEM सम्पर्क</h4>")}
                        </td>
                        <td width='33%' align='right'>
                            {(!string.IsNullOrEmpty(logo3Base64) ? $"<img src='{logo3Base64}' style='max-height:45px; object-fit:contain;'/>" : "<b>HEMA FOUNDATION</b>")}
                        </td>
                    </tr>
                </table>

                <table width='100%' style='margin-bottom: 15px; font-size: 13px; font-weight: bold; color: #333;'>
                    <tr>
                        <td>{formattedDate}</td>
                        <td style='text-align: right;'>{entry.VikramSamvat} | {entry.DayHindi} | Issue No: {entry.IssueNumber}</td>
                    </tr>
                </table>

                <div class='mantra-box'>
                    <div style='font-family:serif; font-size: 16px; font-weight: bold; color: #880e4f; margin-bottom: 5px;'>Morning Mantra</div>
                    <p style='margin: 0 0 5px 0; font-size: 13px;'>{master?.MorningMantraEn}</p>
                    <p style='margin: 0; font-size: 13px; color: #0d47a1;'>{master?.MorningMantraHi}</p>
                </div>

                <div class='section-title'>Celebrations Today</div>
                <h4 style='margin: 0 0 5px 0; color: #b71c1c;'>{entry.CelebrationHeadingEn}</h4>
                <div class='content-text'>{entry.CelebrationContentEn}</div>
                
                <h4 style='margin: 0 0 5px 0; color: #b71c1c;'>{entry.CelebrationHeadingHi}</h4>
                <div class='content-text'>{entry.CelebrationContentHi}</div>

                <div class='challenge-box'>
                    <div class='section-title' style='color: #2e7d32;'>Day's Challenge</div>
                    {questionsHtml}
                </div>

                <div class='mantra-box' style='background-color: #e8f5e9; border-color: #c8e6c9;'>
                    <div style='font-family:serif; font-size: 16px; font-weight: bold; color: #1b5e20; margin-bottom: 5px;'>Values in Focus</div>
                    <p style='margin: 0 0 5px 0; font-size: 13px;'>{master?.ValuesInFocusEn}</p>
                    <p style='margin: 0; font-size: 13px; color: #0d47a1;'>{master?.ValuesInFocusHi}</p>
                </div>

                <div class='footer-box'>
                    <a href='https://{master?.FooterLink}' target='_blank' style='color: #1a237e; font-weight: bold; text-decoration: none;'>{master?.FooterLink}</a>
                    <p style='color: #b71c1c; font-weight: bold; font-size: 13px; margin: 5px 0 0 0;'>{master?.FooterText}</p>
                </div>
            </body>
            </html>";

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);

            string folderName = "generateHemPdf";
            string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, folderName);
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

            string fileName = $"HemSampark_{entry.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(uploadDir, fileName);

            await page.PdfAsync(filePath, new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions { Top = "15px", Bottom = "15px", Left = "15px", Right = "15px" }
            });

            return $"/{folderName}/{fileName}";
        }
    }
}