using System.Diagnostics;
using FacebookPost.Server.Helper;
using FacebookPost.Server.Providers;
using FacebookPost.Server.Service;
using Quartz;

namespace FacebookPost.Server.SchedulerJobs
{
    /// <summary>
    /// Đăng bài lên tất cả các Facebook page theo lịch cron.
    /// Log được ghi ra cả ILogger (stdout log của IIS) và file logs/publish-all-facebook.log.
    /// </summary>
    [DisallowConcurrentExecution]
    public class PublishAllFacebookJob : IJob
    {
        private const string LogFileName = "publish-all-facebook.log";

        private readonly IFacebookService _facebookService;
        private readonly ILogger<PublishAllFacebookJob> _logger;
        private readonly IDateTimeProvider _dateTimeProvider;

        public PublishAllFacebookJob(
            IFacebookService facebookService,
            ILogger<PublishAllFacebookJob> logger,
            IDateTimeProvider dateTimeProvider)
        {
            _facebookService = facebookService;
            _logger = logger;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            var nextFire = context.NextFireTimeUtc.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(context.NextFireTimeUtc.Value.UtcDateTime, TimeHelper.TimeZone)
                    .ToString("yyyy-MM-dd HH:mm:ss")
                : "N/A";

            await WriteLogAsync(
                $"START | PID={Environment.ProcessId} | FireInstanceId={context.FireInstanceId} | NextFire={nextFire}");

            try
            {
                await _facebookService.ProcessCreatePostToAllFacebookPages();

                stopwatch.Stop();
                await WriteLogAsync($"SUCCESS | Duration={stopwatch.Elapsed:hh\\:mm\\:ss\\.fff}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "PublishAllFacebookJob failed");
                await WriteLogAsync(
                    $"FAILED | Duration={stopwatch.Elapsed:hh\\:mm\\:ss\\.fff} | {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex}");

                // Ném lại để Quartz ghi nhận job lỗi
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
