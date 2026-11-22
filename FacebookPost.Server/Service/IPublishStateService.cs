using FacebookPost.Server.Model;

namespace FacebookPost.Server.Service
{
    public interface IPublishStateService
    {
        /// <summary>Trạng thái của ngày chỉ định. Trả về None nếu chưa có hoặc file lưu là của ngày khác.</summary>
        Task<PublishStatus> GetStatusAsync(DateOnly date);

        Task SaveAsync(DateTime vietnamTime, PublishStatus status, string? message = null);
    }
}
