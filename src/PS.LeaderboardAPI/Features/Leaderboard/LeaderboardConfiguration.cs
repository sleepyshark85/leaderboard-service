namespace PS.LeaderboardAPI.Features.Leaderboard;

public class LeaderboardConfiguration
{
    public const string SectionName = "Leaderboard";

    public int TopLimit { get; set; } = 10;

    public int NearbyRange { get; set; } = 2;

    public int ResetIntervalHours { get; set; } = 24;

    public string ResetTimeZone { get; set; } = "UTC";

    public int ResetHour { get; set; } = 0;
    public int CacheWarmupBatchSize { get; set; } = 1000;

    public DateTimeOffset GetNextResetTime()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(ResetTimeZone);
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);

        var resetTime = new DateTimeOffset(
            now.Year, now.Month, now.Day,
            ResetHour, 0, 0,
            timeZone.GetUtcOffset(now.DateTime));

        if (resetTime <= now)
        {
            resetTime = resetTime.AddDays(1);
        }

        return resetTime.ToUniversalTime();
    }
}