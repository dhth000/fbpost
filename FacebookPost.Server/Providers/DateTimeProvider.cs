using FacebookPost.Server.Helper;

namespace FacebookPost.Server.Providers
{
    public class DateTimeProvider : IDateTimeProvider
    {
        private static readonly TimeZoneInfo VietnamTimeZone =
            OperatingSystem.IsWindows()
                ? TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")
                : TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

        public DateTime UtcNow => DateTime.UtcNow;

        public DateTime VietnamNow =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);

        public long ToUnixTimeSeconds(DateTime vietnamTime)
        {
            var utc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(vietnamTime, DateTimeKind.Unspecified),
                TimeHelper.TimeZone);

            return new DateTimeOffset(utc)
                .ToUnixTimeSeconds();
        }
    }
}
