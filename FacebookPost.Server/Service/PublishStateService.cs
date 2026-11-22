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
        private readonly string _filePath = Path.Combine(AppContext.BaseDirectory, "data", "publish-state.json");
        private readonly ILogger<PublishStateService> _logger;

        public PublishStateService(ILogger<PublishStateService> logger)
        {
            _logger = logger;
        }

        public async Task<PublishStatus> GetStatusAsync(DateOnly date)
        {
            await _fileLock.WaitAsync();
            try
            {
                if (!File.Exists(_filePath))
                {
                    return PublishStatus.None;
                }

                var json = await File.ReadAllTextAsync(_filePath);
                var state = JsonSerializer.Deserialize<PublishState>(json, JsonOptions);

                return state != null && state.Date == date.ToString(DateFormat)
                    ? state.Status
                    : PublishStatus.None;
            }
            catch (Exception ex)
            {
                // File hỏng / không đọc được -> coi như chưa chạy để job chạy lại
                _logger.LogError(ex, "Không đọc được file trạng thái {FilePath}", _filePath);
                return PublishStatus.None;
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
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

                // Ghi ra file tạm rồi move để tránh file bị hỏng nếu app tắt giữa chừng
                var tempPath = _filePath + ".tmp";
                await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(state, JsonOptions));
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không ghi được file trạng thái {FilePath}", _filePath);
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}
