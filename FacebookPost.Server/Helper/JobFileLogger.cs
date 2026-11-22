using System.Collections.Concurrent;

namespace FacebookPost.Server.Helper
{
    /// <summary>
    /// Ghi log của Quartz job ra file trong thư mục logs/ (cạnh file .dll), dùng để kiểm tra job khi chạy trên IIS.
    /// </summary>
    public static class JobFileLogger
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new();

        public static async Task WriteAsync(string fileName, string message, ILogger? logger = null)
        {
            try
            {
                var logFolder = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logFolder);

                var fileLock = FileLocks.GetOrAdd(fileName, _ => new SemaphoreSlim(1, 1));

                await fileLock.WaitAsync();
                try
                {
                    await File.AppendAllTextAsync(
                        Path.Combine(logFolder, fileName),
                        message + Environment.NewLine);
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Không ghi được file log {FileName}", fileName);
            }
        }
    }
}
