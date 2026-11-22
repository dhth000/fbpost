using FacebookPost.Server.Helper;
using FacebookPost.Server.Providers;
using Quartz;

namespace FacebookPost.Server.SchedulerJobs
{
    /// <summary>
    /// Job test: chạy mỗi phút, ghi log để kiểm tra Quartz có được trigger khi chạy trên IIS hay không.
    /// Log được ghi ra cả ILogger (stdout log của IIS) và file logs/heartbeat.log.
    /// </summary>
    [DisallowConcurrentExecution]
    public class HeartbeatJob : IJob
    {
        private const string LogFileName = "heartbeat.log";

        private readonly ILogger<HeartbeatJob> _logger;
        private readonly IDateTimeProvider _dateTimeProvider;

        public HeartbeatJob(ILogger<HeartbeatJob> logger, IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var nextFire = context.NextFireTimeUtc.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(context.NextFireTimeUtc.Value.UtcDateTime, TimeHelper.TimeZone)
                    .ToString("yyyy-MM-dd HH:mm:ss")
                : "N/A";

            var message =
                $"[{_dateTimeProvider.VietnamNow:yyyy-MM-dd HH:mm:ss}] Heartbeat OK | " +
                $"PID={Environment.ProcessId} | " +
                $"Process={System.Diagnostics.Process.GetCurrentProcess().ProcessName} | " +
                $"NextFire={nextFire}";

            _logger.LogInformation("{Message}", message);
            await JobFileLogger.WriteAsync(LogFileName, message, _logger);
        }
    }
}
