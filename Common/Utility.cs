namespace HemFoundation.Common
{
    
    public static class DateTimeExtensions
    {
        public static string ConvertDateToString(this DateTime date, string format = "yyyy-MM-dd")
        {
            return date.ToString(format);
        }
    }
}
