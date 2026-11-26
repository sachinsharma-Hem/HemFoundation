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
                c.Email,
                c.Address,
                c.City,
                c.Pincode,
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
                : $"/api/congratulations/wishes/{w.Id}/photo?v={w.DeliveredAt?.Ticks ?? 0}",
            prasadDate = w.PrasadDeliveredOn?.ToString("yyyy-MM-dd"),
            prasadLabel = w.PrasadDeliveredOn?.ToString("dd MMM yyyy", Inv)
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
            // Optional details.
            public string? Email { get; set; }
            public string? Address { get; set; }
            public string? City { get; set; }
            public string? Pincode { get; set; }
        }

        private static readonly System.Text.RegularExpressions.Regex EmailPattern =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>Copies the optional details; with onlyFilled, empty values keep what is saved.</summary>
        private static void ApplyDetails(CongratulationContact c, ContactSaveDto m, bool onlyFilled = false)
        {
            void Set(string? value, Action<string?> assign)
            {
                var v = Clean(value);
                if (v != null || !onlyFilled) assign(v);
            }

            Set(m.Mobile, v => c.Mobile = v);
            Set(m.Notes, v => c.Notes = v);
            Set(m.Email, v => c.Email = v);
            Set(m.Address, v => c.Address = v);
            Set(m.City, v => c.City = v);
            Set(m.Pincode, v => c.Pincode = v);
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
            if ((m.Mobile?.Trim().Length ?? 0) > 20) return "The mobile number is too long.";
            if ((m.Notes?.Trim().Length ?? 0) > 250) return "The notes must be 250 characters or fewer.";

            var email = Clean(m.Email);
            if (email != null && (email.Length > 150 || !EmailPattern.IsMatch(email))) return "Please enter a valid e-mail address.";
            if ((m.Address?.Trim().Length ?? 0) > 300) return "The address must be 300 characters or fewer.";
            if ((m.City?.Trim().Length ?? 0) > 80) return "The city must be 80 characters or fewer.";

            var pin = Clean(m.Pincode);
            if (pin != null && (pin.Length > 10 || !pin.All(ch => char.IsDigit(ch) || ch == ' ')))
                return "The PIN code must contain digits only.";
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
                                         || EF.Functions.ILike(c.Mobile ?? "", pattern, "\\")
                                         || EF.Functions.ILike(c.Email ?? "", pattern, "\\")
                                         || EF.Functions.ILike(c.City ?? "", pattern, "\\"));
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
            ApplyDetails(contact, model);

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
        // Excel import / template
        // ---------------------------------------------------------------

        private const long MaxImportBytes = 5 * 1024 * 1024;
        private const int MaxImportRows = 5000;

        private static readonly string[] TemplateHeaders =
            { "Name", "Occasion", "Date", "Mobile", "Email", "Address", "City", "Pincode", "Notes" };

        /// <summary>Header text → field. Spaces, dots and case are ignored.</summary>
        private static readonly Dictionary<string, string> HeaderAliases = new()
        {
            ["name"] = "Name", ["fullname"] = "Name", ["naam"] = "Name", ["nameoncard"] = "Name", ["नाम"] = "Name",
            ["occasion"] = "Occasion", ["type"] = "Occasion", ["event"] = "Occasion", ["अवसर"] = "Occasion",
            ["date"] = "Date", ["dob"] = "Date", ["dateofbirth"] = "Date", ["birthdate"] = "Date", ["birthday"] = "Date",
            ["anniversary"] = "Date", ["anniversarydate"] = "Date", ["occasiondate"] = "Date", ["तारीख"] = "Date", ["तिथि"] = "Date",
            ["mobile"] = "Mobile", ["mobileno"] = "Mobile", ["mobilenumber"] = "Mobile", ["phone"] = "Mobile", ["phoneno"] = "Mobile",
            ["contact"] = "Mobile", ["contactno"] = "Mobile", ["मोबाइल"] = "Mobile",
            ["email"] = "Email", ["emailid"] = "Email", ["emailaddress"] = "Email", ["mail"] = "Email",
            ["address"] = "Address", ["पता"] = "Address",
            ["city"] = "City", ["शहर"] = "City",
            ["pincode"] = "Pincode", ["pin"] = "Pincode", ["postalcode"] = "Pincode", ["zip"] = "Pincode", ["zipcode"] = "Pincode",
            ["notes"] = "Notes", ["note"] = "Notes", ["remarks"] = "Notes", ["designation"] = "Notes"
        };

        private static readonly string[] DateFormatsWithYear =
        {
            "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "yyyy/MM/dd",
            "dd MMM yyyy", "d MMM yyyy", "dd-MMM-yyyy", "d-MMM-yyyy", "dd MMMM yyyy", "d MMMM yyyy", "MMM d, yyyy", "MMMM d, yyyy",
            "dd-MM-yy", "dd/MM/yy"
        };

        private static readonly string[] DateFormatsNoYear =
            { "dd-MM", "d-M", "dd/MM", "d/M", "dd.MM", "d.M", "dd MMM", "d MMM", "dd-MMM", "d-MMM", "dd MMMM", "d MMMM", "MMM d", "MMMM d" };

        private static string? OccasionFromText(string? text)
        {
            var t = (text ?? "").Trim().ToLowerInvariant();
            if (t.Length == 0) return null;
            if (t.Contains("birth") || t.Contains("जन्म") || t == "b" || t == "bday") return CongratulationOccasion.Birthday;
            if (t.Contains("anniv") || t.Contains("marriage") || t.Contains("wedding") || t.Contains("वर्ष") || t.Contains("शादी") || t.Contains("विवाह") || t == "a")
                return CongratulationOccasion.Anniversary;
            return null;
        }

        /// <summary>Reads a date cell: a real Excel date, or text such as 25-11-1980 or 25 Nov (no year).</summary>
        private static bool TryReadDate(ClosedXML.Excel.IXLCell cell, out DateTime date, out bool yearKnown)
        {
            date = default;
            yearKnown = true;

            if (cell.DataType == ClosedXML.Excel.XLDataType.DateTime && cell.TryGetValue(out DateTime d))
            {
                date = d.Date;
                return true;
            }

            if (cell.DataType == ClosedXML.Excel.XLDataType.Number && cell.TryGetValue(out double serial) && serial > 0 && serial < 2958466)
            {
                date = DateTime.FromOADate(serial).Date;
                return true;
            }

            var text = cell.GetFormattedString().Trim();
            if (text.Length == 0) return false;

            if (DateTime.TryParseExact(text, DateFormatsWithYear, Inv, DateTimeStyles.AllowWhiteSpaces, out date)) return true;

            if (DateTime.TryParseExact(text, DateFormatsNoYear, Inv, DateTimeStyles.AllowWhiteSpaces, out var noYear))
            {
                yearKnown = false;
                date = new DateTime(2000, noYear.Month, noYear.Day);   // 2000 is a leap year, so 29 Feb is kept
                return true;
            }

            return false;
        }

        private static string CellText(ClosedXML.Excel.IXLCell cell)
        {
            if (cell.DataType == ClosedXML.Excel.XLDataType.Number && cell.TryGetValue(out double n))
                return n.ToString("0.##########", Inv);   // mobile numbers and PIN codes without "E+" or ".0"
            return cell.GetFormattedString().Trim();
        }

        [HttpGet("contacts/template")]
        public IActionResult DownloadTemplate()
        {
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.AddWorksheet("Contacts");

            for (int i = 0; i < TemplateHeaders.Length; i++)
            {
                var h = ws.Cell(1, i + 1);
                h.Value = TemplateHeaders[i];
                h.Style.Font.Bold = true;
                h.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#FDE2E3");
            }

            object[][] samples =
            {
                new object[] { "डॉ. चेतना जी गट्टानी", "Birthday", new DateTime(1975, 11, 23), "9826012345", "chetna@example.com", "12, MG Road", "Indore", "452001", "Trustee" },
                new object[] { "श्री एवं श्रीमती राजेश शर्मा जी", "Anniversary", new DateTime(1998, 11, 23), "9893011111", "", "", "Bhopal", "", "" },
                new object[] { "Mr. Rohan Gupta", "Birthday", "29-11", "", "", "", "", "", "Year not known: write only day-month" }
            };

            for (int r = 0; r < samples.Length; r++)
                for (int c = 0; c < samples[r].Length; c++)
                {
                    var cell = ws.Cell(r + 2, c + 1);
                    switch (samples[r][c])
                    {
                        case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = "dd-mm-yyyy"; break;
                        default: cell.SetValue(samples[r][c]?.ToString() ?? ""); break;
                    }
                }

            ws.Column(4).Style.NumberFormat.Format = "@";   // mobile as text
            ws.Column(8).Style.NumberFormat.Format = "@";   // PIN code as text
            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            var help = wb.AddWorksheet("How to fill");
            string[] lines =
            {
                "How to fill the Contacts sheet",
                "",
                "Name and Occasion and Date are required. All other columns are optional.",
                "Occasion: Birthday or Anniversary (जन्मदिन / वर्षगाँठ also works).",
                "Date: 25-11-1980, 25/11/1980 or 25 Nov 1980. If the year is not known, write only 25-11 or 25 Nov.",
                "Mobile, Email, Address, City, Pincode, Notes: optional.",
                "A person already in the list (same name, occasion and date) is not added twice; empty details are filled in.",
                "Keep the first row (the column names). Delete the sample rows before uploading.",
                "Up to 5,000 rows, file size up to 5 MB, .xlsx format."
            };
            for (int i = 0; i < lines.Length; i++) help.Cell(i + 1, 1).Value = lines[i];
            help.Cell(1, 1).Style.Font.Bold = true;
            help.Column(1).Width = 110;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Congratulations-Contacts-Template.xlsx");
        }

        [HttpPost("contacts/import")]
        [RequestSizeLimit(MaxImportBytes + 1024 * 1024)]
        public async Task<IActionResult> ImportContacts(IFormFile? file)
        {
            if (file == null || file.Length == 0) return Fail(400, "Please choose an Excel file (.xlsx).");
            if (file.Length > MaxImportBytes) return Fail(400, "The file must be 5 MB or smaller.");
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return Fail(400, "Only .xlsx files can be uploaded. In Excel use File → Save As → Excel Workbook (.xlsx).");

            ClosedXML.Excel.XLWorkbook wb;
            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Position = 0;
                wb = new ClosedXML.Excel.XLWorkbook(ms);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Congratulations import: unreadable file");
                return Fail(400, "The file could not be read. Please upload a valid .xlsx file.");
            }

            using (wb)
            {
                var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Equals("Contacts", StringComparison.OrdinalIgnoreCase)) ?? wb.Worksheets.First();
                var used = ws.RangeUsed();
                if (used == null) return Fail(400, "The sheet is empty.");

                int firstRow = used.FirstRow().RowNumber(), lastRow = used.LastRow().RowNumber();
                int lastCol = used.LastColumn().ColumnNumber();

                // Map the header row.
                var columns = new Dictionary<string, int>();
                for (int c = 1; c <= lastCol; c++)
                {
                    var key = new string(ws.Cell(firstRow, c).GetFormattedString().Trim().ToLowerInvariant()
                        .Where(ch => !char.IsWhiteSpace(ch) && ch != '.' && ch != '_' && ch != '-' && ch != '/' && ch != '*').ToArray());
                    if (HeaderAliases.TryGetValue(key, out var field) && !columns.ContainsKey(field)) columns[field] = c;
                }

                if (!columns.ContainsKey("Name") || !columns.ContainsKey("Date"))
                    return Fail(400, "The first row must have the column names. Name and Date are required (download the sample file).");
                if (lastRow - firstRow > MaxImportRows) return Fail(400, $"Up to {MaxImportRows:N0} rows can be uploaded at a time.");

                string Text(int row, string field) => columns.TryGetValue(field, out var c) ? CellText(ws.Cell(row, c)) : "";

                var existing = await _db.CongratulationContacts.Where(x => x.IsActive).ToListAsync();
                string Key(string name, string occ, int day, int month) => $"{name.Trim().ToLowerInvariant()}|{occ}|{day}|{month}";
                var byKey = new Dictionary<string, CongratulationContact>();
                foreach (var x in existing) byKey.TryAdd(Key(x.FullName, x.Occasion, x.OccasionDay, x.OccasionMonth), x);

                int added = 0, updated = 0, empty = 0;
                var errors = new List<object>();
                int errorCount = 0;
                var now = _clock.Now;

                for (int row = firstRow + 1; row <= lastRow; row++)
                {
                    var name = Text(row, "Name");
                    var occText = Text(row, "Occasion");
                    var dateCell = ws.Cell(row, columns["Date"]);

                    if (name.Length == 0 && occText.Length == 0 && dateCell.IsEmpty()) { empty++; continue; }

                    void Error(string message)
                    {
                        errorCount++;
                        if (errors.Count < 100) errors.Add(new { row, name, message });
                    }

                    // Without an Occasion column every row is a birthday.
                    var occasion = columns.ContainsKey("Occasion") ? OccasionFromText(occText) : CongratulationOccasion.Birthday;
                    if (occasion == null) { Error("Occasion must be Birthday or Anniversary."); continue; }
                    if (!TryReadDate(dateCell, out var date, out var yearKnown)) { Error("The date could not be read (use 25-11-1980 or 25-11)."); continue; }
                    if (date.Year < 1900 || date.Year > _clock.Today.Year + 1) { yearKnown = false; date = new DateTime(2000, date.Month, date.Day); }

                    var dto = new ContactSaveDto
                    {
                        Name = name, Occasion = occasion, Date = date.ToString("yyyy-MM-dd", Inv), YearKnown = yearKnown,
                        Mobile = Text(row, "Mobile"), Email = Text(row, "Email"), Address = Text(row, "Address"),
                        City = Text(row, "City"), Pincode = Text(row, "Pincode"), Notes = Text(row, "Notes")
                    };

                    var error = ValidateContact(dto, out _, out _);
                    if (error != null) { Error(error); continue; }

                    var key = Key(name, occasion, date.Day, date.Month);
                    if (byKey.TryGetValue(key, out var found))
                    {
                        // Already in the list: fill in the details given in the file.
                        ApplyDetails(found, dto, onlyFilled: true);
                        if (yearKnown && found.OccasionYear == null) found.OccasionYear = (short)date.Year;
                        if (found.Id > 0) found.UpdatedAt = now;
                        updated++;
                        continue;
                    }

                    var contact = new CongratulationContact
                    {
                        FullName = name.Trim(),
                        Occasion = occasion,
                        OccasionDay = (short)date.Day,
                        OccasionMonth = (short)date.Month,
                        OccasionYear = yearKnown ? (short)date.Year : null,
                        CreatedAt = now,
                        CreatedBy = CurrentUser
                    };
                    ApplyDetails(contact, dto);
                    _db.CongratulationContacts.Add(contact);
                    byKey[key] = contact;
                    added++;
                }

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    added,
                    updated,
                    failed = errorCount,
                    errors,
                    message = $"{added} added, {updated} already in the list (details updated), {errorCount} not added."
                });
            }
        }

        // ---------------------------------------------------------------
        // English → Hindi for the name on the card
        // ---------------------------------------------------------------

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> HindiCache = new();

        // When the service cannot be reached it is not asked again for 10 minutes,
        // so typing stays fast on a server without internet access.
        private static DateTime _hindiOfflineUntil = DateTime.MinValue;

        /// <summary>
        /// Turns English words into Hindi (Devanagari) with Google Input Tools when the server
        /// can reach it. Words it cannot convert come back as null and the page converts them itself.
        /// </summary>
        [HttpGet("transliterate")]
        public async Task<IActionResult> Transliterate([FromQuery] string? text, [FromServices] IHttpClientFactory httpFactory, [FromServices] IConfiguration config)
        {
            var words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim()).Where(w => w.Length is > 0 and <= 40 && w.All(ch => ch < 128 && char.IsLetter(ch)))
                .Select(w => w.ToLowerInvariant()).Distinct().Take(12).ToList();

            var result = new Dictionary<string, string?>();
            if (words.Count == 0) return Ok(new { success = true, online = true, words = result });
            if (DateTime.UtcNow < _hindiOfflineUntil) return Ok(new { success = true, online = false, words = result });
            bool reached = false;

            string endpoint = config["Congratulations:TransliterateUrl"] ?? "https://inputtools.google.com/request";
            var client = httpFactory.CreateClient("CongratulationsHindi");
            client.Timeout = TimeSpan.FromSeconds(4);

            await Task.WhenAll(words.Select(async w =>
            {
                if (HindiCache.TryGetValue(w, out var cached)) { lock (result) result[w] = cached; return; }
                string? hindi = null;
                try
                {
                    var url = $"{endpoint}?text={Uri.EscapeDataString(w)}&itc=hi-t-i0-und&num=1&cp=0&cs=1&ie=utf-8&oe=utf-8";
                    using var doc = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync(url));
                    reached = true;
                    var root = doc.RootElement;
                    if (root[0].GetString() == "SUCCESS")
                        hindi = root[1][0][1][0].GetString();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Hindi conversion not available for {Word}", w);
                }

                if (!string.IsNullOrWhiteSpace(hindi)) HindiCache.TryAdd(w, hindi);
                lock (result) result[w] = string.IsNullOrWhiteSpace(hindi) ? null : hindi;
            }));

            if (!reached && !words.All(HindiCache.ContainsKey)) _hindiOfflineUntil = DateTime.UtcNow.AddMinutes(10);
            return Ok(new { success = true, online = reached || words.All(HindiCache.ContainsKey), words = result });
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

        public class PrasadDto { public string? Date { get; set; } }

        /// <summary>Saves (or clears, with an empty date) the day the prasad was given.</summary>
        [HttpPost("wishes/{id:int}/prasad")]
        public async Task<IActionResult> SavePrasad(int id, [FromBody] PrasadDto model)
        {
            var wish = await _db.CongratulationWishes.FirstOrDefaultAsync(w => w.Id == id);
            if (wish == null) return Fail(404, "Card not found.");

            if (string.IsNullOrWhiteSpace(model?.Date))
            {
                wish.PrasadDeliveredOn = null;
                wish.PrasadSavedBy = CurrentUser;
                await _db.SaveChangesAsync();
                return Ok(new { success = true, message = "Prasad date removed.", wish = WishDto(wish) });
            }

            if (!DateTime.TryParseExact(model.Date.Trim(), "yyyy-MM-dd", Inv, DateTimeStyles.None, out var day))
                return Fail(400, "Please choose the date the prasad was given.");
            if (day.Date > _clock.Today) return Fail(400, "The prasad date cannot be in the future.");

            wish.PrasadDeliveredOn = day.Date;
            wish.PrasadSavedBy = CurrentUser;
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Prasad delivery date saved.", wish = WishDto(wish) });
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
                pending = await _db.CongratulationWishes.CountAsync(w => w.Status == CongratulationWish.Generated),
                prasadPending = await _db.CongratulationWishes.CountAsync(w => w.Status == CongratulationWish.Delivered && w.PrasadDeliveredOn == null)
            });
        }
    }
}
