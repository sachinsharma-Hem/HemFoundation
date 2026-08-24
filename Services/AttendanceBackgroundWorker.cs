using HemFoundation.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HemFoundation.Services
{
    public class AttendanceBackgroundWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AttendanceBackgroundWorker> _logger;

        public AttendanceBackgroundWorker(IServiceProvider serviceProvider, ILogger<AttendanceBackgroundWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Attendance Auto-Checkout Background Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        var today = DateTime.UtcNow.Date;

                        // Find all records where status is CheckedIn but date is strictly before today (meaning midnight passed)
                        var expiredAttendances = await dbContext.AffiliateAttendances
                            .Where(x => x.Status == "CheckedIn" && x.AttendanceDate < today && !x.IsDeleted)
                            .ToListAsync(stoppingToken);

                        if (expiredAttendances.Any())
                        {
                            foreach (var att in expiredAttendances)
                            {
                                // Set auto punch-out time to end of that respective day (23:59:59) or PunchInTime
                                att.PunchOutTime = att.AttendanceDate.AddHours(23).AddMinutes(59).AddSeconds(59);
                                att.PunchOutLocation = "System Auto-Checkout (Midnight)";
                                att.Status = "CheckedOut";
                                att.PunchOutMode = "System"; // Tracked as system action
                                att.ModifiedDate = DateTime.UtcNow;

                                if (att.PunchInTime.HasValue)
                                {
                                    var diff = att.PunchOutTime.Value - att.PunchInTime.Value;
                                    att.TotalHoursWorked = Math.Round(diff.TotalHours, 2);
                                }
                            }

                            await dbContext.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation($"Auto-checked out {expiredAttendances.Count} expired attendance records.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while executing attendance auto-checkout worker.");
                }

                // Run check every 30 minutes
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }
}