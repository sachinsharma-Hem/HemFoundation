    using HemFoundation.Common;
    using HemFoundation.Data;
    using HemFoundation.DTOs;
    using HemFoundation.Models.Identity;
    using HemFoundation.Services;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.IdentityModel.Tokens;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Text;
    using System.Security.Cryptography;

    namespace HemFoundation.Controllers.Api
    {
        [Route("api/auth")]
        [ApiController]
        public class AuthApiController : ControllerBase
        {
            private readonly SignInManager<ApplicationUser> _signInManager;
            private readonly UserManager<ApplicationUser> _userManager;
            private readonly ApplicationDbContext _context;
            private readonly IConfiguration _configuration;

            public AuthApiController(
                SignInManager<ApplicationUser> signInManager,
                UserManager<ApplicationUser> userManager,
                ApplicationDbContext context,
                IConfiguration configuration)
            {
                _signInManager = signInManager;
                _userManager = userManager;
                _context = context;
                _configuration = configuration;
            }

            [HttpPost("login")]
            public async Task<IActionResult> Login([FromBody] LoginDto model)
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Invalid payload or missing fields" });

                string username = model.Email?.Trim() ?? "";
                if (string.IsNullOrEmpty(username))
                    return BadRequest(new { success = false, message = "Username or Email is required." });

                var adminUser = await _context.Admins
                    .FirstOrDefaultAsync(a => (a.Email == username || a.IdentityUserId == username) && a.IsActive && !a.IsDeleted);

                if (adminUser != null)
                {
                    if (adminUser.PlainPassword != model.Password)
                    {
                        return Unauthorized(new { success = false, message = "Invalid username or password." });
                    }

                    string primaryRole = "Admin";
                    var roles = new List<string> { primaryRole };

                    List<HemFoundation.Models.Entities.RoleMenuRight> menuRights;
                    if (adminUser.IsSuperAdmin)
                    {
                        menuRights = await _context.RoleMenuRights
                            .Where(rm => rm.CanView == true && rm.IsActive == true && rm.IsDeleted == false)
                            .ToListAsync();
                    }
                    else
                    {
                        var roleIds = await _context.Roles
                            .Where(r => r.Name == primaryRole)
                            .Select(r => r.Id)
                            .ToListAsync();

                        menuRights = await _context.RoleMenuRights
                            .Where(rm => roleIds.Contains(rm.RoleId) && rm.CanView == true && rm.IsActive == true && rm.IsDeleted == false)
                            .ToListAsync();
                    }

                    var menuIds = menuRights.Select(rm => rm.MenuId).Distinct().ToList();
                    string adminDynamicPath = Convert.ToHexString(
        RandomNumberGenerator.GetBytes(16)
    );

                    string redirectUrl = "/admin/dashboard";
                    var dbMenus = await _context.Menus
                        .Where(m => menuIds.Contains(m.Id) && m.IsActive == true && m.IsDeleted == false)
                        .OrderBy(m => m.DisplayOrder)
                        .ToListAsync();

                    var menus = dbMenus.Select(m => new {
                        m.Id,
                        m.ParentId,
                        MenuName = m.MenuName,
                        Controller = m.MenuName == "Dashboard" ? "Admin" : m.Controller,
                        Action = m.MenuName == "Dashboard" ? "Dashboard" : m.Action,
                        RouteUrl = m.MenuName == "Dashboard" ? redirectUrl : m.RouteUrl,
                        m.Icon,
                        m.DisplayOrder,
                        m.MenuType
                    }).ToList();

                    string fullName = $"{adminUser.FirstName} {adminUser.LastName}".Trim();

                    var authClaims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
                        new Claim(ClaimTypes.Email, adminUser.Email ?? ""),
                        new Claim(ClaimTypes.Name, fullName),
                        new Claim("UserID", adminUser.Id.ToString()),
                        new Claim(ClaimTypes.Role, primaryRole)
                    };

                    var jwtKey = _configuration["JwtSettings:Secret"] ?? "HemFoundation_Super_Secret_Key_2026_Secure!";
                    var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

                    int expiryHours = model.RememberMe ? (24 * 7) : (int.TryParse(_configuration["JwtSettings:ExpiryHours"], out var hours) ? hours : 8);

                    var token = new JwtSecurityToken(
                        issuer: _configuration["JwtSettings:Issuer"] ?? "HemFoundation",
                        audience: _configuration["JwtSettings:Audience"] ?? "HemFoundationUsers",
                        expires: DateTime.Now.AddHours(expiryHours),
                        claims: authClaims,
                        signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                    );

                    HttpContext.Session.SetString("IsAdminLoggedIn", "true");
                    HttpContext.Session.SetString("UserId", adminUser.Id.ToString());
                    HttpContext.Session.SetString("UserEmail", adminUser.Email ?? "");
                    HttpContext.Session.SetString("UserFullName", fullName);
                    HttpContext.Session.SetString("FirstName", adminUser.FirstName ?? "");
                    HttpContext.Session.SetString("LastName", adminUser.LastName ?? "");
                    HttpContext.Session.SetString("UserMobile", adminUser.MobileNo ?? "");
                    HttpContext.Session.SetString("UserRole", primaryRole);
                    HttpContext.Session.SetString("DefaultURL", redirectUrl);
                    HttpContext.Session.SetString("AdminDynamicPath", adminDynamicPath);

                    return Ok(new
                    {
                        success = true,
                        token = new JwtSecurityTokenHandler().WriteToken(token),
                        expiration = token.ValidTo,
                        redirectUrl = redirectUrl,
                        key = adminDynamicPath,
                        user = new
                        {
                            Id = adminUser.Id.ToString(),
                            FullName = fullName,
                            FirstName = adminUser.FirstName,
                            LastName = adminUser.LastName,
                            Email = adminUser.Email,
                            MobileNo = adminUser.MobileNo,
                            Roles = roles
                        },
                        menus,
                        rights = menuRights
                    });
                }


            var user = await _context.Set<ApplicationUser>()
 .FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);

            if (user == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid credentials or account inactive."
                });
            }

            if (user.LockoutEnabled)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Your account is not enabled for login. Please contact administrator."
                });
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Your account is currently locked. Please contact administrator."
                });
            }

            var businessUser = await _context.Users
                    .FirstOrDefaultAsync(b => b.AspNetUserID == user.Id);

                if (businessUser == null || businessUser.IsActive == false)
                {
                    return Unauthorized(new { success = false, message = "Your account is inactive, unsubscribed, or does not exist." });
                }

                var passwordHasher = new PasswordHasher<ApplicationUser>();
                var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash ?? "", model.Password);

                if (verificationResult == PasswordVerificationResult.Failed)
                    return Unauthorized(new { success = false, message = "Invalid username or password." });

                // Fetch actual database roles assigned to this user
                var rolesList = await (from ur in _context.UserRoles
                                       join r in _context.Roles on ur.RoleId equals r.Id
                                       where ur.UserId == user.Id
                                       select r.Name!).ToListAsync();

                string businessFullName = businessUser != null ? $"{businessUser.FirstName}".Trim() : (user.UserName ?? "");
                var businessAuthClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Email, user.Email ?? ""),
                    new Claim(ClaimTypes.Name, businessFullName),
                    new Claim("UserID", businessUser?.UserID.ToString() ?? "")
                };

                foreach (var role in rolesList)
                {
                    businessAuthClaims.Add(new Claim(ClaimTypes.Role, role));
                }

                var bJwtKey = _configuration["JwtSettings:Secret"] ?? "HemFoundation_Super_Secret_Key_2026_Secure!";
                var bAuthSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(bJwtKey));

                int bExpiryHours = model.RememberMe ? (24 * 7) : (int.TryParse(_configuration["JwtSettings:ExpiryHours"], out var bHours) ? bHours : 8);

                var bToken = new JwtSecurityToken(
                    issuer: _configuration["JwtSettings:Issuer"] ?? "HemFoundation",
                    audience: _configuration["JwtSettings:Audience"] ?? "HemFoundationUsers",
                    expires: DateTime.Now.AddHours(bExpiryHours),
                    claims: businessAuthClaims,
                    signingCredentials: new SigningCredentials(bAuthSigningKey, SecurityAlgorithms.HmacSha256)
                );

                // Fetch exact Role IDs for the user's roles
                var bRoleIds = await _context.Roles
                    .Where(r => rolesList.Contains(r.Name!))
                    .Select(r => r.Id)
                    .ToListAsync();

                // Fetch menus assigned strictly to user's roles from RoleMenuRights table
                var bMenuRights = await _context.RoleMenuRights
                    .Where(rm => bRoleIds.Contains(rm.RoleId) && rm.CanView == true && rm.IsActive == true && rm.IsDeleted == false)
                    .ToListAsync();

                var bMenuIds = bMenuRights.Select(rm => rm.MenuId).Distinct().ToList();

                string bPrimaryRole = rolesList.FirstOrDefault() ?? "";

                var dashboardMapping = await _context.RoleDashboardMappings
                    .FirstOrDefaultAsync(r => r.RoleName == bPrimaryRole && r.IsActive);

                string bRedirectUrl = dashboardMapping != null && !string.IsNullOrEmpty(dashboardMapping.RedirectUrl) ? dashboardMapping.RedirectUrl : "/Home/Index";
                string dynamicDashboardController = "Home";

                if (!string.IsNullOrEmpty(bRedirectUrl))
                {
                    var parts = bRedirectUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        dynamicDashboardController = parts[0];
                    }
                }

                var bDbMenus = await _context.Menus
                    .Where(m => bMenuIds.Contains(m.Id) && m.IsActive == true && m.IsDeleted == false)
                    .OrderBy(m => m.DisplayOrder)
                    .ToListAsync();

                var bMenus = bDbMenus.Select(m => new {
                    m.Id,
                    m.ParentId,
                    MenuName = m.MenuName,
                    Controller = m.MenuName == "Dashboard" ? dynamicDashboardController : m.Controller,
                    Action = m.MenuName == "Dashboard" ? "Dashboard" : m.Action,
                    RouteUrl = m.MenuName == "Dashboard" ? dashboardMapping?.RedirectUrl : m.RouteUrl,
                    m.Icon,
                    m.DisplayOrder,
                    m.MenuType
                }).ToList();

                string bFirstName = businessUser != null ? (businessUser.FirstName ?? "") : "";
                string bLastName = businessUser != null ? (businessUser.LastName ?? "") : "";
                string userrealfullName = !string.IsNullOrEmpty(bFirstName) ? $"{bFirstName} {bLastName}".Trim() : (user.UserName ?? "");
                string mobileNo = businessUser != null ? (businessUser.MobileNo ?? "") : "";

                HttpContext.Session.SetString("IsAdminLoggedIn", "true");
                HttpContext.Session.SetString("UserId", user.Id);
                HttpContext.Session.SetString("UserEmail", user.Email ?? "");
                HttpContext.Session.SetString("UserFullName", businessFullName);
                HttpContext.Session.SetString("FirstName", bFirstName);
                HttpContext.Session.SetString("LastName", bLastName);
                HttpContext.Session.SetString("UserMobile", mobileNo);
                HttpContext.Session.SetString("UserRole", bPrimaryRole);
                HttpContext.Session.SetInt32("UserID", businessUser?.UserID ?? 0);
                HttpContext.Session.SetString("AspNetUserID", user.Id);
                HttpContext.Session.SetString("DefaultURL", dashboardMapping?.RedirectUrl ?? "/Home/Index");

                return Ok(new
                {
                    success = true,
                    token = new JwtSecurityTokenHandler().WriteToken(bToken),
                    expiration = bToken.ValidTo,
                    redirectUrl = bRedirectUrl,
                    user = new
                    {
                        user.Id,
                        FullName = userrealfullName,
                        FirstName = bFirstName,
                        LastName = bLastName,
                        Email = user.Email,
                        MobileNo = mobileNo,
                        Roles = rolesList
                    },
                    menus = bMenus,
                    rights = bMenuRights
                });
            }

            [HttpPost("logout")]
            [HttpGet("logout")]
            public async Task<IActionResult> Logout()
            {
                HttpContext.Session.Clear();
                await _signInManager.SignOutAsync();
                return Ok(new { success = true, message = "Logged out successfully from server." });
            }

            [HttpPost("forgot-password")]
            public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
            {
                string identifier = model?.Username?.Trim() ?? "";
                if (string.IsNullOrEmpty(identifier))
                    return BadRequest(new { success = false, message = "Username or Mobile Number is required." });

                var identityUser = await _context.Set<ApplicationUser>()
                    .FirstOrDefaultAsync(u => u.UserName == identifier);

                HemFoundation.Models.Entities.Users? businessUser = null;

                if (identityUser != null)
                {
                    businessUser = await _context.Users
                        .FirstOrDefaultAsync(b => b.AspNetUserID == identityUser.Id);
                }
                else
                {
                    businessUser = await _context.Users
                        .FirstOrDefaultAsync(b => b.MobileNo == identifier);

                    if (businessUser != null && !string.IsNullOrEmpty(businessUser.AspNetUserID))
                    {
                        identityUser = await _context.Set<ApplicationUser>()
                            .FirstOrDefaultAsync(u => u.Id == businessUser.AspNetUserID);
                    }
                }

                if (identityUser == null || businessUser == null)
                {
                    return NotFound(new { success = false, message = "User not found with the provided username or mobile number." });
                }

                if (businessUser.IsActive == false)
                {
                    return BadRequest(new { success = false, message = "This account is inactive or has been unsubscribed." });
                }

                string username = identityUser.UserName ?? "";
                string password = businessUser.Password_ ?? "";
                string mobileNumber = businessUser.MobileNo ?? "";
                string name = businessUser.FirstName ?? "";

                if (!string.IsNullOrEmpty(mobileNumber) && !string.IsNullOrEmpty(username))
                {
                    var smsManager = new SMSManager(_configuration);
                    smsManager.StudentRegistration(mobileNumber, name, username, password);

                    return Ok(new { success = true, message = "Login credentials have been successfully sent to your mobile number." });
                }

                return BadRequest(new { success = false, message = "Mobile number or username is missing for this account." });
            }

            [HttpGet("whatsapp-send-otp")]
            public async Task<IActionResult> WhatsappSendOTP([FromQuery] string mobileNo)
            {
                string identifier = mobileNo?.Trim() ?? "";
                if (string.IsNullOrEmpty(identifier))
                    return BadRequest(new { success = false, message = "Username or User ID is required." });

                try
                {
                    var identityUser = await _context.Set<ApplicationUser>()
                        .FirstOrDefaultAsync(u => u.UserName == identifier);

                    HemFoundation.Models.Entities.Users? businessUser = null;

                    if (identityUser != null)
                    {
                        businessUser = await _context.Users
                            .FirstOrDefaultAsync(b => b.AspNetUserID == identityUser.Id);
                    }
                    else
                    {
                        businessUser = await _context.Users
                            .FirstOrDefaultAsync(b => b.MobileNo == identifier || b.UserID.ToString() == identifier);

                        if (businessUser != null && !string.IsNullOrEmpty(businessUser.AspNetUserID))
                        {
                            identityUser = await _context.Set<ApplicationUser>()
                                .FirstOrDefaultAsync(u => u.Id == businessUser.AspNetUserID);
                        }
                    }

                    if (identityUser == null || businessUser == null || string.IsNullOrEmpty(businessUser.MobileNo))
                    {
                        return Ok(new { success = false, message = "User does not exist with this username or valid mobile number!" });
                    }

                    if (businessUser.IsActive == false)
                    {
                        return Ok(new { success = false, message = "This account is already deactivated or unsubscribed!" });
                    }

                    string otpcode = new Random().Next(1000, 9999).ToString();

                    if (!string.IsNullOrEmpty(businessUser.EmailID))
                    {
                        try
                        {
                            EmailManager.StudentEmailOTPVerification(businessUser.EmailID, businessUser.FirstName ?? "User", otpcode);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Email notification failed: " + ex.Message);
                        }
                    }

                    SMSManager sMSManager = new SMSManager(_configuration);
                    sMSManager.WhatsAppsendSMSForOTP(businessUser.MobileNo, otpcode);

                    return Ok(new { success = true, message = otpcode });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = ex.Message });
                }
            }

            [HttpPost("delete-user-account")]
            public async Task<IActionResult> DeleteUserAccount([FromBody] DeleteAccountDto model)
            {
                string identifier = model?.Identifier?.Trim() ?? "";
                if (string.IsNullOrEmpty(identifier))
                    return BadRequest(new { success = false, message = "Identifier is missing." });

                try
                {
                    HemFoundation.Models.Entities.Users? businessUser = null;
                    ApplicationUser? identityUser = null;

                    identityUser = await _context.Set<ApplicationUser>()
                        .FirstOrDefaultAsync(u => u.UserName == identifier);

                    if (identityUser != null)
                    {
                        businessUser = await _context.Users
                            .FirstOrDefaultAsync(b => b.AspNetUserID == identityUser.Id);
                    }
                    else
                    {
                        businessUser = await _context.Users
                            .FirstOrDefaultAsync(b => b.MobileNo == identifier || b.UserID.ToString() == identifier);

                        if (businessUser != null && !string.IsNullOrEmpty(businessUser.AspNetUserID))
                        {
                            identityUser = await _context.Set<ApplicationUser>()
                                .FirstOrDefaultAsync(u => u.Id == businessUser.AspNetUserID);
                        }
                    }

                    if (businessUser == null)
                    {
                        return Ok(new { success = false, message = "User account does not exist!" });
                    }

                    businessUser.IsActive = false;

                    if (identityUser != null)
                    {
                        identityUser.LockoutEnabled = true;
                        identityUser.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                    }

                    await _context.SaveChangesAsync();

                    HttpContext.Session.Clear();
                    await _signInManager.SignOutAsync();

                    return Ok(new { success = true, message = "User account has been successfully unsubscribed and deleted." });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = "An error occurred: " + ex.Message });
                }
            }
        }

        public class LoginDto
        {
            public string? Email { get; set; }
            public string? Password { get; set; }
            public bool RememberMe { get; set; }
        }

        public class ForgotPasswordDto
        {
            public string? Username { get; set; }
        }
        public class DeleteAccountDto
        {
            public string? Identifier { get; set; }
        }
    }