using System.Diagnostics;
using FacebookPost.Server.Helper;
using FacebookPost.Server.Model;
using FacebookPost.Server.Options;
using FacebookPost.Server.Providers;
using FacebookPost.Server.Service;
using Microsoft.Extensions.Options;
using Quartz;

namespace FacebookPost.Server.SchedulerJobs
{
    /// <summary>
    /// Đăng bài lên tất cả các Facebook page.
    /// Được trigger lặp lại trong khung giờ cấu hình (mặc định 22:01 -> 23:30, mỗi 5 phút).
    /// Mỗi lần trigger kiểm tra data/publish-state.json: nếu hôm nay đã Passed thì bỏ qua,
    /// nếu None / Failed thì chạy (lại) toàn bộ page.
    /// Log được ghi ra cả ILogger (stdout log của IIS) và file logs/publish-all-facebook.log.
    /// </summary>
    [DisallowConcurrentExecution]
    public class PublishAllFacebookJob : IJob
    {
        private const string LogFileName = "publish-all-facebook.log";

        private readonly IFacebookService _facebookService;
        private readonly IPublishStateService _publishStateService;
        private readonly ILogger<PublishAllFacebookJob> _logger;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly PublishAllFacebookJobOptions _options;

        public PublishAllFacebookJob(
            IFacebookService facebookService,
            IPublishStateService publishStateService,
            ILogger<PublishAllFacebookJob> logger,
            IDateTimeProvider dateTimeProvider,
            IOptions<PublishAllFacebookJobOptions> options)
        {
            _facebookService = facebookService;
            _publishStateService = publishStateService;
            _logger = logger;
            _dateTimeProvider = dateTimeProvider;
            _options = options.Value;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var now = _dateTimeProvider.VietnamNow;
            var nowTime = TimeOnly.FromDateTime(now);

            // Chặn trường hợp bị trigger ngoài khung giờ (ví dụ misfire chạy bù sau 23:30)
            if (nowTime < _options.StartTime || nowTime > _options.EndTime)
            {
                _logger.LogInformation(
                    "PublishAllFacebook SKIPPED: {Time} ngoài khung giờ {Start} - {End}",
                    nowTime, _options.StartTime, _options.EndTime);
                return;
            }

            var today = DateOnly.FromDateTime(now);
            var status = await _publishStateService.GetStatusAsync(today);

            if (status == PublishStatus.Passed)
            {
                // Không ghi file log để tránh log lặp lại mỗi 5 phút
                _logger.LogDebug("PublishAllFacebook SKIPPED: {Date} đã Passed", today);
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            await WriteLogAsync(
                $"START | PreviousStatus={status} | PID={Environment.ProcessId} | FireInstanceId={context.FireInstanceId}");

            try
            {
                await _facebookService.ProcessCreatePostToAllFacebookPages();

                stopwatch.Stop();
                await _publishStateService.SaveAsync(_dateTimeProvider.VietnamNow, PublishStatus.Passed);
                await WriteLogAsync($"SUCCESS | Duration={stopwatch.Elapsed:hh\\:mm\\:ss\\.fff}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "PublishAllFacebookJob failed");

                await _publishStateService.SaveAsync(
                    _dateTimeProvider.VietnamNow,
                    PublishStatus.Failed,
                    $"{ex.GetType().Name}: {ex.Message}");

                await WriteLogAsync(
                    $"FAILED | Duration={stopwatch.Elapsed:hh\\:mm\\:ss\\.fff} | {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex}");

                // Không refire ngay, lần trigger kế tiếp (sau IntervalMinutes) sẽ chạy lại
                throw new JobExecutionException(ex, refireImmediately: false);
            }
        }

        private async Task WriteLogAsync(string content)
        {
            var message = $"[{_dateTimeProvider.VietnamNow:yyyy-MM-dd HH:mm:ss}] PublishAllFacebook {content}";

            _logger.LogInformation("{Message}", message);
            await JobFileLogger.WriteAsync(LogFileName, message, _logger);
        }
    }
}
