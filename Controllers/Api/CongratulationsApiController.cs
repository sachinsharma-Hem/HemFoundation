using HemFoundation.Data;
using HemFoundation.Models.Congratulations;
using HemFoundation.Services.HemSampark;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HemFoundation.Controllers.Api
{
    /// <summary>Requires a logged-in portal user (server session), like the page itself.</summary>
    public class CongratulationsLoginAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var http = context.HttpContext;

            if (http.Session.GetString("IsAdminLoggedIn") != "true")
            {
                context.Result = new JsonResult(new { success = false, message = "Your session has expired. Please log in again." })
                { StatusCode = StatusCodes.Status401Unauthorized };
                return;
            }

            // State-changing calls must come from the page (a cross-site form cannot send this header).
            if (!HttpMethods.IsGet(http.Request.Method) && !http.Request.Headers.ContainsKey("X-Requested-With"))
            {
                context.Result = new JsonResult(new { success = false, message = "Invalid request." })
                { StatusCode = StatusCodes.Status400BadRequest };
            }
        }
    }

    /// <summary>
    /// Birthday / anniversary contact list, upcoming occasions, generated cards
    /// and the photo taken when a card is handed over.
    /// </summary>
    [Route("api/congratulations")]
    [ApiController]
    [CongratulationsLogin]
    public class CongratulationsApiController : ControllerBase
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] PhotoExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif" };
        private const long MaxPhotoBytes = 12 * 1024 * 1024;

        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly HemSamparkClock _clock;
        private readonly ILogger<CongratulationsApiController> _logger;

        public CongratulationsApiController(
            ApplicationDbContext db, IWebHostEnvironment env, HemSamparkClock clock,
            ILogger<CongratulationsApiController> logger)
        {
            _db = db;
            _env = env;
            _clock = clock;
            _logger = logger;
        }

        private string? CurrentUser =>
            HttpContext.Session.GetString("UserEmail") is { Length: > 0 } email ? email : HttpContext.Session.GetString("UserId");

        private IActionResult Fail(int status, string message) => StatusCode(status, new { success = false, message });

        private string PhotoFolder
        {
            get
            {
                var folder = Path.Combine(_env.ContentRootPath, "App_Data", "Congratulations");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        // ---------------------------------------------------------------
        // Dates
        // ---------------------------------------------------------------

        /// <summary>The occasion's date in a given year (29 Feb becomes 28 Feb in other years).</summary>
        private static DateTime OccasionIn(int year, int month, int day) =>
            new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

        /// <summary>Next time the occasion happens, today included.</summary>
        private static DateTime NextOccurrence(DateTime today, int month, int day)
        {
            var date = OccasionIn(today.Year, month, day);
            return date >= today ? date : OccasionIn(today.Year + 1, month, day);
        }

        private static string Label(DateTime d) => d.ToString("dd MMM yyyy", Inv);

        private object ContactDto(CongratulationContact c, DateTime today)
        {
            var next = NextOccurrence(today, c.OccasionMonth, c.OccasionDay);
            return new
            {
                c.Id,
                name = c.FullName,
                occasion = c.Occasion,
                day = c.OccasionDay,
                month = c.OccasionMonth,
                year = c.OccasionYear,
                date = c.OccasionYear is > 0
                    ? new DateTime(c.OccasionYear.Value, c.OccasionMonth, Math.Min(c.OccasionDay, DateTime.DaysInMonth(c.OccasionYear.Value, c.OccasionMonth))).ToString("yyyy-MM-dd")
                    : null,
                dayMonth = new DateTime(2000, c.OccasionMonth, c.OccasionDay).ToString("dd MMM", Inv),
                c.Mobile,
                c.Notes,
                nextDate = next.ToString("yyyy-MM-dd"),
                nextLabel = Label(next),
                daysLeft = (int)(next - today).TotalDays
            };
        }

        private object WishDto(CongratulationWish w) => new
        {
            w.Id,
            contactId = w.ContactId,
            name = w.NameOnCard,
            occasion = w.Occasion,
            date = w.WishDate.ToString("yyyy-MM-dd"),
            dateLabel = Label(w.WishDate),
            w.Status,
            generatedAt = w.GeneratedAt.ToString("dd MMM yyyy, hh:mm tt", Inv),
            deliveredAt = w.DeliveredAt?.ToString("dd MMM yyyy, hh:mm tt", Inv),
            photoUrl = string.IsNullOrEmpty(w.PhotoPath)
                ? null
                : $"/api/congratulations/wishes/{w.Id}/photo?v={w.DeliveredAt?.Ticks ?? 0}"
        };

        // ---------------------------------------------------------------
        // Contacts
        // ---------------------------------------------------------------

        public class ContactSaveDto
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Occasion { get; set; }
            public string? Date { get; set; }   // yyyy-MM-dd (the year is optional: send 2000 when unknown)
            public bool YearKnown { get; set; } = true;
            public string? Mobile { get; set; }
            public string? Notes { get; set; }
        }

        private static string? ValidateContact(ContactSaveDto m, out string occasion, out DateTime date)
        {
            occasion = CongratulationOccasion.Normalize(m.Occasion) ?? "";
            date = default;

            if (string.IsNullOrWhiteSpace(m.Name)) return "Please enter the name.";
            if (m.Name.Trim().Length > 120) return "The name must be 120 characters or fewer.";
            if (occasion.Length == 0) return "Choose Birthday or Anniversary.";
            if (!DateTime.TryParseExact(m.Date, "yyyy-MM-dd", Inv, DateTimeStyles.None, out date))
                return "Please choose a valid date.";
            if ((m.Mobile?.Length ?? 0) > 20) return "The mobile number is too long.";
            return null;
        }

        [HttpGet("contacts")]
        public async Task<IActionResult> GetContacts(
            [FromQuery] string? search, [FromQuery] string? occasion, [FromQuery] int? month,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);
            var today = _clock.Today;

            var query = _db.CongratulationContacts.AsNoTracking().Where(c => c.IsActive);

            var occ = CongratulationOccasion.Normalize(occasion);
            if (occ != null) query = query.Where(c => c.Occasion == occ);
            if (month is >= 1 and <= 12) query = query.Where(c => c.OccasionMonth == month);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                query = query.Where(c => EF.Functions.ILike(c.FullName, pattern, "\\")
                                         || EF.Functions.ILike(c.Mobile ?? "", pattern, "\\"));
            }

            // Sorted by the next date it comes round, so the nearest is first.
            var all = await query.ToListAsync();
            var sorted = all
                .Select(c => new { c, next = NextOccurrence(today, c.OccasionMonth, c.OccasionDay) })
                .OrderBy(x => x.next).ThenBy(x => x.c.FullName)
                .ToList();

            return Ok(new
            {
                success = true,
                total = sorted.Count,
                page,
                pageSize,
                items = sorted.Skip((page - 1) * pageSize).Take(pageSize).Select(x => ContactDto(x.c, today))
            });
        }

        [HttpPost("contacts")]
        public async Task<IActionResult> SaveContact([FromBody] ContactSaveDto model)
        {
            if (model == null) return Fail(400, "Invalid request.");

            var error = ValidateContact(model, out var occasion, out var date);
            if (error != null) return Fail(400, error);

            string name = model.Name!.Trim();

            // The same person and occasion is kept once.
            var duplicate = await _db.CongratulationContacts.AnyAsync(c =>
                c.IsActive && c.Id != model.Id && c.Occasion == occasion &&
                c.OccasionDay == date.Day && c.OccasionMonth == date.Month &&
                c.FullName.ToLower() == name.ToLower());

            if (duplicate) return Fail(409, $"{name} is already in the list for this {occasion.ToLower()}.");

            CongratulationContact contact;

            if (model.Id > 0)
            {
                var found = await _db.CongratulationContacts.FirstOrDefaultAsync(c => c.Id == model.Id && c.IsActive);
                if (found == null) return Fail(404, "Contact not found.");
                contact = found;
                contact.UpdatedAt = _clock.Now;
            }
            else
            {
                contact = new CongratulationContact { CreatedAt = _clock.Now, CreatedBy = CurrentUser };
                _db.CongratulationContacts.Add(contact);
            }

            contact.FullName = name;
            contact.Occasion = occasion;
            contact.OccasionDay = (short)date.Day;
            contact.OccasionMonth = (short)date.Month;
            contact.OccasionYear = model.YearKnown ? (short)date.Year : null;
            contact.Mobile = string.IsNullOrWhiteSpace(model.Mobile) ? null : model.Mobile.Trim();
            contact.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = model.Id > 0 ? "Contact updated." : "Contact added.",
                contact = ContactDto(contact, _clock.Today)
            });
        }

        [HttpDelete("contacts/{id:int}")]
        public async Task<IActionResult> DeleteContact(int id)
        {
            var contact = await _db.CongratulationContacts.FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
            if (contact == null) return Fail(404, "Contact not found.");

            // Kept for the history of cards already given; hidden from the list.
            contact.IsActive = false;
            contact.UpdatedAt = _clock.Now;
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "Contact removed from the list." });
        }

        // ---------------------------------------------------------------
        // Upcoming occasions
        // ---------------------------------------------------------------

        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcoming([FromQuery] int days = 7)
        {
            days = Math.Clamp(days, 1, 60);
            var today = _clock.Today;
            var until = today.AddDays(days - 1);

            var contacts = await _db.CongratulationContacts.AsNoTracking().Where(c => c.IsActive).ToListAsync();

            var upcoming = contacts
                .Select(c => new { c, next = NextOccurrence(today, c.OccasionMonth, c.OccasionDay) })
                .Where(x => x.next <= until)
                .OrderBy(x => x.next).ThenBy(x => x.c.FullName)
                .ToList();

            var ids = upcoming.Select(x => x.c.Id).ToList();
            var wishes = await _db.CongratulationWishes.AsNoTracking()
                .Where(w => w.ContactId != null && ids.Contains(w.ContactId.Value) && w.WishDate >= today && w.WishDate <= until)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                today = today.ToString("yyyy-MM-dd"),
                todayLabel = today.ToString("dddd, dd MMMM yyyy", Inv),
                items = upcoming.Select(x =>
                {
                    var wish = wishes.FirstOrDefault(w => w.ContactId == x.c.Id && w.Occasion == x.c.Occasion && w.WishDate == x.next);
                    return new
                    {
                        contact = ContactDto(x.c, today),
                        wish = wish == null ? null : WishDto(wish)
                    };
                })
            });
        }

        // ---------------------------------------------------------------
        // Cards
        // ---------------------------------------------------------------

        public class WishCreateDto
        {
            public int? ContactId { get; set; }
            public string? Name { get; set; }
            public string? Occasion { get; set; }
            public string? Date { get; set; }         // the occasion date shown on the card (yyyy-MM-dd)
            public string? Mobile { get; set; }
            public bool SaveContact { get; set; } = true;
        }

        /// <summary>
        /// Records that a card was made (the picture itself is drawn in the
        /// browser). The person is added to the contact list unless asked not to.
        /// Making the card again for the same occasion and date reuses the record.
        /// </summary>
        [HttpPost("wishes")]
        public async Task<IActionResult> CreateWish([FromBody] WishCreateDto model)
        {
            if (model == null) return Fail(400, "Invalid request.");

            var check = new ContactSaveDto { Name = model.Name, Occasion = model.Occasion, Date = model.Date, Mobile = model.Mobile };
            var error = ValidateContact(check, out var occasion, out var date);
            if (error != null) return Fail(400, error);

            string name = model.Name!.Trim();
            var today = _clock.Today;

            // The card is for the next time this date comes round (today included).
            var wishDate = date >= today ? date : NextOccurrence(today, date.Month, date.Day);

            CongratulationContact? contact = null;

            if (model.ContactId is > 0)
            {
                contact = await _db.CongratulationContacts.FirstOrDefaultAsync(c => c.Id == model.ContactId && c.IsActive);
            }

            if (contact == null && model.SaveContact)
            {
                contact = await _db.CongratulationContacts.FirstOrDefaultAsync(c =>
                    c.IsActive && c.Occasion == occasion && c.OccasionDay == date.Day &&
                    c.OccasionMonth == date.Month && c.FullName.ToLower() == name.ToLower());

                if (contact == null)
                {
                    contact = new CongratulationContact
                    {
                        FullName = name,
                        Occasion = occasion,
                        OccasionDay = (short)date.Day,
                        OccasionMonth = (short)date.Month,
                        OccasionYear = null,
                        Mobile = string.IsNullOrWhiteSpace(model.Mobile) ? null : model.Mobile.Trim(),
                        CreatedAt = _clock.Now,
                        CreatedBy = CurrentUser
                    };
                    _db.CongratulationContacts.Add(contact);
                    await _db.SaveChangesAsync();
                }
            }

            CongratulationWish? wish = null;

            if (contact != null)
            {
                wish = await _db.CongratulationWishes.FirstOrDefaultAsync(w =>
                    w.ContactId == contact.Id && w.Occasion == occasion && w.WishDate == wishDate);
            }

            if (wish == null)
            {
                wish = new CongratulationWish
                {
                    ContactId = contact?.Id,
                    Occasion = occasion,
                    WishDate = wishDate,
                    Status = CongratulationWish.Generated,
                    GeneratedAt = _clock.Now,
                    GeneratedBy = CurrentUser
                };
                _db.CongratulationWishes.Add(wish);
            }

            wish.NameOnCard = name;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = contact != null && model.SaveContact ? "Card ready. Saved in the contact list." : "Card ready.",
                wish = WishDto(wish),
                contact = contact == null ? null : ContactDto(contact, today)
            });
        }

        [HttpGet("wishes")]
        public async Task<IActionResult> GetWishes(
            [FromQuery] string? status, [FromQuery] string? search,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);

            var query = _db.CongratulationWishes.AsNoTracking().AsQueryable();

            if (string.Equals(status, CongratulationWish.Delivered, StringComparison.OrdinalIgnoreCase))
                query = query.Where(w => w.Status == CongratulationWish.Delivered);
            else if (string.Equals(status, CongratulationWish.Generated, StringComparison.OrdinalIgnoreCase))
                query = query.Where(w => w.Status == CongratulationWish.Generated);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                query = query.Where(w => EF.Functions.ILike(w.NameOnCard, pattern, "\\"));
            }

            int total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(w => w.DeliveredAt ?? w.GeneratedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            return Ok(new { success = true, total, page, pageSize, items = rows.Select(WishDto) });
        }

        /// <summary>Photo taken when the card was handed over (gallery or camera).</summary>
        [HttpPost("wishes/{id:int}/photo")]
        [RequestSizeLimit(MaxPhotoBytes + 1024 * 1024)]
        public async Task<IActionResult> UploadPhoto(int id, [FromForm] IFormFile? photo)
        {
            var wish = await _db.CongratulationWishes.FirstOrDefaultAsync(w => w.Id == id);
            if (wish == null) return Fail(404, "Card not found.");

            if (photo == null || photo.Length == 0) return Fail(400, "Please choose or take a photo.");
            if (photo.Length > MaxPhotoBytes) return Fail(400, "The photo must be 12 MB or smaller.");

            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) && (photo.ContentType ?? "").StartsWith("image/")) ext = ".jpg";
            if (!PhotoExtensions.Contains(ext)) return Fail(400, "Only photos (JPG, PNG, WEBP, HEIC) can be uploaded.");

            string fileName = $"{Guid.NewGuid():N}{ext}";
            await using (var stream = new FileStream(Path.Combine(PhotoFolder, fileName), FileMode.CreateNew))
                await photo.CopyToAsync(stream);

            string? previous = wish.PhotoPath;

            wish.PhotoPath = fileName;
            wish.Status = CongratulationWish.Delivered;
            wish.DeliveredAt = _clock.Now;
            wish.DeliveredBy = CurrentUser;
            await _db.SaveChangesAsync();

            if (!string.IsNullOrEmpty(previous))
            {
                try { System.IO.File.Delete(Path.Combine(PhotoFolder, Path.GetFileName(previous))); }
                catch (Exception ex) { _logger.LogWarning(ex, "Congratulations: old photo not removed"); }
            }

            return Ok(new { success = true, message = "Photo saved. Marked as given.", wish = WishDto(wish) });
        }

        [HttpGet("wishes/{id:int}/photo")]
        public async Task<IActionResult> GetPhoto(int id)
        {
            var path = await _db.CongratulationWishes.AsNoTracking()
                .Where(w => w.Id == id).Select(w => w.PhotoPath).FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(path)) return NotFound();

            var full = Path.Combine(PhotoFolder, Path.GetFileName(path));
            if (!System.IO.File.Exists(full)) return NotFound();

            var type = Path.GetExtension(full).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".heic" => "image/heic",
                ".heif" => "image/heif",
                _ => "image/jpeg"
            };

            Response.Headers.CacheControl = "private, max-age=3600";
            return PhysicalFile(full, type);
        }

        /// <summary>
        /// Wraps the card picture drawn in the browser into a one-page PDF
        /// (same size and look as the JPEG).
        /// </summary>
        [HttpPost("card-pdf")]
        [RequestSizeLimit(15 * 1024 * 1024)]
        public async Task<IActionResult> CardPdf([FromForm] IFormFile? image, [FromForm] string? fileName)
        {
            if (image == null || image.Length == 0) return Fail(400, "No card picture received.");
            if (image.Length > 12 * 1024 * 1024) return Fail(400, "The card picture is too large.");

            byte[] bytes;
            await using (var ms = new MemoryStream())
            {
                await image.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            try
            {
                using var doc = new PdfSharpCore.Pdf.PdfDocument();
                var page = doc.AddPage();

                using var picture = PdfSharpCore.Drawing.XImage.FromStream(() => new MemoryStream(bytes));

                // Page = 297 mm wide, height from the card's own proportions.
                double widthPt = 297 / 25.4 * 72;
                page.Width = PdfSharpCore.Drawing.XUnit.FromPoint(widthPt);
                page.Height = PdfSharpCore.Drawing.XUnit.FromPoint(widthPt * picture.PixelHeight / picture.PixelWidth);

                using (var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page))
                    gfx.DrawImage(picture, 0, 0, page.Width.Point, page.Height.Point);

                await using var output = new MemoryStream();
                doc.Save(output, false);

                string safe = string.Concat((fileName ?? "card").Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or ' ')).Trim();
                if (safe.Length == 0) safe = "card";

                return File(output.ToArray(), "application/pdf", safe + ".pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Congratulations: PDF could not be made");
                return Fail(400, "The PDF could not be made from this picture.");
            }
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var today = _clock.Today;
            var contacts = await _db.CongratulationContacts.AsNoTracking().Where(c => c.IsActive)
                .Select(c => new { c.Occasion, c.OccasionMonth, c.OccasionDay }).ToListAsync();

            int next7 = contacts.Count(c => NextOccurrence(today, c.OccasionMonth, c.OccasionDay) <= today.AddDays(6));
            int todayCount = contacts.Count(c => NextOccurrence(today, c.OccasionMonth, c.OccasionDay) == today);

            return Ok(new
            {
                success = true,
                contacts = contacts.Count,
                birthdays = contacts.Count(c => c.Occasion == CongratulationOccasion.Birthday),
                anniversaries = contacts.Count(c => c.Occasion == CongratulationOccasion.Anniversary),
                today = todayCount,
                next7,
                delivered = await _db.CongratulationWishes.CountAsync(w => w.Status == CongratulationWish.Delivered),
                pending = await _db.CongratulationWishes.CountAsync(w => w.Status == CongratulationWish.Generated)
            });
        }
    }
}
