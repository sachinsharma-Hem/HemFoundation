using HemFoundation.Data;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Security.Claims;

namespace HemFoundation.Controllers.Api
{
    [Authorize]
    [Route("api/other-activity")]
    [ApiController]
    public class OtherActivityApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public OtherActivityApiController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private async Task<long> GetCurrentAffiliateIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return 0;

            return await _context.Users
                .Where(x => x.AspNetUserID == userId)
                .Select(x => x.UserID)
                .FirstOrDefaultAsync();
        }

        // ================= ACTIVITY ENDPOINTS =================

        [HttpGet("list")]
        public async Task<IActionResult> GetActivities()
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var list = await _context.AffiliateOtherActivities
                .Where(x => x.AffiliateId == affId && !x.IsDeleted)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return Ok(new { success = true, data = list });
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateActivity([FromBody] ActivityCreateModel model)
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var activity = new AffiliateOtherActivity
            {
                AffiliateId = affId,
                ActivityName = model.ActivityName,
                SchoolName = model.SchoolName,
                Address = model.Address,
                Pincode = model.Pincode,
                City = model.City,
                State = model.State,
                VisitReason = model.VisitReason,
                InitialRemark = model.InitialRemark,
                Status = "Pending",
                CreatedDate = DateTime.UtcNow
            };

            _context.AffiliateOtherActivities.Add(activity);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Activity created successfully." });
        }

        [HttpPost("update-progress")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateProgress(
      [FromForm] long id,
      [FromForm] string? updateRemark,
      [FromForm] string? status,
      [FromForm] IFormFile? activityImage)
        {
            try
            {
                long affId = await GetCurrentAffiliateIdAsync();

                if (affId == 0)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Unauthorized. Affiliate not found."
                    });
                }

                // Validate ID
                if (id <= 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid activity ID."
                    });
                }

                // Find activity only for logged-in affiliate
                var item = await _context.AffiliateOtherActivities
                    .FirstOrDefaultAsync(x =>
                        x.Id == id &&
                        x.AffiliateId == affId &&
                        !x.IsDeleted);

                if (item == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Activity not found or you do not have permission to update it."
                    });
                }

                // Clean values
                updateRemark = updateRemark?.Trim() ?? string.Empty;
                status = status?.Trim() ?? "Pending";

                // Allowed statuses
                var allowedStatuses = new[]
                {
            "Pending",
            "InProgress",
            "Completed",
            "Cancelled"
        };

                if (!allowedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Invalid status '{status}'. Allowed values: Pending, InProgress, Completed, Cancelled."
                    });
                }

                // Update basic information
                item.UpdateRemark = updateRemark;
                item.Status = status;
                item.UpdateDate = DateTime.UtcNow;
                item.ModifiedDate = DateTime.UtcNow;

                // ================= IMAGE UPLOAD =================

                if (activityImage != null && activityImage.Length > 0)
                {
                    // Optional file size limit: 10 MB
                    if (activityImage.Length > 10 * 1024 * 1024)
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Image size cannot exceed 10 MB."
                        });
                    }

                    var uploadsFolder = Path.Combine(
                        _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
                        "uploads",
                        "activities"
                    );

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Safe extension
                    var extension = Path.GetExtension(activityImage.FileName);

                    var allowedExtensions = new[]
                    {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

                    if (!allowedExtensions.Contains(
                            extension,
                            StringComparer.OrdinalIgnoreCase))
                    {
                        return BadRequest(new
                        {
                            success = false,
                            message = "Only JPG, JPEG, PNG and WEBP images are allowed."
                        });
                    }

                    var uniqueFileName =
                        $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

                    var filePath = Path.Combine(
                        uploadsFolder,
                        uniqueFileName
                    );

                    await using (var stream = new FileStream(
                        filePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None))
                    {
                        await activityImage.CopyToAsync(stream);
                    }

                    item.ImagePath =
                        $"/uploads/activities/{uniqueFileName}";
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Activity updated successfully.",
                    data = new
                    {
                        item.Id,
                        item.Status,
                        item.UpdateRemark,
                        item.ImagePath,
                        item.UpdateDate
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while updating the activity.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("expense/list")]
        public async Task<IActionResult> GetExpenses()
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var list = await _context.AffiliateExpenses
                .Where(x => x.AffiliateId == affId && !x.IsDeleted)
                .OrderByDescending(x => x.ExpenseDate)
                .Select(x => new
                {
                    x.Id,
                    x.ExpenseDate,
                    x.ExpenseDetail,
                    x.Amount,
                    x.Status,
                    x.ReceiptPath
                })
                .ToListAsync();

            return Ok(new { success = true, data = list });
        }

        [HttpPost("expense/create")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateExpense(
      [FromForm] DateTime expenseDate,
      [FromForm] string expenseDetail,
      [FromForm] decimal amount,
      [FromForm] string? relatedType,
      [FromForm] long? relatedId,
      IFormFile? expenseImage)
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            string prefix = relatedType == "SchoolVisit" ? "[School Visit] " : (relatedType == "OtherActivity" ? "[Other Activity] " : "");
            string finalDetail = prefix + expenseDetail;

            string? savedReceiptPath = null;

            if (expenseImage != null && expenseImage.Length > 0)
            {
                if (expenseImage.Length > 10 * 1024 * 1024)
                {
                    return BadRequest(new { success = false, message = "Image size cannot exceed 10 MB." });
                }

                var uploadsFolder = Path.Combine(
                    _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
                    "uploads",
                    "expenses"
                );

                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var extension = Path.GetExtension(expenseImage.FileName);
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

                if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = "Only JPG, JPEG, PNG, WEBP and PDF files are allowed." });
                }

                var uniqueFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await expenseImage.CopyToAsync(stream);
                }

                savedReceiptPath = $"/uploads/expenses/{uniqueFileName}";
            }

            var expense = new AffiliateExpense
            {
                AffiliateId = affId,
                ExpenseDate = expenseDate.Date,
                ExpenseDetail = finalDetail,
                Amount = amount,
                RelatedType = relatedType,
                RelatedId = relatedId,
                Status = "Pending",
                ReceiptPath = savedReceiptPath,
                CreatedDate = DateTime.UtcNow
            };

            _context.AffiliateExpenses.Add(expense);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Expense submitted successfully." });
        }

        // ================= EXPENSE ENDPOINTS =================

        //[HttpGet("expense/list")]
        //public async Task<IActionResult> GetExpenses()
        //{
        //    long affId = await GetCurrentAffiliateIdAsync();
        //    if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

        //    var list = await _context.AffiliateExpenses
        //        .Where(x => x.AffiliateId == affId && !x.IsDeleted)
        //        .OrderByDescending(x => x.ExpenseDate)
        //        .ToListAsync();

        //    return Ok(new { success = true, data = list });
        //}

        //[HttpPost("expense/create")]
        //public async Task<IActionResult> CreateExpense([FromBody] ExpenseCreateModel model)
        //{
        //    long affId = await GetCurrentAffiliateIdAsync();
        //    if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

        //    // Auto-prepend related information to ExpenseDetail for clarity
        //    string prefix = model.RelatedType == "SchoolVisit" ? "[School Visit] " : (model.RelatedType == "OtherActivity" ? "[Other Activity] " : "");
        //    string finalDetail = prefix + model.ExpenseDetail;

        //    var expense = new AffiliateExpense
        //    {
        //        AffiliateId = affId,
        //        ExpenseDate = model.ExpenseDate.Date,
        //        ExpenseDetail = finalDetail,
        //        Amount = model.Amount,
        //        RelatedType = model.RelatedType,
        //        RelatedId = model.RelatedId,
        //        Status = "Pending",
        //        CreatedDate = DateTime.UtcNow
        //    };

        //    _context.AffiliateExpenses.Add(expense);
        //    await _context.SaveChangesAsync();

        //    return Ok(new { success = true, message = "Expense submitted successfully." });
        //}

        // Get list of completed/active school visits for dropdown
        [HttpGet("expense/school-visits")]
        public async Task<IActionResult> GetSchoolVisitsForExpense()
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var visits = await _context.AffiliateVisitPlans
                .Where(v => v.AffiliateId == affId && !v.IsDeleted)
                .Join(_context.SchoolProfileDetails, v => v.InstituteID, s => s.instituteid, (v, s) => new
                {
                    id = v.Id,
                    name = s.institute_name + (!string.IsNullOrEmpty(s.cityname) ? $" ({s.cityname})" : "")
                })
                .ToListAsync();

            return Ok(new { success = true, data = visits });
        }

        [HttpGet("expense/activities-list")]
        public async Task<IActionResult> GetActivitiesForExpense()
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var activities = await _context.AffiliateOtherActivities
                .Where(x => x.AffiliateId == affId && !x.IsDeleted)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.ActivityName + " - " + x.SchoolName
                })
                .ToListAsync();

            return Ok(new { success = true, data = activities });
        }
        [HttpGet("expense/monthly-bills")]
        public async Task<IActionResult> GetMonthlyExpenseBills()
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var bills = await _context.MonthlyExpenseBills
                .Where(x => x.AffiliateId == affId && !x.IsDeleted)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            return Ok(new { success = true, data = bills });
        }

        [HttpPost("expense/generate-monthly-bill")]
        public async Task<IActionResult> GenerateMonthlyExpenseBill([FromBody] ExpenseBillGenModel model)
        {
            try
            {
                long affId = await GetCurrentAffiliateIdAsync();
                if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

                if (!DateTime.TryParse(model.MonthYear + "-01", out DateTime targetMonth))
                {
                    return BadRequest(new { success = false, message = "Invalid month format." });
                }

                int year = targetMonth.Year;
                int month = targetMonth.Month;

                var expenses = await _context.AffiliateExpenses
                    .Where(x => x.AffiliateId == affId && !x.IsDeleted &&
                                x.ExpenseDate.Year == year && x.ExpenseDate.Month == month)
                    .ToListAsync();

                if (expenses == null || expenses.Count == 0)
                {
                    return BadRequest(new { success = false, message = "No expenses found for this month." });
                }

                decimal totalAmount = expenses.Sum(x => x.Amount);
                string monthNameStr = targetMonth.ToString("MMMM-yyyy");

                var existingBill = await _context.MonthlyExpenseBills
                    .FirstOrDefaultAsync(x => x.AffiliateId == affId && x.MonthYear == monthNameStr && !x.IsDeleted);

                MonthlyExpenseBill expenseBill;
                if (existingBill != null)
                {
                    if (existingBill.Status != "Generated")
                        return BadRequest(new { success = false, message = "Cannot modify this bill as it is already submitted or processed." });

                    expenseBill = existingBill;
                    expenseBill.TotalAmount = totalAmount;
                    expenseBill.ModifiedDate = DateTime.UtcNow;
                }
                else
                {
                    expenseBill = new MonthlyExpenseBill
                    {
                        AffiliateId = affId,
                        MonthYear = monthNameStr,
                        TotalAmount = totalAmount,
                        Status = "Generated",
                        CreatedDate = DateTime.UtcNow
                    };
                    _context.MonthlyExpenseBills.Add(expenseBill);
                }

                await _context.SaveChangesAsync();

                var affiliate = await _context.Users.FirstOrDefaultAsync(x => x.UserID == affId);
                var bankDetail = await _context.AffiliateBankDetails.FirstOrDefaultAsync(x => x.AffiliateId == affId);

                string pdfUrl = await GenerateExpensePdfAsync(expenseBill, affiliate, bankDetail, expenses);

                expenseBill.GeneratedPdfUrl = pdfUrl;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Expense bill generated successfully.", pdfUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("expense/upload-signed-bill")]
        public async Task<IActionResult> UploadSignedExpenseBill([FromForm] long billId, [FromForm] IFormFile signedFile)
        {
            long affId = await GetCurrentAffiliateIdAsync();
            if (affId == 0) return Unauthorized(new { success = false, message = "Unauthorized" });

            var bill = await _context.MonthlyExpenseBills.FirstOrDefaultAsync(x => x.Id == billId && x.AffiliateId == affId);
            if (bill == null) return NotFound(new { success = false, message = "Bill not found." });

            if (signedFile != null && signedFile.Length > 0)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "signed_expense_bills");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string fileName = $"Signed_Expense_{bill.Id}_{Guid.NewGuid()}{Path.GetExtension(signedFile.FileName)}";
                string filePath = Path.Combine(folder, fileName);

                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await signedFile.CopyToAsync(fs);
                }

                bill.SignedPdfUrl = "/uploads/signed_expense_bills/" + fileName;
                bill.Status = "Uploaded"; // Sent to Admin
                bill.ModifiedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Signed expense bill uploaded successfully!" });
            }

            return BadRequest(new { success = false, message = "Please select a valid PDF file." });
        }

        private async Task<string> GenerateExpensePdfAsync(MonthlyExpenseBill bill, Users aff, AffiliateBankDetail bank, List<AffiliateExpense> expenses)
        {
            string itemRows = "";
            for (int i = 0; i < expenses.Count; i++)
            {
                itemRows += $@"
                <tr style='border-bottom: 1px solid #e0e0e0;'>
                    <td style='padding: 10px; text-align: center;'>{i + 1}</td>
                    <td style='padding: 10px;'>{expenses[i].ExpenseDate:dd-MMM-yyyy}</td>
                    <td style='padding: 10px;'>{System.Net.WebUtility.HtmlEncode(expenses[i].ExpenseDetail)}</td>
                    <td style='padding: 10px; text-align: right; font-weight: 500;'>₹{expenses[i].Amount:0.00}</td>
                </tr>";
            }

            string htmlContent = $@"
            <html>
            <body style='font-family: Arial, sans-serif; padding: 40px; color: #333;'>
                <h2 style='color: #0b5ed7; text-align: center; margin-bottom: 5px;'>EXPENSE REIMBURSEMENT INVOICE</h2>
                <p style='text-align: center; color: #777; margin-top: 0;'><b>Month:</b> {bill.MonthYear}</p>
                <hr style='border: 1px solid #0b5ed7; margin-bottom: 30px;'/>
                
                <p><b>Affiliate Name:</b> {aff?.FirstName} {aff?.LastName}</p>
                <p><b>Mobile:</b> {aff?.MobileNo} | <b>Email:</b> {aff?.EmailID}</p>

                <table width='100%' style='border-collapse: collapse; margin-top: 20px; font-size: 14px;'>
                    <thead>
                        <tr style='background-color: #0b5ed7; color: #fff;'>
                            <th style='padding: 10px; text-align: center;'>S.No</th>
                            <th style='padding: 10px; text-align: left;'>Date</th>
                            <th style='padding: 10px; text-align: left;'>Particulars / Detail</th>
                            <th style='padding: 10px; text-align: right;'>Amount (₹)</th>
                        </tr>
                    </thead>
                    <tbody>
                        {itemRows}
                    </tbody>
                    <tfoot>
                        <tr>
                            <td colspan='3' style='padding: 12px; text-align: right; border-top: 2px solid #0b5ed7;'><b>Total Reimbursement:</b></td>
                            <td style='padding: 12px; text-align: right; color: #198754; border-top: 2px solid #0b5ed7;'><b>₹{bill.TotalAmount:0.00}</b></td>
                        </tr>
                    </tfoot>
                </table>

                <div style='margin-top: 40px; border: 1px solid #ddd; padding: 15px; border-radius: 6px; background: #f9f9f9;'>
                    <h4 style='margin: 0 0 10px 0; font-size: 13px; text-transform: uppercase; color: #555;'>Bank Details</h4>
                    <p style='margin: 3px 0; font-size: 13px;'><b>Account Name:</b> {bank?.AccountName}</p>
                    <p style='margin: 3px 0; font-size: 13px;'><b>Account No:</b> {bank?.AccountNumber}</p>
                    <p style='margin: 3px 0; font-size: 13px;'><b>Bank Name:</b> {bank?.BankName} | <b>IFSC:</b> {bank?.IFSC}</p>
                </div>
            </body>
            </html>";

            string chromiumPath = Path.Combine(_env.ContentRootPath, "Chromium");
            var browserFetcher = new BrowserFetcher(new BrowserFetcherOptions { Path = chromiumPath });
            var installedBrowser = await browserFetcher.DownloadAsync();

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = installedBrowser.GetExecutablePath(),
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            });

            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);

            string uploadDir = Path.Combine(_env.WebRootPath, "uploads", "expense_bills");
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

            string fileName = $"ExpenseBill_{bill.Id}_{DateTime.Now.Ticks}.pdf";
            string filePath = Path.Combine(uploadDir, fileName);

            await page.PdfAsync(filePath, new PdfOptions { Format = PaperFormat.A4, PrintBackground = true });

            return $"/uploads/expense_bills/{fileName}";
        }


        
    }
    public class ExpenseBillGenModel
    {
        public string MonthYear { get; set; } = string.Empty; 
    }

    public class ActivityCreateModel
    {
        public string ActivityName { get; set; } = string.Empty;
        public string SchoolName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string VisitReason { get; set; } = string.Empty;
        public string? InitialRemark { get; set; }
    }

    public class ActivityUpdateModel
    {
        public long Id { get; set; }
        public string UpdateRemark { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class ExpenseCreateModel
    {
        public DateTime ExpenseDate { get; set; }
        public string ExpenseDetail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? RelatedType { get; set; }
        public long? RelatedId { get; set; }
    }
}