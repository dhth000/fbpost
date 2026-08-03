namespace FacebookPost.Server.Providers
{
    public interface IDateTimeProvider
    {
        DateTime UtcNow { get; }
        DateTime VietnamNow { get; }
        long ToUnixTimeSeconds(DateTime vietnamTime);
    }
}
