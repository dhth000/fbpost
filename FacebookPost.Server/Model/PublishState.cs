namespace FacebookPost.Server.Model
{
    public enum PublishStatus
    {
        None,
        Passed,
        Failed
    }

    /// <summary>
    /// Trạng thái lần chạy gần nhất của PublishAllFacebookJob, lưu ở data/publish-state.json.
    /// </summary>
    public class PublishState
    {
        /// <summary>Ngày chạy (giờ Việt Nam), định dạng yyyy-MM-dd.</summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>Giờ kết thúc lần chạy (giờ Việt Nam), định dạng HH:mm:ss.</summary>
        public string Time { get; set; } = string.Empty;

        public PublishStatus Status { get; set; } = PublishStatus.None;

        public string? Message { get; set; }
    }
}
