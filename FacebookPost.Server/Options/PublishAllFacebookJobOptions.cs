namespace FacebookPost.Server.Options
{
    /// <summary>
    /// Cấu hình khung giờ chạy PublishAllFacebookJob (giờ Việt Nam).
    /// Job được trigger lặp lại mỗi IntervalMinutes trong khoảng StartTime -> EndTime,
    /// nếu trạng thái hôm nay chưa Passed thì sẽ chạy (lại).
    /// </summary>
    public class PublishAllFacebookJobOptions
    {
        public const string SectionName = "Quartz:PublishAllFacebook";

        public TimeOnly StartTime { get; set; } = new(22, 1);

        public TimeOnly EndTime { get; set; } = new(23, 30);

        public int IntervalMinutes { get; set; } = 5;
    }
}
