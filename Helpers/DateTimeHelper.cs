namespace HemFoundation.Helpers
{
    public static class DateTimeHelper
    {
        public static DateTime UtcNow => DateTime.UtcNow;

        public static DateTime? ToUtc(DateTime? date)
        {
            if (date == null)
                return null;

            return DateTime.SpecifyKind(date.Value, DateTimeKind.Utc);
        }

        public static DateTime ToUtc(DateTime date)
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }
    }
}