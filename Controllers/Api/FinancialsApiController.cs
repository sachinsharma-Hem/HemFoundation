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
    [Route("api/financials")]
    [ApiController]
    public class FinancialsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public FinancialsApiController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private async Task<long> GetAffiliateId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var aff = await _context.Affiliates.FirstOrDefaultAsync(a => a.AspNetUserID == userId);
            return aff?.UserID ?? 0;
        }

        // =========================================================================
        // 1. ADMIN: ORGANISATION MASTER APIs
        // =========================================================================

        [HttpGet("admin/orgs")]
        public async Task<IActionResult> GetAllOrganizations()
        {
            var list = await _context.OrganizationMasters
                .Where(o => !o.IsDeleted)
                .OrderByDescending(o => o.Id)
                .ToListAsync();
            return Ok(new { success = true, data = list });
        }

        [HttpPost("admin/save-org")]
        public async Task<IActionResult> SaveOrganization([FromForm] OrganizationDto dto)
        {
            string? logoPath = null;
            if (dto.LogoFile != null && dto.LogoFile.Length > 0)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "logos");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var fileName = Guid.NewGuid().ToString() + "_" + dto.LogoFile.FileName;
                var path = Path.Combine(folder, fileName);
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    await dto.LogoFile.CopyToAsync(fs);
                }
                logoPath = "/uploads/logos/" + fileName;
            }

            if (dto.Id > 0)
            {
                var org = await _context.OrganizationMasters.FindAsync(dto.Id);
                if (org == null) return NotFound(new { success = false, message = "Organization not found" });

                org.OrgName = dto.OrgName;
                org.Address = dto.Address;
                org.IsActive = dto.IsActive;
                if (!string.IsNullOrEmpty(logoPath)) org.LogoUrl = logoPath;
                org.ModifiedDate = DateTime.UtcNow;
            }
            else
            {
                var org = new OrganizationMaster
                {
                    OrgName = dto.OrgName,
                    Address = dto.Address,
                    LogoUrl = logoPath,
                    IsActive = dto.IsActive,
                    CreatedDate = DateTime.UtcNow
                };
                _context.OrganizationMasters.Add(org);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Organization saved successfully." });
        }

        [HttpDelete("admin/delete-org/{id}")]
        public async Task<IActionResult> DeleteOrganization(int id)
        {
            var org = await _context.OrganizationMasters.FindAsync(id);
            if (org == null) return NotFound(new { success = false, message = "Organization not found" });

            org.IsDeleted = true;
            org.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Organization deleted successfully." });
        }

        // =========================================================================
        // 2. ADMIN: BILL PROCESSING & PAYMENTS
        // =========================================================================

        // =========================================================================
        // YEH METHODS APNE FINANCIALS API CONTROLLER MEIN ADD/REPLACE KAREIN
        // =========================================================================

        // 1. UPDATE: Admin GetAllBills (Exclude 'Generated' status)
        [HttpGet("admin/all-bills")]
        public async Task<IActionResult> GetAllBills([FromQuery] string? status, [FromQuery] long? affiliateId)
        {
            // Sirf wahi bill layein jo Draft ("Generated") nahi hain!
            var query = _context.MonthlyBills.Where(b => !b.IsDeleted && b.Status != "Generated");

            if (!string.IsNullOrEmpty(status)) query = query.Where(b => b.Status == status);
            if (affiliateId.HasValue && affiliateId > 0) query = query.Where(b => b.AffiliateId == affiliateId);

            var list = await (from b in query
                              join a in _context.Affiliates on b.AffiliateId equals a.UserID
                              join o in _context.OrganizationMasters on b.OrganizationId equals o.Id
                              join p in _context.BillPaymentDetails on b.Id equals p.MonthlyBillId into payGroup
                              from pay in payGroup.DefaultIfEmpty()
                              orderby b.CreatedDate descending
                              select new
                              {
                                  b.Id,
                                  b.BillNumber,
                                  b.BillDate,
                                  AffiliateName = a.FirstName + " " + a.LastName,
                                  OrgName = o.OrgName,
                                  b.Particulars,
                                  b.TotalAmount,
                                  b.Status,
                                  b.SignedPdfUrl, // Sirf signed URL bhej rahe hain
                                  b.AdminRemarks,
                                  b.CreatedDate,
                                  Payment = pay != null ? new { pay.TransactionNumber, pay.AmountPaid } : null
                              }).ToListAsync();

            return Ok(new { success = true, data = list });
        }

        // 2. NEW: Dropdown ke liye Affiliates List (Admin ke liye)
        [HttpGet("admin/affiliates-list")]
        public async Task<IActionResult> GetAffiliatesList()
        {
            var affiliates = await _context.Affiliates.Select(a => new { a.UserID, Name = a.FirstName + " " + a.LastName }).ToListAsync();
            return Ok(new { success = true, data = affiliates });
        }

        // 3. NEW: Admin ke liye Affiliate Statistics
        [HttpGet("admin/affiliate-stats/{affiliateId?}")]
        public async Task<IActionResult> GetAdminAffiliateStats(long? affiliateId)
        {
            var query = _context.MonthlyBills.Where(b => !b.IsDeleted && b.Status != "Generated");
            if (affiliateId.HasValue && affiliateId > 0) query = query.Where(b => b.AffiliateId == affiliateId);

            var stats = new
            {
                TotalBills = await query.CountAsync(),
                Approved = await query.CountAsync(b => b.Status == "Approved" || b.Status == "Paid"),
                Rejected = await query.CountAsync(b => b.Status == "Rejected"),
                PendingAmount = await query.Where(b => b.Status == "Uploaded" || b.Status == "Approved" || b.Status == "Correction Requested").SumAsync(b => (decimal?)b.TotalAmount) ?? 0,
                PaidAmount = await _context.BillPaymentDetails.Where(p => query.Select(b => b.Id).Contains(p.MonthlyBillId)).SumAsync(p => (decimal?)p.AmountPaid) ?? 0
            };
            return Ok(new { success = true, data = stats });
        }

        // 4. NEW: Affiliate ke khud ke Statistics (Tab 2 ke liye)
        [HttpGet("my-stats")]
        public async Task<IActionResult> GetMyStats()
        {
            long affId = await GetAffiliateId();
            var query = _context.MonthlyBills.Where(b => b.AffiliateId == affId && !b.IsDeleted && b.Status != "Generated");

            var stats = new
            {
                TotalBills = await query.CountAsync(),
                Approved = await query.CountAsync(b => b.Status == "Approved" || b.Status == "Paid"),
                Rejected = await query.CountAsync(b => b.Status == "Rejected"),
                PendingAmount = await query.Where(b => b.Status == "Uploaded" || b.Status == "Approved" || b.Status == "Correction Requested").SumAsync(b => (decimal?)b.TotalAmount) ?? 0,
                PaidAmount = await _context.BillPaymentDetails.Where(p => query.Select(b => b.Id).Contains(p.MonthlyBillId)).SumAsync(p => (decimal?)p.AmountPaid) ?? 0
            };
            return Ok(new { success = true, data = stats });
        }

        [HttpPost("admin/process-bill")]
        public async Task<IActionResult> ProcessBillPayment([FromBody] AdminBillProcessDto dto)
        {
            var bill = await _context.MonthlyBills.FindAsync(dto.BillId);
            if (bill == null) return NotFound(new { success = false, message = "Bill not found" });

            bill.Status = dto.Status; // Approved, Paid, Rejected, Correction Requested
            bill.AdminRemarks = dto.AdminRemarks;
            bill.ModifiedDate = DateTime.UtcNow;

            if (dto.Status == "Paid")
            {
                var pay = await _context.BillPaymentDetails.FirstOrDefaultAsync(p => p.MonthlyBillId == bill.Id);
                if (pay == null)
                {
                    pay = new BillPaymentDetail { MonthlyBillId = bill.Id };
                    _context.BillPaymentDetails.Add(pay);
                }
                pay.ApprovedBy = dto.ApprovedBy ?? "Admin";
                pay.PaymentDate = dto.PaymentDate ?? DateTime.UtcNow;
                pay.AmountPaid = dto.AmountPaid > 0 ? dto.AmountPaid : bill.TotalAmount;
                pay.TransactionNumber = dto.TransactionNumber ?? "";
                pay.PaymentBy = dto.PaymentBy ?? "Finance Team";
                pay.EntryBy = dto.EntryBy ?? User.Identity?.Name ?? "System";
                pay.CreatedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Bill status and payment details updated successfully." });
        }

        // =========================================================================
        // 3. AFFILIATE: BILL GENERATION & SIGNED UPLOAD
        // =========================================================================

        [HttpGet("init-data")]
        public async Task<IActionResult> GetInitData()
        {
            long affId = await GetAffiliateId();
            var orgs = await _context.OrganizationMasters.Where(o => o.IsActive && !o.IsDeleted).ToListAsync();
            var bankDetail = await _context.AffiliateBankDetails.FirstOrDefaultAsync(b => b.AffiliateId == affId);

            return Ok(new { success = true, organizations = orgs, bankDetail });
        }

        // 1. DRAFT BILLS LAANE KE LIYE (Jisme Signed PDF Upload nahi hui - Status "Generated")
        [HttpGet("draft-bills")]
        public async Task<IActionResult> GetDraftBills()
        {
            long affId = await GetAffiliateId();
            var draftBills = await _context.MonthlyBills
                .Where(b => b.AffiliateId == affId && b.Status == "Generated" && !b.IsDeleted)
                .OrderByDescending(b => b.CreatedDate)
                .Select(b => new
                {
                    b.Id,
                    b.BillNumber,
                    b.OrganizationId,
                    b.BillDate,
                    b.Particulars,
                    b.TotalAmount,
                    b.AmountInWords,
                    b.GeneratedPdfUrl,
                    Items = _context.MonthlyBillItems
                                .Where(i => i.MonthlyBillId == b.Id)
                                .Select(i => new { i.Description, i.Amount })
                                .ToList()
                })
                .ToListAsync();

            return Ok(new { success = true, data = draftBills });
        }

        // 2. HISTORY BILLS (Jisme Signed PDF Upload ho chuki hai)
        [HttpGet("my-bills")]
        public async Task<IActionResult> GetMyBills()
        {
            long affId = await GetAffiliateId();
            // YAHAN CONDITION CHANGE KI HAI: Sirf wahi bill aayenge jo "Generated" nahi hain (Yani Uploaded, Approved ya Paid hain)
            var bills = await (from b in _context.MonthlyBills.Where(x => x.AffiliateId == affId && x.Status != "Generated" && !x.IsDeleted)
                               join o in _context.OrganizationMasters on b.OrganizationId equals o.Id
                               orderby b.CreatedDate descending
                               select new
                               {
                                   b.Id,
                                   b.BillNumber,
                                   b.BillDate,
                                   OrgName = o.OrgName,
                                   b.Particulars,
                                   b.TotalAmount,
                                   b.Status,
                                   b.GeneratedPdfUrl,
                                   b.SignedPdfUrl,
                                   b.AdminRemarks
                               }).ToListAsync();

            return Ok(new { success = true, data = bills });
        }

        [HttpPost("generate-monthly-bill")]
        public async Task<IActionResult> GenerateMonthlyBill([FromBody] BillPayloadDto model)
        {
            long affId = await GetAffiliateId();
            var affiliate = await _context.Affiliates.FirstOrDefaultAsync(a => a.UserID == affId);
            var org = await _context.OrganizationMasters.FindAsync(model.OrganizationId);

            if (affiliate == null || org == null) return BadRequest(new { success = false, message = "Invalid Affiliate or Organization" });

            // Bank Details Update / Save
            var bankDetail = await _context.AffiliateBankDetails.FirstOrDefaultAsync(b => b.AffiliateId == affId);
            if (bankDetail == null)
            {
                bankDetail = new AffiliateBankDetail { AffiliateId = affId };
                _context.AffiliateBankDetails.Add(bankDetail);
            }
            bankDetail.AccountName = model.AccountName;
            bankDetail.AccountNumber = model.AccountNumber;
            bankDetail.BankName = model.BankName;
            bankDetail.IFSC = model.IFSC;
            bankDetail.PAN = model.PAN;

            MonthlyBill bill;

            // AGAR BILL PEHLE SE HAI AUR EDIT HO RAHA HAI
            if (model.BillId.HasValue && model.BillId > 0)
            {
                bill = await _context.MonthlyBills.FindAsync(model.BillId.Value);
                if (bill == null || bill.Status != "Generated") return BadRequest(new { success = false, message = "Cannot edit this bill." });

                bill.OrganizationId = model.OrganizationId;
                bill.BillDate = model.BillDate;
                bill.Particulars = model.Particulars;
                bill.TotalAmount = model.TotalAmount;
                bill.AmountInWords = model.AmountInWords;
                bill.ModifiedDate = DateTime.UtcNow;

                // Puraane items delete karo
                var oldItems = _context.MonthlyBillItems.Where(i => i.MonthlyBillId == bill.Id);
                _context.MonthlyBillItems.RemoveRange(oldItems);
            }
            else
            {
                // NAYA BILL BAN RAHA HAI
                var lastBill = await _context.MonthlyBills.OrderByDescending(b => b.Id).FirstOrDefaultAsync();
                int nextNumber = lastBill != null ? int.Parse(lastBill.BillNumber) + 1 : 1;

                bill = new MonthlyBill
                {
                    AffiliateId = affId,
                    BillNumber = nextNumber.ToString("D4"),
                    OrganizationId = model.OrganizationId,
                    BillDate = model.BillDate,
                    Particulars = model.Particulars,
                    TotalAmount = model.TotalAmount,
                    AmountInWords = model.AmountInWords,
                    Status = "Generated",
                    CreatedDate = DateTime.UtcNow
                };
                _context.MonthlyBills.Add(bill);
            }

            await _context.SaveChangesAsync(); // Save to get ID

            // Naye items add karo
            foreach (var item in model.Items)
            {
                _context.MonthlyBillItems.Add(new MonthlyBillItem
                {
                    MonthlyBillId = bill.Id,
                    Description = item.Description,
                    Amount = item.Amount
                });
            }
            await _context.SaveChangesAsync();

            // PDF Generation
            string pdfUrl = await GeneratePdfAsync(bill, affiliate, org, bankDetail, model.Items);
            bill.GeneratedPdfUrl = pdfUrl;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, pdfUrl = pdfUrl, billId = bill.Id });
        }

        [HttpPost("upload-signed-bill")]
        public async Task<IActionResult> UploadSignedBill([FromForm] int billId, [FromForm] IFormFile signedFile)
        {
            var bill = await _context.MonthlyBills.FindAsync(billId);
            if (bill == null) return NotFound(new { success = false, message = "Bill not found" });

            if (signedFile != null && signedFile.Length > 0)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "signed_bills");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var fileName = $"Signed_{bill.BillNumber}_{Guid.NewGuid()}{Path.GetExtension(signedFile.FileName)}";
                var filePath = Path.Combine(folder, fileName);

                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await signedFile.CopyToAsync(fs);
                }

                bill.SignedPdfUrl = "/uploads/signed_bills/" + fileName;
                bill.Status = "Uploaded"; // Waiting for Admin Approval
                bill.ModifiedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Signed bill uploaded successfully!" });
            }

            return BadRequest(new { success = false, message = "Please select a valid PDF file." });
        }

       

        // =========================================================================
        // HELPER: PDF GENERATION METHOD
        // =========================================================================
        // =========================================================================
        // HELPER: PROFESSIONAL PDF GENERATION METHOD
        // =========================================================================
        private async Task<string> GeneratePdfAsync(MonthlyBill bill, Affiliate aff, OrganizationMaster org, AffiliateBankDetail bank, List<BillItemDto> items)
        {
            string itemRows = "";
            for (int i = 0; i < items.Count; i++)
            {
                itemRows += $@"
            <tr style='border-bottom: 1px solid #e0e0e0;'>
                <td style='padding: 12px 15px; text-align: center; color: #555;'>{i + 1}</td>
                <td style='padding: 12px 15px; color: #333;'>{items[i].Description}</td>
                <td style='padding: 12px 15px; text-align: right; color: #333; font-weight: 500;'>₹{items[i].Amount:0.00}</td>
            </tr>";
            }

            // Convert Local Image to Base64 so Puppeteer can render it perfectly!
            string base64Logo = "";
            if (!string.IsNullOrEmpty(org.LogoUrl))
            {
                try
                {
                    var physicalPath = Path.Combine(_env.WebRootPath, org.LogoUrl.TrimStart('/'));
                    if (System.IO.File.Exists(physicalPath))
                    {
                        byte[] imageArray = System.IO.File.ReadAllBytes(physicalPath);
                        string ext = Path.GetExtension(physicalPath).Replace(".", "").ToLower();
                        base64Logo = $"data:image/{ext};base64,{Convert.ToBase64String(imageArray)}";
                    }
                }
                catch { /* Fallback to empty if file not found */ }
            }

            string logoHtml = !string.IsNullOrEmpty(base64Logo) ? $"<img src='{base64Logo}' style='max-height: 80px; max-width: 250px; object-fit: contain;' />" : $"<h2 style='color:#0b5ed7; margin:0;'>{org.OrgName}</h2>";

            string htmlContent = $@"
    <html>
    <body style='font-family: ""Helvetica Neue"", Helvetica, Arial, sans-serif; padding: 50px; background-color: #fff; color: #333;'>
        
        <table width='100%' style='border-bottom: 3px solid #0b5ed7; padding-bottom: 20px; margin-bottom: 30px;'>
            <tr>
                <td width='60%' valign='top'>
                    {logoHtml}
                </td>
                <td width='40%' valign='top' style='text-align: right;'>
                    <h1 style='color: #0b5ed7; margin: 0 0 5px 0; font-size: 32px; letter-spacing: 2px;'>INVOICE</h1>
                    <p style='margin: 0; font-size: 14px; color: #777;'>
                        <b>Bill No:</b> {bill.BillNumber}<br>
                        <b>Date:</b> {bill.BillDate:dd-MMM-yyyy}
                    </p>
                </td>
            </tr>
        </table>

        <table width='100%' style='margin-bottom: 40px;'>
            <tr>
                <td width='50%' valign='top' style='padding-right: 20px;'>
                    <h4 style='margin: 0 0 10px 0; color: #fff; background-color: #6c757d; padding: 6px 12px; display: inline-block; border-radius: 4px; font-size: 12px; text-transform: uppercase;'>Billed To</h4>
                    <p style='margin: 0; font-size: 15px; line-height: 1.6;'>
                        <strong style='font-size: 18px; color: #212529;'>{org.OrgName}</strong><br>
                        {org.Address.Replace("\n", "<br>")}
                    </p>
                </td>
                
                <td width='50%' valign='top' style='text-align: right; padding-left: 20px;'>
                    <h4 style='margin: 0 0 10px 0; color: #fff; background-color: #198754; padding: 6px 12px; display: inline-block; border-radius: 4px; font-size: 12px; text-transform: uppercase;'>From</h4>
                    <p style='margin: 0; font-size: 15px; line-height: 1.6;'>
                        <strong style='font-size: 18px; color: #212529;'>{aff.FirstName} {aff.LastName}</strong><br>
                        {(string.IsNullOrEmpty(aff.AddressLine1) ? "Address not available" : aff.AddressLine1)}<br>
                        <b>Phone:</b> {aff.MobileNo}<br>
                        <b>Email:</b> {(string.IsNullOrEmpty(aff.EmailID) ? "N/A" : aff.EmailID)}
                    </p>
                </td>
            </tr>
        </table>

        <div style='background-color: #f8f9fa; padding: 10px 15px; border-left: 4px solid #0b5ed7; margin-bottom: 25px;'>
            <h5 style='margin: 0; font-size: 14px; color: #555;'>PARTICULARS</h5>
            <p style='margin: 5px 0 0 0; font-size: 16px; font-weight: bold; color: #000;'>{bill.Particulars}</p>
        </div>
        
        <table width='100%' style='border-collapse: collapse; margin-bottom: 30px; font-size: 15px;'>
            <thead>
                <tr style='background-color: #0b5ed7; color: #fff;'>
                    <th style='padding: 12px 15px; text-align: center; width: 10%; border-radius: 6px 0 0 0;'>S.No</th>
                    <th style='padding: 12px 15px; text-align: left; width: 65%;'>Description (Annexure)</th>
                    <th style='padding: 12px 15px; text-align: right; width: 25%; border-radius: 0 6px 0 0;'>Amount (Rs)</th>
                </tr>
            </thead>
            <tbody>
                {itemRows}
            </tbody>
            <tfoot>
                <tr>
                    <td colspan='2' style='padding: 15px; text-align: right; font-size: 16px; border-top: 2px solid #0b5ed7;'><b>Total Amount:</b></td>
                    <td style='padding: 15px; text-align: right; font-size: 18px; color: #198754; border-top: 2px solid #0b5ed7;'><b>₹{bill.TotalAmount:0.00}</b></td>
                </tr>
            </tfoot>
        </table>

        <p style='font-size: 14px; background-color: #e9ecef; padding: 10px; border-radius: 4px;'>
            <b>Amount in Words:</b> Rupees {bill.AmountInWords} Only.
        </p>
        
        <table width='100%' style='margin-top: 50px;'>
            <tr>
                <td width='60%' valign='bottom'>
                    <div style='border: 1px solid #dee2e6; border-radius: 6px; padding: 15px; background-color: #fcfcfc;'>
                        <h4 style='margin: 0 0 10px 0; color: #495057; font-size: 14px; text-transform: uppercase;'>Bank Details for Payment</h4>
                        <table style='font-size: 13px; color: #555;' cellpadding='3'>
                            <tr><td><b>Account Name:</b></td><td>{bank.AccountName}</td></tr>
                            <tr><td><b>Account No:</b></td><td>{bank.AccountNumber}</td></tr>
                            <tr><td><b>Bank Name:</b></td><td>{bank.BankName}</td></tr>
                            <tr><td><b>IFSC Code:</b></td><td>{bank.IFSC}</td></tr>
                            <tr><td><b>PAN No:</b></td><td>{bank.PAN}</td></tr>
                        </table>
                    </div>
                </td>
                <td width='40%' valign='bottom' style='text-align: center; padding-left: 30px;'>
                    <div style='border-bottom: 1px solid #000; width: 100%; height: 40px; margin-bottom: 5px;'></div>
                    <b style='font-size: 14px; color: #333;'>Signature of Affiliate</b>
                </td>
            </tr>
        </table>

    </body>
    </html>";

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
            using var page = await browser.NewPageAsync();
            await page.SetContentAsync(htmlContent);

            var fileName = $"Bill_{bill.BillNumber}_{DateTime.Now.Ticks}.pdf";
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "bills");
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

            var filePath = Path.Combine(uploadDir, fileName);

            // Set Print Background to true so colors are rendered properly!
            await page.PdfAsync(filePath, new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true, // REQUIRED FOR COLORS TO SHOW IN PDF
                MarginOptions = new MarginOptions { Top = "10px", Bottom = "10px", Left = "10px", Right = "10px" }
            });

            return $"/uploads/bills/{fileName}";
        }
    }

    public class OrganizationDto
    {
        public int Id { get; set; }
        public string OrgName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public IFormFile? LogoFile { get; set; }
    }

    public class AdminBillProcessDto
    {
        public int BillId { get; set; }
        public string Status { get; set; } = string.Empty; // Approved, Paid, Rejected, Correction Requested
        public string? AdminRemarks { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? PaymentDate { get; set; }
        public decimal AmountPaid { get; set; }
        public string? TransactionNumber { get; set; }
        public string? PaymentBy { get; set; }
        public string? EntryBy { get; set; }
    }

    public class BillPayloadDto
    {
        public int? BillId { get; set; } // <--- YEH NAYI LINE ADD KAREIN
        public int OrganizationId { get; set; }
        public DateTime BillDate { get; set; }
        public string Particulars { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string AmountInWords { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string IFSC { get; set; } = string.Empty;
        public string PAN { get; set; } = string.Empty;
        public List<BillItemDto> Items { get; set; } = new List<BillItemDto>();
    }

    public class BillItemDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}