using HemFoundation.Data;
using HemFoundation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using HemFoundation.Data;
using HemFoundation.Models;

namespace HemFoundation.Services
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // 1. Log to DB
                await LogExceptionToDatabaseAsync(context, ex);

                // 2. Re-throw to let app.UseExceptionHandler() render your Error.cshtml page
                throw;
            }
        }

        private async Task LogExceptionToDatabaseAsync(HttpContext context, Exception ex)
        {
            try
            {
                // DbContext is Scoped; resolve it through the request's service provider
                using var scope = context.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

                var log = new ErrorLog
                {
                    RequestId = traceId,
                    RequestPath = context.Request.Path,
                    HttpMethod = context.Request.Method,
                    Message = ex.Message,
                    ExceptionType = ex.GetType().FullName,
                    StackTrace = ex.StackTrace,
                    InnerException = ex.InnerException?.ToString(),
                    UserIdentifier = context.User.Identity?.IsAuthenticated == true
                                     ? context.User.Identity.Name
                                     : "Anonymous",
                    CreatedAtUtc = DateTime.Now
                };

                await dbContext.ErrorLogs.AddAsync(log);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception dbEx)
            {
                // Failsafe: If DB connection is down, fallback to console/file logger
                _logger.LogError(dbEx, "CRITICAL: Global exception logger failed to write to database.");
            }
        }
    }
}