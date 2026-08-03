namespace FacebookPost.Server.Helper
{
    public static class TimeHelper
    {
        public static readonly TimeZoneInfo TimeZone =
        OperatingSystem.IsWindows()
            ? TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
            : TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    }
}
