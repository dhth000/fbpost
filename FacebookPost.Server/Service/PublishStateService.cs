using System.Text.Json;
using System.Text.Json.Serialization;
using FacebookPost.Server.Model;

namespace FacebookPost.Server.Service
{
    /// <summary>
    /// Lưu / đọc trạng thái chạy PublishAllFacebookJob trong file data/publish-state.json (cạnh file .dll).
    /// </summary>
    public class PublishStateService : IPublishStateService
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const string TimeFormat = "HH:mm:ss";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly SemaphoreSlim _fileLock = new(1, 1);

        // Bản sao trạng thái trong bộ nhớ: nếu IIS không cho ghi file (thiếu quyền) thì trong cùng process
        // vẫn biết hôm nay đã Passed, tránh job đăng lại mỗi lần trigger.
        private PublishState? _memoryState;
        private readonly string _filePath = Path.Combine(AppContext.BaseDirectory, "data", "publish-state.json");
        private readonly ILogger<PublishStateService> _logger;

        public PublishStateService(ILogger<PublishStateService> logger)
        {
            _logger = logger;
        }

        public async Task<PublishStatus> GetStatusAsync(DateOnly date)
        {
            var dateText = date.ToString(DateFormat);

            await _fileLock.WaitAsync();
            try
            {
                if (_memoryState?.Date == dateText && _memoryState.Status == PublishStatus.Passed)
                {
                    return PublishStatus.Passed;
                }

                if (!File.Exists(_filePath))
                {
                    return _memoryState?.Date == dateText ? _memoryState.Status : PublishStatus.None;
                }

                var json = await File.ReadAllTextAsync(_filePath);
                var state = JsonSerializer.Deserialize<PublishState>(json, JsonOptions);

                return state != null && state.Date == dateText
                    ? state.Status
                    : PublishStatus.None;
            }
            catch (Exception ex)
            {
                // File hỏng / không đọc được -> dùng trạng thái trong bộ nhớ, không có thì coi như chưa chạy
                _logger.LogError(ex, "Không đọc được file trạng thái {FilePath}", _filePath);
                return _memoryState?.Date == dateText ? _memoryState.Status : PublishStatus.None;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task SaveAsync(DateTime vietnamTime, PublishStatus status, string? message = null)
        {
            var state = new PublishState
            {
                Date = vietnamTime.ToString(DateFormat),
                Time = vietnamTime.ToString(TimeFormat),
                Status = status,
                Message = message
            };

            await _fileLock.WaitAsync();
            try
            {
                _memoryState = state;

                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

                // Ghi ra file tạm rồi move để tránh file bị hỏng nếu app tắt giữa chừng
                var tempPath = _filePath + ".tmp";
                await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(state, JsonOptions));
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex,
                    "Không ghi được file trạng thái {FilePath}. Kiểm tra quyền Modify của App Pool trên thư mục data. " +
                    "Trạng thái chỉ còn lưu trong bộ nhớ, sẽ mất khi app restart.", _filePath);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}
