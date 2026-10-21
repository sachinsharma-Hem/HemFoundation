using HemFoundation.Data;
using HemFoundation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HemFoundation.Services
{
    public class ExceptionLoggerService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ExceptionLoggerService> _logger;

        public ExceptionLoggerService(
            IHttpContextAccessor httpContextAccessor,
            IServiceProvider serviceProvider,
            ILogger<ExceptionLoggerService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task LogExceptionAsync(Exception ex)
        {
            try
            {
                var context = _httpContextAccessor.HttpContext;
                if (context == null) return;

                // Resolve ApplicationDbContext from a new scope
                using var scope = _serviceProvider.CreateScope();
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
                    CreatedAtUtc = DateTime.UtcNow
                };

                await dbContext.ErrorLogs.AddAsync(log);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "CRITICAL: Exception logger failed to write to database.");
            }
        }
    }
}