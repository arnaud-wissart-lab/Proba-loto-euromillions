namespace Worker.Jobs;

public sealed class SendSubscriptionsJobOptions
{
    public const string SectionName = "Jobs:SendSubscriptions";

    public string Cron { get; init; } = "0 0/5 * * * ?";

    public string TimeZoneId { get; init; } = "Europe/Paris";

    public bool RunOnStartup { get; init; }
}
