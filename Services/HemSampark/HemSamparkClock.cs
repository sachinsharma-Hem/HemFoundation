namespace HemFoundation.Services.HemSampark
{
    /// <summary>
    /// Single source of "now" and "today" for HEM Sampark. Publications are
    /// selected by the business date (India time by default), never by the
    /// browser's clock or the hosting server's local time zone.
    /// Configure with HemSampark:TimeZone (IANA or Windows id).
    /// </summary>
    public class HemSamparkClock
    {
        private readonly TimeZoneInfo _zone;

        public HemSamparkClock(IConfiguration config)
        {
            _zone = Resolve(config["HemSampark:TimeZone"]);
        }

        private static TimeZoneInfo Resolve(string? configured)
        {
            var candidates = new[] { configured, "Asia/Kolkata", "India Standard Time" };

            foreach (var id in candidates)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }

            return TimeZoneInfo.CreateCustomTimeZone(
                "HemSamparkIST", TimeSpan.FromMinutes(330), "India Standard Time", "IST");
        }

        public string ZoneId => _zone.Id;

        /// <summary>Current business date and time (Kind = Unspecified).</summary>
        public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone);

        /// <summary>Current business date at midnight.</summary>
        public DateTime Today => Now.Date;
    }
}
