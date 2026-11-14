using HemFoundation.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HemFoundation.Services.HemSampark
{
    /// <summary>Who is using HEM Sampark in the current request.</summary>
    /// <param name="Key">Value stored in hemsampark_quiz_submissions.user_id.</param>
    public record HemSamparkIdentity(string Key, string DisplayName, bool IsAdminAccount);

    /// <summary>
    /// Reads the existing login (server session created by /api/auth/login, or
    /// a JWT bearer token) and answers "who is this" and "may they administer
    /// HEM Sampark". It adds no login mechanism of its own.
    /// </summary>
    public class HemSamparkAccess
    {
        private const string AdminCacheKey = "HemSamparkIsAdmin";

        private readonly IHttpContextAccessor _http;
        private readonly ApplicationDbContext _db;

        public HemSamparkAccess(IHttpContextAccessor http, ApplicationDbContext db)
        {
            _http = http;
            _db = db;
        }

        public HemSamparkIdentity? Current
        {
            get
            {
                var ctx = _http.HttpContext;
                if (ctx == null) return null;

                var session = ctx.Session;

                if (session.GetString("IsAdminLoggedIn") == "true")
                {
                    var name = $"{session.GetString("FirstName")} {session.GetString("LastName")}".Trim();
                    if (name.Length == 0) name = session.GetString("UserFullName") ?? "";

                    // Regular (student / teacher / school ...) accounts.
                    var userId = session.GetInt32("UserID") ?? 0;
                    if (userId > 0)
                        return new HemSamparkIdentity(userId.ToString(), name, false);

                    // Accounts from the Admins table have no numeric UserID.
                    var accountId = session.GetString("UserId");
                    if (!string.IsNullOrEmpty(accountId))
                    {
                        bool isAdmin = session.GetString("UserRole") == "Admin";
                        return new HemSamparkIdentity(
                            isAdmin ? $"admin:{accountId}" : accountId, name, isAdmin);
                    }
                }

                // API clients that send the JWT issued by /api/auth/login.
                var user = ctx.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    var display = user.FindFirstValue(ClaimTypes.Name) ?? "";
                    var subject = user.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (user.IsInRole("Admin") && !string.IsNullOrEmpty(subject))
                        return new HemSamparkIdentity($"admin:{subject}", display, true);

                    var businessId = user.FindFirstValue("UserID");
                    if (!string.IsNullOrEmpty(businessId))
                        return new HemSamparkIdentity(businessId, display, false);

                    if (!string.IsNullOrEmpty(subject))
                        return new HemSamparkIdentity(subject, display, false);
                }

                return null;
            }
        }

        /// <summary>
        /// True for Admin accounts, and for any role that has been granted the
        /// "Hem Sampark Admin" menu through the existing role-menu rights.
        /// </summary>
        public async Task<bool> IsAdminAsync()
        {
            var ctx = _http.HttpContext;
            if (ctx == null) return false;

            var identity = Current;
            if (identity == null) return false;
            if (identity.IsAdminAccount) return true;

            var session = ctx.Session;
            var cached = session.GetString(AdminCacheKey);
            if (cached != null) return cached == "1";

            var roleNames = new List<string>();
            var sessionRole = session.GetString("UserRole");
            if (!string.IsNullOrEmpty(sessionRole)) roleNames.Add(sessionRole);
            roleNames.AddRange(ctx.User.FindAll(ClaimTypes.Role).Select(c => c.Value));

            bool allowed = false;

            if (roleNames.Count > 0)
            {
                if (roleNames.Contains("Admin"))
                {
                    allowed = true;
                }
                else
                {
                    var roleIds = await _db.Roles
                        .Where(r => roleNames.Contains(r.Name!))
                        .Select(r => r.Id)
                        .ToListAsync();

                    allowed = await (
                        from right in _db.RoleMenuRights
                        join menu in _db.Menus on right.MenuId equals menu.Id
                        where roleIds.Contains(right.RoleId)
                              && right.CanView && right.IsActive && !right.IsDeleted
                              && menu.IsActive && !menu.IsDeleted
                              && ((menu.RouteUrl != null &&
                                   menu.RouteUrl.ToLower().Contains("hemsampark/admin"))
                                  || (menu.Controller != null && menu.Action != null &&
                                      menu.Controller.ToLower() == "hemsampark" &&
                                      menu.Action.ToLower() == "admin"))
                        select right.Id).AnyAsync();
                }
            }

            session.SetString(AdminCacheKey, allowed ? "1" : "0");
            return allowed;
        }
    }

    /// <summary>
    /// Requires a logged-in HEM Sampark user. State-changing requests must also
    /// carry X-Requested-With, which a cross-site form cannot send.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class HemSamparkUserAttribute : Attribute, IAsyncAuthorizationFilter
    {
        protected virtual bool RequireAdmin => false;

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var http = context.HttpContext;
            var access = http.RequestServices.GetRequiredService<HemSamparkAccess>();

            if (access.Current == null)
            {
                context.Result = new JsonResult(new
                {
                    success = false,
                    message = "Your session has expired. Please log in again."
                })
                { StatusCode = StatusCodes.Status401Unauthorized };
                return;
            }

            if (RequireAdmin && !await access.IsAdminAsync())
            {
                context.Result = new JsonResult(new
                {
                    success = false,
                    message = "You do not have permission to manage HEM Sampark."
                })
                { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }

            if (!HttpMethods.IsGet(http.Request.Method) &&
                !HttpMethods.IsHead(http.Request.Method) &&
                !http.Request.Headers.ContainsKey("X-Requested-With"))
            {
                context.Result = new JsonResult(new
                {
                    success = false,
                    message = "Invalid request."
                })
                { StatusCode = StatusCodes.Status400BadRequest };
            }
        }
    }

    /// <summary>Requires HEM Sampark admin rights.</summary>
    public class HemSamparkAdminAttribute : HemSamparkUserAttribute
    {
        protected override bool RequireAdmin => true;
    }
}
