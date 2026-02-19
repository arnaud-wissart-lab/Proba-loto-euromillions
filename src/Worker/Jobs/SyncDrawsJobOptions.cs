namespace Worker.Jobs;

public sealed class SyncDrawsJobOptions
{
    public const string SectionName = "Jobs:SyncDraws";

    public string Cron { get; init; } = "0 30 2 * * ?";

    public string TimeZoneId { get; init; } = "Europe/Paris";

    public bool RunOnStartup { get; init; }
}
